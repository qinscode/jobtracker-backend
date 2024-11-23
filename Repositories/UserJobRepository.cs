using JobTracker.Data;
using JobTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Repositories;

public class UserJobRepository : IUserJobRepository
{
    private readonly JobTrackerContext _context;

    public UserJobRepository(JobTrackerContext context)
    {
        _context = context;
    }

    public async Task<UserJob?> GetUserJobByIdAsync(Guid id)
    {
        return await _context.UserJobs
            .Include(uj => uj.User)
            .Include(uj => uj.Job)
            .FirstOrDefaultAsync(uj => uj.Id == id);
    }

    public async Task<UserJob> CreateUserJobAsync(UserJob userJob)
    {
        _context.UserJobs.Add(userJob);
        await _context.SaveChangesAsync();
        return userJob;
    }

    public async Task UpdateUserJobAsync(UserJob userJob)
    {
        _context.Entry(userJob).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteUserJobAsync(Guid id)
    {
        var userJob = await _context.UserJobs.FindAsync(id);
        if (userJob != null)
        {
            _context.UserJobs.Remove(userJob);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<UserJob>> GetUserJobsAsync(int pageNumber, int pageSize)
    {
        return await _context.UserJobs
            .Include(uj => uj.User)
            .Include(uj => uj.Job)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetUserJobsCountAsync()
    {
        return await _context.UserJobs.CountAsync();
    }

    public async Task<UserJob?> GetUserJobByUserIdAndJobIdAsync(Guid userId, int jobId)
    {
        return await _context.UserJobs
            .Include(uj => uj.User)
            .Include(uj => uj.Job)
            .FirstOrDefaultAsync(uj => uj.UserId == userId && uj.JobId == jobId);
    }

    public async Task<IEnumerable<UserJob>> GetUserJobsByUserIdAndStatusAsync(Guid userId, UserJobStatus status,
        int pageNumber, int pageSize)
    {
        return await _context.UserJobs
            .Include(uj => uj.Job)
            .Where(uj => uj.UserId == userId && uj.Status == status)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetUserJobsCountByUserIdAndStatusAsync(Guid userId, UserJobStatus status)
    {
        return await _context.UserJobs
            .CountAsync(uj => uj.UserId == userId && uj.Status == status);
    }

    public async Task<IEnumerable<UserJob>> GetRecentUserJobsAsync(Guid userId, int count, UserJobStatus[] statuses)
    {
        return await _context.UserJobs
            .Include(uj => uj.User)
            .Include(uj => uj.Job)
            .Where(uj => uj.UserId == userId && statuses.Contains(uj.Status))
            .OrderByDescending(uj => uj.UpdatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Job>> GetJobsByUserIdAndStatusAsync(Guid userId, UserJobStatus status, int pageNumber,
        int pageSize)
    {
        var jobs = await _context.UserJobs
            .Where(uj => uj.UserId == userId && uj.Status == status)
            .Select(uj => uj.Job)
            .Where(j => j != null)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return jobs.Where(j => j != null).Cast<Job>();
    }

    public async Task<int> GetJobsCountByUserIdAndStatusAsync(Guid userId, UserJobStatus status)
    {
        return await _context.UserJobs
            .CountAsync(uj => uj.UserId == userId && uj.Status == status);
    }

    public async Task<Dictionary<UserJobStatus, int>> GetUserJobStatusCountsAsync(Guid userId)
    {
        var statusCounts = await _context.UserJobs
            .Where(uj => uj.UserId == userId)
            .GroupBy(uj => uj.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        // Ensure all statuses are included, even if count is 0
        foreach (UserJobStatus status in Enum.GetValues(typeof(UserJobStatus)))
            if (!statusCounts.ContainsKey(status))
                statusCounts[status] = 0;

        return statusCounts;
    }

    public async Task<int> GetTotalJobsCountAsync()
    {
        return await _context.Jobs.Where(j => j.IsActive == true).CountAsync();
    }

    public async Task<int> GetNewJobsCountAsync()
    {
        return await _context.Jobs.CountAsync(j => j.IsNew == true);
    }

    public async Task<IEnumerable<Job>> SearchJobsByTitleAsync(Guid userId, string searchTerm, UserJobStatus? status,
        int pageNumber, int pageSize)
    {
        var query = _context.UserJobs
            .Where(uj => uj.UserId == userId)
            .Include(uj => uj.Job)
            .Where(uj => uj.Job != null);

        if (status.HasValue) query = query.Where(uj => uj.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(uj => uj.Job != null && (
                (uj.Job.JobTitle != null && EF.Functions.ILike(uj.Job.JobTitle, $"%{searchTerm}%")) ||
                (uj.Job.BusinessName != null && EF.Functions.ILike(uj.Job.BusinessName, $"%{searchTerm}%")) ||
                (uj.Job.JobDescription != null && EF.Functions.ILike(uj.Job.JobDescription, $"%{searchTerm}%"))
            ));
        }

        var jobs = await query
            .Select(uj => uj.Job)
            .OrderByDescending(j => j != null ? j.CreatedAt : DateTime.MinValue)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return jobs.Where(j => j != null).Cast<Job>();
    }

    public async Task<int> CountJobsByTitleAsync(Guid userId, string searchTerm, UserJobStatus? status)
    {
        var query = _context.UserJobs
            .Where(uj => uj.UserId == userId)
            .Include(uj => uj.Job)
            .Where(uj => uj.Job != null);

        if (status.HasValue) query = query.Where(uj => uj.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(uj =>
                (uj.Job != null && uj.Job.JobTitle != null && EF.Functions.ILike(uj.Job.JobTitle, $"%{searchTerm}%")) ||
                (uj.Job != null && uj.Job.BusinessName != null &&
                 EF.Functions.ILike(uj.Job.BusinessName, $"%{searchTerm}%")) ||
                (uj.Job != null && uj.Job.JobDescription != null &&
                 EF.Functions.ILike(uj.Job.JobDescription, $"%{searchTerm}%")));
        }

        return await query.CountAsync();
    }

    public async Task<int> GetUserJobsCountInLastDaysAsync(Guid userId, int days)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        return await _context.UserJobs
            .Where(uj => uj.UserId == userId && uj.CreatedAt >= cutoffDate)
            .CountAsync();
    }

    public async Task<IEnumerable<(DateTime Date, int Count)>> GetDailyApplicationCountsAsync(Guid userId, int days)
    {
        var startDate = DateTime.UtcNow.Date.AddDays(-days + 1);

        var dailyCounts = await _context.UserJobs
            .Where(uj => uj.UserId == userId && uj.CreatedAt >= startDate)
            .GroupBy(uj => uj.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        // 确保所有日期都有数据，即使是0
        var result = new List<(DateTime Date, int Count)>();
        for (var date = startDate; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
        {
            var count = dailyCounts.FirstOrDefault(x => x.Date == date)?.Count ?? 0;
            result.Add((date, count));
        }

        return result.OrderBy(x => x.Date);
    }

    public async Task<CumulativeStatusCountDto> GetCumulativeStatusCountsAsync(Guid userId)
    {
        var result = new CumulativeStatusCountDto();

        // 获取每个状态的数量
        var appliedCount = await _context.UserJobs
            .CountAsync(uj => uj.UserId == userId && (
                uj.Status == UserJobStatus.Applied ||
                uj.Status == UserJobStatus.Reviewed ||
                uj.Status == UserJobStatus.Interviewing ||
                uj.Status == UserJobStatus.TechnicalAssessment ||
                uj.Status == UserJobStatus.Offered
            ));

        var reviewedCount = await _context.UserJobs
            .CountAsync(uj => uj.UserId == userId && (
                uj.Status == UserJobStatus.Reviewed ||
                uj.Status == UserJobStatus.Interviewing ||
                uj.Status == UserJobStatus.TechnicalAssessment ||
                uj.Status == UserJobStatus.Offered
            ));

        var interviewingCount = await _context.UserJobs
            .CountAsync(uj => uj.UserId == userId && (
                uj.Status == UserJobStatus.Interviewing ||
                uj.Status == UserJobStatus.TechnicalAssessment ||
                uj.Status == UserJobStatus.Offered
            ));

        var technicalCount = await _context.UserJobs
            .CountAsync(uj => uj.UserId == userId && (
                uj.Status == UserJobStatus.TechnicalAssessment ||
                uj.Status == UserJobStatus.Offered
            ));

        var offeredCount = await _context.UserJobs
            .CountAsync(uj => uj.UserId == userId &&
                              uj.Status == UserJobStatus.Offered
            );

        result.Applied = appliedCount;
        result.Reviewed = reviewedCount;
        result.Interviewing = interviewingCount;
        result.TechnicalAssessment = technicalCount;
        result.Offered = offeredCount;

        return result;
    }

    public async Task<IEnumerable<WorkTypeCountDto>> GetWorkTypeCountsAsync(Guid userId)
    {
        return await _context.UserJobs
            .Where(uj => uj.UserId == userId && uj.Job != null && uj.Job.IsActive == true)
            .GroupBy(uj => uj.Job!.WorkType ?? "Unknown")
            .Select(g => new WorkTypeCountDto
            {
                WorkType = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
    }

    public async Task<IEnumerable<SuburbCountDto>> GetSuburbCountsAsync(Guid userId)
    {
        return await _context.UserJobs
            .Where(uj => uj.UserId == userId &&
                         uj.Job != null &&
                         uj.Job.IsActive == true &&
                         !string.IsNullOrWhiteSpace(uj.Job.Suburb))
            .GroupBy(uj => uj.Job!.Suburb!)
            .Select(g => new SuburbCountDto
            {
                Suburb = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserJob>> GetAllUserJobsAsync()
    {
        return await _context.UserJobs
            .Include(uj => uj.User)
            .Include(uj => uj.Job)
            .ToListAsync();
    }
}