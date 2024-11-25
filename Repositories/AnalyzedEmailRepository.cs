using JobTracker.Data;
using JobTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Repositories;

public class AnalyzedEmailRepository : IAnalyzedEmailRepository
{
    private readonly JobTrackerContext _context;

    public AnalyzedEmailRepository(JobTrackerContext context)
    {
        _context = context;
    }

    public async Task<AnalyzedEmail> CreateAsync(AnalyzedEmail analyzedEmail)
    {
        _context.AnalyzedEmails.Add(analyzedEmail);
        await _context.SaveChangesAsync();
        return analyzedEmail;
    }

    public async Task CreateManyAsync(IEnumerable<AnalyzedEmail> analyzedEmails)
    {
        await _context.AnalyzedEmails.AddRangeAsync(analyzedEmails);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid userEmailConfigId, string messageId)
    {
        return await _context.AnalyzedEmails
            .AnyAsync(ae => ae.UserEmailConfigId == userEmailConfigId && ae.MessageId == messageId);
    }

    public async Task<DateTime?> GetLastAnalyzedDateAsync(Guid userEmailConfigId)
    {
        return await _context.AnalyzedEmails
            .Where(ae => ae.UserEmailConfigId == userEmailConfigId)
            .OrderByDescending(ae => ae.ReceivedDate)
            .Select(ae => ae.ReceivedDate)
            .FirstOrDefaultAsync();
    }

    public async Task<uint?> GetLastAnalyzedUidAsync(Guid userEmailConfigId)
    {
        return await _context.AnalyzedEmails
            .Where(ae => ae.UserEmailConfigId == userEmailConfigId && ae.Uid != null)
            .OrderByDescending(ae => ae.Uid)
            .Select(ae => ae.Uid)
            .FirstOrDefaultAsync();
    }
}