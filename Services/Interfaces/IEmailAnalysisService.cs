using JobTracker.Models;

namespace JobTracker.Services;

public interface IEmailAnalysisService
{
    // 分析最近的邮件（手动触发）
    Task<List<EmailAnalysisDto>> AnalyzeRecentEmails(UserEmailConfig config);

    // 新增：分析所有邮件
    Task<List<EmailAnalysisDto>> AnalyzeAllEmails(UserEmailConfig config);

    Task<bool> IsRejectionEmail(string emailContent);
    Task ProcessRejectionEmail(string emailContent, Guid userId);
}