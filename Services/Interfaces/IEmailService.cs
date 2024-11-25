using JobTracker.Models;

namespace JobTracker.Services.Interfaces;

public interface IEmailService
{
    // 扫描最近的5封邮件（用于手动触发）
    Task<IEnumerable<EmailMessage>> FetchRecentEmailsAsync(UserEmailConfig config);

    Task ConnectAndAuthenticateAsync(string email, string password);
}