using System.Text;
using JobTracker.Models;
using JobTracker.Services.Interfaces;
using MailKit;
using MailKit.Net.Imap;
using Polly;
using Polly.Retry;

namespace JobTracker.Services;

public class EmailService : IEmailService
{
    private const int MaxRetries = 3;
    private const int TimeoutSeconds = 30;
    private const int MaxEmailsToScan = 50;
    private static readonly TimeZoneInfo PerthTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Perth");
    private readonly ILogger<EmailService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(MaxRetries, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Error during email operation (Attempt {RetryCount} of {MaxRetries}). Waiting {TimeSpan} before next retry.",
                        retryCount,
                        MaxRetries,
                        timeSpan);
                });
    }

    public async Task<IEnumerable<EmailMessage>> FetchNewEmailsAsync(UserEmailConfig config)
    {
        _logger.LogInformation("Starting to fetch emails for {Email}", config.EmailAddress);
        var messages = new List<EmailMessage>();

        await _retryPolicy.ExecuteAsync(async () =>
        {
            using var emailClient = new ImapClient();
            try
            {
                await ConnectWithTimeout(emailClient, config);
                _logger.LogInformation("Successfully connected to email server for {Email}", config.EmailAddress);

                var inbox = emailClient.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);
                _logger.LogInformation("Opened inbox for {Email}", config.EmailAddress);

                // 获取最近的50封邮件
                var totalEmails = inbox.Count;
                var startIndex = Math.Max(0, totalEmails - MaxEmailsToScan);
                var endIndex = totalEmails - 1;

                _logger.LogInformation("Fetching emails {Start} to {End} for {Email}",
                    startIndex, endIndex, config.EmailAddress);

                // 直接获取邮件
                for (var i = endIndex; i >= startIndex; i--)
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email fetch operation for {Email}", config.EmailAddress);
                throw;
            }
            finally
            {
                if (emailClient.IsConnected)
                {
                    await emailClient.DisconnectAsync(true);
                    _logger.LogInformation("Disconnected from email server for {Email}", config.EmailAddress);
                }
            }
        });

        _logger.LogInformation("Completed fetching {Count} emails for {Email}", messages.Count, config.EmailAddress);
        return messages;
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
}