using JobTracker.Data;
using JobTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Repositories
{
    public class UserJobRepository : IUserJobRepository
    {
        private readonly JobTrackerContext _context;

        public UserJobRepository(JobTrackerContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<UserJob>> GetAllUserJobsAsync()
        {
            return await _context.UserJobs
                .Include(uj => uj.User)
                .Include(uj => uj.Job)
                .ToListAsync();
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
    }
}