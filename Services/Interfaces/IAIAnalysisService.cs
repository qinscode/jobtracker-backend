using JobTracker.Models;

namespace JobTracker.Services.Interfaces;

public interface IAIAnalysisService
{
    Task<bool> IsRejectionEmail(string emailContent);

    Task<(string CompanyName, string JobTitle, UserJobStatus Status, List<string> KeyPhrases, string? SuggestedActions,
            string? ReasonForRejection)>
        ExtractJobInfo(string emailContent);
}