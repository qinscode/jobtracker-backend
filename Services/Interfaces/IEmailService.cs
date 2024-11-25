using JobTracker.Models;

namespace JobTracker.Services.Interfaces;

public interface IEmailService
{
    // 扫描最近的5封邮件（用于手动触发）
    Task<IEnumerable<EmailMessage>> FetchRecentEmailsAsync(UserEmailConfig config);

    // 新增：扫描所有邮件
    Task<IEnumerable<EmailMessage>> FetchAllEmailsAsync(UserEmailConfig config);

    // 新增：增量扫描方法
    Task<IEnumerable<EmailMessage>> FetchIncrementalEmailsAsync(UserEmailConfig config, uint? lastUid = null);

    Task ConnectAndAuthenticateAsync(string email, string password);
}