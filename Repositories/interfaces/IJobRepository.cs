using JobTracker.Models;

namespace JobTracker.Repositories;

public interface IJobRepository
{
    Task<IEnumerable<Job>> GetJobsAsync(int pageNumber, int pageSize);
    Task<int> GetJobsCountAsync();
    Task<Job?> GetJobByIdAsync(int id);  // Changed from Guid to int
    Task<Job> CreateJobAsync(Job job);
    Task UpdateJobAsync(Job job);
    Task DeleteJobAsync(int id);  // Changed from Guid to int
    Task<IEnumerable<Job>> GetNewJobsAsync(int pageNumber, int pageSize);
    Task<int> GetNewJobsCountAsync();
}