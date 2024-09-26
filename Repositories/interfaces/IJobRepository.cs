using JobTracker.Models;

namespace JobTracker.Repositories;

public interface IJobRepository
{
    Task<IEnumerable<Job>> GetJobsAsync(int pageNumber, int pageSize);
    Task<int> GetJobsCountAsync();
    Task<Job?> GetJobByIdAsync(Guid id);
    Task<Job> CreateJobAsync(Job job);
    Task UpdateJobAsync(Job job);
    Task DeleteJobAsync(Guid id);
}