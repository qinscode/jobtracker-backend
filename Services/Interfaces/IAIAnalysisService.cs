namespace JobTracker.Services.Interfaces;

public interface IAIAnalysisService
{
    Task<bool> IsRejectionEmail(string emailContent);
    Task<(string CompanyName, string JobTitle)> ExtractJobInfo(string emailContent);
} 