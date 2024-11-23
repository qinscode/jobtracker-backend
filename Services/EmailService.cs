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
    private const int MaxEmailsToScan = 50;
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

    // 删除或注释掉 StartEmailMonitoringAsync 方法，因为我们现在使用后台服务
    // public async Task StartEmailMonitoringAsync(UserEmailConfig config, CancellationToken cancellationToken)
    // {
    //     // ... 
    // }

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

    public async Task<IEnumerable<EmailMessage>> FetchNewEmailsAsync(UserEmailConfig config)
    {
        var messages = new List<EmailMessage>();
        var lastAnalyzedDate = await _analyzedEmailRepository.GetLastAnalyzedDateAsync(config.Id);

        await _retryPolicy.ExecuteAsync(async () =>
        {
            using var emailClient = new ImapClient();
            try
            {
                await ConnectWithTimeout(emailClient, config);
                var inbox = emailClient.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);

                // 如果有上次分析的时间，只获取之后的邮件
                var query = SearchQuery.All;
                if (lastAnalyzedDate.HasValue) query = SearchQuery.DeliveredAfter(lastAnalyzedDate.Value);

                // 获取符合条件的邮件索引
                var indexes = await inbox.SearchAsync(query);

                // 按时间倒序排序
                var sortedIndexes = indexes.OrderByDescending(x => x);

                foreach (var i in sortedIndexes.Take(MaxEmailsToScan))
                    try
                    {
                        var message = await inbox.GetMessageAsync(i);
                        var textBody = message.TextBody ?? message.HtmlBody;

                        if (!string.IsNullOrEmpty(textBody))
                        {
                            messages.Add(new EmailMessage
                            {
                                MessageId = message.MessageId,
                                Subject = message.Subject,
                                Body = textBody,
                                ReceivedDate = TimeZoneInfo.ConvertTimeFromUtc(message.Date.UtcDateTime, PerthTimeZone)
                            });

                            // 输出邮件信息到控制台
                            Console.WriteLine("\n========== Email Found ==========");
                            Console.WriteLine($"Subject: {message.Subject}");
                            Console.WriteLine($"Date: {message.Date.DateTime}");
                            Console.WriteLine($"From: {message.From}");
                            Console.WriteLine("Content Preview: ");
                            Console.WriteLine(textBody.Length > 50 ? textBody[..50] + "..." : textBody);
                            Console.WriteLine("================================\n");

                            _logger.LogDebug("Successfully processed email with subject: {Subject}",
                                message.Subject);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing email at index {Index} for {Email}",
                            i, config.EmailAddress);
                    }
            }
            finally
            {
                if (emailClient.IsConnected) await emailClient.DisconnectAsync(true);
            }
        });

        _logger.LogInformation("Completed fetching {Count} emails for {Email}", messages.Count, config.EmailAddress);
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
}