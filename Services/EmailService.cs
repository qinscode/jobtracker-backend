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
    private readonly IAnalyzedEmailRepository _analyzedEmailRepository;
    private readonly ILogger<EmailService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly EmailServiceConfig _config;
    private static readonly TimeZoneInfo PerthTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Perth");

    public EmailService(
        ILogger<EmailService> logger,
        IAnalyzedEmailRepository analyzedEmailRepository,
        IConfiguration configuration)
    {
        _logger = logger;
        _analyzedEmailRepository = analyzedEmailRepository;
        _config = configuration.GetSection("EmailScanning").Get<EmailServiceConfig>()
                  ?? new EmailServiceConfig();

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                _config.Gmail.RetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(
                    Math.Pow(2, retryAttempt) * _config.Gmail.RetryDelaySeconds)
            );
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
                var startIndex = Math.Max(0, totalEmails - _config.Gmail.MaxConcurrentScans);

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

        await _retryPolicy.ExecuteAsync(async () =>
        {
            using var emailClient = new ImapClient();
            try
            {
                await ConnectWithTimeout(emailClient, config);
                var inbox = emailClient.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);

                // 获取最后分析的日期
                var lastAnalyzedDate = await _analyzedEmailRepository.GetLastAnalyzedDateAsync(config.Id);

                // 构建查询
                SearchQuery query;
                if (lastAnalyzedDate.HasValue)
                {
                    // 转换为UTC时间，并往前推1小时以确保不会遗漏
                    var searchDate = TimeZoneInfo.ConvertTimeToUtc(lastAnalyzedDate.Value)
                        .AddHours(-1);

                    query = SearchQuery.SentSince(searchDate);
                    _logger.LogInformation(
                        "Searching for emails sent since: {SearchDate} (UTC)",
                        searchDate);
                }
                else
                {
                    query = SearchQuery.All;
                    _logger.LogInformation("No last analyzed date found, searching all emails");
                }

                var uids = await inbox.SearchAsync(query);
                _logger.LogInformation("Found {Count} new emails", uids.Count);

                if (!uids.Any())
                {
                    _logger.LogInformation("No new emails found for {Email}", config.EmailAddress);
                    return;
                }

                // 获取已处理的消息ID
                var processedMessageIds = await _analyzedEmailRepository.GetProcessedMessageIdsAsync(config.Id);
                _logger.LogInformation(
                    "Found {Count} previously processed messages",
                    processedMessageIds.Count);

                // 按批次处理邮件
                foreach (var uidBatch in uids.OrderBy(x => x.Id).Chunk(_config.Gmail.BatchSize))
                {
                    try
                    {
                        foreach (var uid in uidBatch)
                        {
                            try
                            {
                                var message = await inbox.GetMessageAsync(uid);

                                _logger.LogDebug(
                                    "Processing message - UID: {Uid}, Subject: {Subject}, MessageId: {MessageId}, Date: {Date}",
                                    uid, message.Subject, message.MessageId, message.Date);

                                if (processedMessageIds.Contains(message.MessageId))
                                {
                                    _logger.LogDebug(
                                        "Skipping already processed message: {MessageId}",
                                        message.MessageId);
                                    continue;
                                }

                                await ProcessEmailMessage(message, messages, uid);

                                _logger.LogInformation(
                                    "Successfully processed email - UID: {Uid}, Subject: {Subject}, Date: {Date}",
                                    uid, message.Subject, message.Date);

                                await Task.Delay(100);
                            }
                            catch (MessageNotFoundException ex)
                            {
                                _logger.LogWarning(ex, "Message with UID {Uid} not found", uid);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing batch of emails");
                        continue;
                    }

                    _logger.LogInformation(
                        "Processed batch of {Count} emails for {Email}",
                        uidBatch.Length, config.EmailAddress);
                }
            }
            finally
            {
                if (emailClient.IsConnected)
                    await emailClient.DisconnectAsync(true);
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
        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(_config.Gmail.RetryDelaySeconds * 2));
        try
        {
            await client.ConnectAsync(
                _config.Gmail.ImapServer,
                _config.Gmail.ImapPort,
                _config.Gmail.UseSsl,
                cts.Token);
            await client.AuthenticateAsync(
                config.EmailAddress,
                DecryptPassword(config.EncryptedPassword),
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Connection timeout for {Email}", config.EmailAddress);
            throw new TimeoutException(
                $"Connection timeout after {_config.Gmail.RetryDelaySeconds * 2} seconds");
        }
    }

    private async Task ConnectWithTimeout(ImapClient client, string email, string password)
    {
        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(_config.Gmail.RetryDelaySeconds * 2));
        try
        {
            await client.ConnectAsync(
                _config.Gmail.ImapServer,
                _config.Gmail.ImapPort,
                _config.Gmail.UseSsl,
                cts.Token);
            await client.AuthenticateAsync(email, password, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Connection timeout for {Email}", email);
            throw new TimeoutException(
                $"Connection timeout after {_config.Gmail.RetryDelaySeconds * 2} seconds");
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