using System.Text;
using JobTracker.Models;
using JobTracker.Repositories;
using JobTracker.Services.Interfaces;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using Polly;
using Polly.Retry;

namespace JobTracker.Services;

public class EmailService : IEmailService
{
    private const int MaxRetries = 3;
    private const int TimeoutSeconds = 30;
    private const int MaxEmailsToScan = 5;
    private static readonly TimeZoneInfo PerthTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Perth");
    private readonly IAnalyzedEmailRepository _analyzedEmailRepository;
    private readonly ILogger<EmailService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public EmailService(
        ILogger<EmailService> logger,
        IAnalyzedEmailRepository analyzedEmailRepository)
    {
        _logger = logger;
        _analyzedEmailRepository = analyzedEmailRepository;
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(MaxRetries, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    public async Task ConnectAndAuthenticateAsync(string email, string password)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            using var client = new ImapClient();
            try
            {
                await ConnectWithTimeout(client, email, password);
                _logger.LogInformation("Test connection successful for {Email}", email);
            }
            finally
            {
                if (client.IsConnected)
                {
                    await client.DisconnectAsync(true);
                    _logger.LogInformation("Test connection disconnected for {Email}", email);
                }
            }
        });
    }

    // 扫描最近的50封邮件
    public async Task<IEnumerable<EmailMessage>> FetchRecentEmailsAsync(UserEmailConfig config)
    {
        var messages = new List<EmailMessage>();

        await _retryPolicy.ExecuteAsync(async () =>
        {
            using var emailClient = new ImapClient();
            try
            {
                await ConnectWithTimeout(emailClient, config);
                var inbox = emailClient.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);

                var totalEmails = inbox.Count;
                var startIndex = Math.Max(0, totalEmails - MaxEmailsToScan);

                for (var i = totalEmails - 1; i >= startIndex; i--)
                {
                    var message = await inbox.GetMessageAsync(i);
                    await ProcessEmailMessage(message, messages);
                }
            }
            finally
            {
                if (emailClient.IsConnected) await emailClient.DisconnectAsync(true);
            }
        });

        return messages;
    }

    // 扫描所有邮件的方法
    public async Task<IEnumerable<EmailMessage>> FetchAllEmailsAsync(UserEmailConfig config)
    {
        var messages = new List<EmailMessage>();

        await _retryPolicy.ExecuteAsync(async () =>
        {
            using var emailClient = new ImapClient();
            try
            {
                await ConnectWithTimeout(emailClient, config);
                var inbox = emailClient.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);

                var totalEmails = inbox.Count;

                // 从最新的邮件开始处理
                for (var i = totalEmails - 1; i >= 0; i--)
                {
                    // 每处理100封邮件记录一次日志
                    if (i % 100 == 0)
                        _logger.LogInformation("Processing email {Current}/{Total} for {Email}",
                            totalEmails - i, totalEmails, config.EmailAddress);

                    var message = await inbox.GetMessageAsync(i);

                    // 检查是否已经分析过
                    if (await _analyzedEmailRepository.ExistsAsync(config.Id, message.MessageId)) continue;

                    await ProcessEmailMessage(message, messages);

                    // 每处理10封邮件暂停一下，避免过度消耗资源
                    if (messages.Count % 10 == 0) await Task.Delay(100);
                }
            }
            finally
            {
                if (emailClient.IsConnected) await emailClient.DisconnectAsync(true);
            }
        });

        return messages;
    }

    public async Task<IEnumerable<EmailMessage>> FetchIncrementalEmailsAsync(UserEmailConfig config,
        uint? lastUid = null)
    {
        var messages = new List<EmailMessage>();
        var batchSize = 100; // 每批处理的邮件数量

        await _retryPolicy.ExecuteAsync(async () =>
        {
            using var emailClient = new ImapClient();
            try
            {
                await ConnectWithTimeout(emailClient, config);
                var inbox = emailClient.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);

                // 获取所有UID
                var query = lastUid.HasValue
                    ? SearchQuery.Uids(new[] { new UniqueId(lastUid.Value + 1, uint.MaxValue) })
                    : SearchQuery.All;

                var uids = await inbox.SearchAsync(query);
                if (!uids.Any())
                {
                    _logger.LogInformation("No new emails found for {Email}", config.EmailAddress);
                    return;
                }

                // 按批次处理邮件
                foreach (var uidBatch in uids.OrderBy(x => x.Id).Chunk(batchSize))
                {
                    try
                    {
                        foreach (var uid in uidBatch)
                            try
                            {
                                var message = await inbox.GetMessageAsync(uid);

                                // 检查是否已经分析过
                                if (await _analyzedEmailRepository.ExistsAsync(config.Id, message.MessageId)) continue;

                                await ProcessEmailMessage(message, messages, uid);

                                // 短暂暂停，避免过度消耗资源
                                await Task.Delay(100);
                            }
                            catch (MessageNotFoundException ex)
                            {
                                _logger.LogWarning(ex, "Message with UID {Uid} not found", uid);
                            }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing batch of emails");
                        // 继续处理下一批，而不是完全中断
                        continue;
                    }

                    _logger.LogInformation("Processed batch of {Count} emails for {Email}",
                        uidBatch.Length, config.EmailAddress);
                }
            }
            finally
            {
                if (emailClient.IsConnected) await emailClient.DisconnectAsync(true);
            }
        });

        return messages;
    }

    // 增量扫描新邮件
    public async Task<IEnumerable<EmailMessage>> FetchNewEmailsAsync(UserEmailConfig config, DateTime? since = null)
    {
        var messages = new List<EmailMessage>();

        await _retryPolicy.ExecuteAsync(async () =>
        {
            using var emailClient = new ImapClient();
            try
            {
                await ConnectWithTimeout(emailClient, config);
                var inbox = emailClient.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);

                // 构建查询
                var query = SearchQuery.All;
                if (since.HasValue) query = SearchQuery.DeliveredAfter(since.Value);

                var uids = await inbox.SearchAsync(query);
                foreach (var uid in uids.OrderByDescending(x => x))
                {
                    var message = await inbox.GetMessageAsync(uid);
                    await ProcessEmailMessage(message, messages);
                }
            }
            finally
            {
                if (emailClient.IsConnected) await emailClient.DisconnectAsync(true);
            }
        });

        return messages;
    }

    private async Task ConnectWithTimeout(ImapClient client, UserEmailConfig config)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        try
        {
            await client.ConnectAsync("imap.gmail.com", 993, true, cts.Token);
            await client.AuthenticateAsync(config.EmailAddress, DecryptPassword(config.EncryptedPassword), cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Connection timeout for {Email}", config.EmailAddress);
            throw new TimeoutException($"Connection timeout after {TimeoutSeconds} seconds");
        }
    }

    private async Task ConnectWithTimeout(ImapClient client, string email, string password)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        try
        {
            await client.ConnectAsync("imap.gmail.com", 993, true, cts.Token);
            await client.AuthenticateAsync(email, password, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Connection timeout for {Email}", email);
            throw new TimeoutException($"Connection timeout after {TimeoutSeconds} seconds");
        }
    }

    private string DecryptPassword(string encryptedPassword)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedPassword));
    }

    private async Task ProcessEmailMessage(MimeMessage message, List<EmailMessage> messages)
    {
        var textBody = message.TextBody ?? message.HtmlBody;
        if (!string.IsNullOrEmpty(textBody))
            await Task.Run(() =>
            {
                messages.Add(new EmailMessage
                {
                    MessageId = message.MessageId,
                    Subject = message.Subject,
                    Body = textBody,
                    ReceivedDate = TimeZoneInfo.ConvertTimeFromUtc(message.Date.UtcDateTime, PerthTimeZone)
                });

                _logger.LogDebug("Processed email: {Subject}", message.Subject);
            });
    }

    private async Task ProcessEmailMessage(MimeMessage message, List<EmailMessage> messages, UniqueId uid)
    {
        var textBody = message.TextBody ?? message.HtmlBody;
        if (!string.IsNullOrEmpty(textBody))
            await Task.Run(() =>
            {
                messages.Add(new EmailMessage
                {
                    MessageId = message.MessageId,
                    Subject = message.Subject,
                    Body = textBody,
                    ReceivedDate = TimeZoneInfo.ConvertTimeFromUtc(message.Date.UtcDateTime, PerthTimeZone),
                    Uid = uid.Id // 添加UID到EmailMessage模型
                });

                _logger.LogDebug("Processed email: {Subject} (UID: {Uid})", message.Subject, uid);
            });
    }
}