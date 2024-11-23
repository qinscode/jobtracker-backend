using JobTracker.Models;

namespace JobTracker.Services;

public interface IEmailAnalysisService
{
    // 分析最近的邮件（手动触发）
    Task<List<EmailAnalysisDto>> AnalyzeRecentEmails(UserEmailConfig config);

    // 分析新邮件（后台服务使用）
    Task<List<EmailAnalysisDto>> AnalyzeNewEmails(UserEmailConfig config, DateTime? since = null);

    Task<bool> IsRejectionEmail(string emailContent);
    Task ProcessRejectionEmail(string emailContent, Guid userId);
}