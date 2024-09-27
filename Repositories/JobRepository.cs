using JobTracker.Data;
using JobTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Repositories;

public class JobRepository : IJobRepository
{
    private readonly JobTrackerContext _context;

    public JobRepository(JobTrackerContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Job>> GetJobsAsync(int pageNumber, int pageSize)
    {
        return await _context.Jobs
            .OrderByDescending(j => j.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetJobsCountAsync()
    {
        return await _context.Jobs.CountAsync();
    }

    public async Task<IEnumerable<Job>> GetActiveJobsAsync(int pageNumber, int pageSize)
    {
        return await _context.Jobs
            .Where(j => j.IsActive == true)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetActiveJobsCountAsync()
    {
        return await _context.Jobs
            .Where(j => j.IsActive == true)
            .CountAsync();
    }

    public async Task<Job?> GetJobByIdAsync(int id)
    {
        return await _context.Jobs
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task<Job> CreateJobAsync(Job job)
    {
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task UpdateJobAsync(Job job)
    {
        _context.Entry(job).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteJobAsync(int id)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job != null)
        {
            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Job>> GetNewJobsAsync(int pageNumber, int pageSize)
    {
        return await _context.Jobs
            .Where(j => j.IsNew == true)
            .Where(j => j.IsActive == true)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetNewJobsCountAsync()
    {
        return await _context.Jobs
            .Where(j => j.IsNew == true)
            .CountAsync();
    }

    public async Task<IEnumerable<Job>> SearchJobsByTitleAsync(string searchTerm, int pageNumber, int pageSize)
    {
        return await _context.Jobs
            .Where(j => EF.Functions.ILike(j.JobTitle, $"%{searchTerm}%"))
            .Where(j => j.IsActive == true)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountJobsByTitleAsync(string searchTerm)
    {
        return await _context.Jobs
            .Where(j => EF.Functions.ILike(j.JobTitle, $"%{searchTerm}%"))
            .Where(j => j.IsActive == true)
            .CountAsync();
    }
}