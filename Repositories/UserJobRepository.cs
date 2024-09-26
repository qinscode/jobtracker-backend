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

    public async Task<UserJob> GetUserJobByIdAsync(Guid id)
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

    public async Task<UserJob?> GetUserJobByUserIdAndJobIdAsync(Guid userId, Guid jobId)
    {
        return await _context.UserJobs
            .FirstOrDefaultAsync(uj => uj.UserId == userId && uj.JobId == jobId);
    }

    public async Task<IEnumerable<UserJob>> GetUserJobsByUserIdAndStatusAsync(Guid userId, UserJobStatus status, int pageNumber, int pageSize)
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

    public async Task<IEnumerable<UserJob>> GetAllUserJobsAsync()
    {
        return await _context.UserJobs
            .Include(uj => uj.User)
            .Include(uj => uj.Job)
            .ToListAsync();
    }

    public async Task<IEnumerable<Job>> GetJobsByUserIdAndStatusAsync(Guid userId, UserJobStatus status, int pageNumber, int pageSize)
    {
        return await _context.UserJobs
            .Where(uj => uj.UserId == userId && uj.Status == status)
            .Select(uj => uj.Job)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetJobsCountByUserIdAndStatusAsync(Guid userId, UserJobStatus status)
    {
        return await _context.UserJobs
            .CountAsync(uj => uj.UserId == userId && uj.Status == status);
    }
}