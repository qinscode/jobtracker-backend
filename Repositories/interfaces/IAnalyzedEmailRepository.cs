using JobTracker.Models;

namespace JobTracker.Repositories;

public interface IAnalyzedEmailRepository
{
    Task<AnalyzedEmail> CreateAsync(AnalyzedEmail analyzedEmail);
    Task<bool> ExistsAsync(Guid userEmailConfigId, string messageId);
    Task<DateTime?> GetLastAnalyzedDateAsync(Guid userEmailConfigId);
    Task<uint?> GetLastAnalyzedUidAsync(Guid userEmailConfigId);
    Task CreateManyAsync(IEnumerable<AnalyzedEmail> analyzedEmails);
    Task<HashSet<string>> GetProcessedMessageIdsAsync(Guid userEmailConfigId);
    Task<List<AnalyzedEmail>> GetEmailsByJobIdAsync(int jobId);
    Task UpdateAsync(AnalyzedEmail analyzedEmail);

    Task<(List<AnalyzedEmail> Emails, int TotalCount)> GetAnalyzedEmailsAsync(
        Guid userEmailConfigId,
        int pageNumber,
        int pageSize);

    Task<(List<AnalyzedEmail> Emails, int TotalCount)> SearchAnalyzedEmailsAsync(
        Guid userId,
        string searchTerm,
        int pageNumber,
        int pageSize);
}