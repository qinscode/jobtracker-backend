using JobTracker.Models;

namespace JobTracker.Repositories;

public interface IAnalyzedEmailRepository
{
    Task<AnalyzedEmail> CreateAsync(AnalyzedEmail analyzedEmail);
    Task<bool> ExistsAsync(Guid userEmailConfigId, string messageId);
    Task<DateTime?> GetLastAnalyzedDateAsync(Guid userEmailConfigId);
}