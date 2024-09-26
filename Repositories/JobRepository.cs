using JobTracker.Data;
using JobTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Repositories
{
    public class JobRepository : IJobRepository
    {
        private readonly JobTrackerContext _context;

        public JobRepository(JobTrackerContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Job>> GetAllJobsAsync()
        {
            return await _context.Jobs.Include(j => j.Advertiser).ToListAsync();
        }

        public async Task<Job?> GetJobByIdAsync(Guid id)
        {
            return await _context.Jobs
                .Include(j => j.Advertiser)
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

        public async Task DeleteJobAsync(Guid id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job != null)
            {
                _context.Jobs.Remove(job);
                await _context.SaveChangesAsync();
            }
        }
    }
}