using JobTracker.Models;

namespace JobTracker.Services.Interfaces;

public interface IEmailService
{
    // 扫描最近的50封邮件（用于手动触发）
    Task<IEnumerable<EmailMessage>> FetchRecentEmailsAsync(UserEmailConfig config);
    
    // 增量扫描新邮件（用于后台服务）
    Task<IEnumerable<EmailMessage>> FetchNewEmailsAsync(UserEmailConfig config, DateTime? since = null);
    
    Task ConnectAndAuthenticateAsync(string email, string password);
} 