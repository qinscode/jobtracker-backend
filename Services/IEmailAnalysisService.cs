using System;
using System.Threading.Tasks;
using JobTracker.Models;

namespace JobTracker.Services;

public interface IEmailAnalysisService
{
    Task ScanEmailsAsync(UserEmailConfig config);
    Task<bool> IsRejectionEmail(string emailContent);
    Task ProcessRejectionEmail(string emailContent, Guid userId);
    Task<List<EmailAnalysisDto>> AnalyzeAllEmails(UserEmailConfig config);
} 