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

    public async Task<HashSet<string>> GetProcessedMessageIdsAsync(Guid userEmailConfigId)
    {
        var messageIds = await _context.AnalyzedEmails
            .Where(ae => ae.UserEmailConfigId == userEmailConfigId)
            .Select(ae => ae.MessageId)
            .ToListAsync();

        return new HashSet<string>(messageIds);
    }

    public async Task<List<AnalyzedEmail>> GetEmailsByJobIdAsync(int jobId)
    {
        return await _context.AnalyzedEmails
            .Where(ae => ae.MatchedJobId == jobId)
            .ToListAsync();
    }

    public async Task UpdateAsync(AnalyzedEmail analyzedEmail)
    {
        _context.Entry(analyzedEmail).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task<(List<AnalyzedEmail> Emails, int TotalCount)> GetAnalyzedEmailsAsync(
        Guid userEmailConfigId,
        int pageNumber,
        int pageSize)
    {
        var query = _context.AnalyzedEmails
            .Include(ae => ae.MatchedJob)
            .Where(ae => ae.UserEmailConfigId == userEmailConfigId)
            .OrderByDescending(ae => ae.ReceivedDate);

        var totalCount = await query.CountAsync();

        var emails = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (emails, totalCount);
    }

    public async Task<(List<AnalyzedEmail> Emails, int TotalCount)> SearchAnalyzedEmailsAsync(
        Guid userId,
        string searchTerm,
        int pageNumber,
        int pageSize)
    {
        var query = _context.AnalyzedEmails
            .Include(ae => ae.MatchedJob)
            .Include(ae => ae.UserEmailConfig)
            .Where(ae => ae.UserEmailConfig!.UserId == userId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            // 获取所有可能匹配的邮件
            var allMatchingEmails = await query
                .Where(ae =>
                    EF.Functions.ILike(ae.Subject, $"%{searchTerm}%") ||
                    (ae.MatchedJob != null && (
                        EF.Functions.ILike(ae.MatchedJob.JobTitle ?? "", $"%{searchTerm}%") ||
                        EF.Functions.ILike(ae.MatchedJob.BusinessName ?? "", $"%{searchTerm}%")
                    ))
                )
                .ToListAsync();

            // 在内存中过滤包括关键短语的结果
            var filteredEmails = allMatchingEmails
                .Concat(allMatchingEmails.Where(e =>
                    e.KeyPhrases != null &&
                    e.KeyPhrases.Any(kp => kp.ToLower().Contains(searchTerm))))
                .Distinct()
                .OrderByDescending(e => e.ReceivedDate)
                .ToList();

            // 应用分页
            var pagedEmails = filteredEmails
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pagedEmails, filteredEmails.Count);
        }
        else
        {
            // 如果没有搜索词，直接返回分页结果
            var totalCount = await query.CountAsync();
            var emails = await query
                .OrderByDescending(ae => ae.ReceivedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (emails, totalCount);
        }
    }
}