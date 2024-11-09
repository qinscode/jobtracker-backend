using JobTracker.Models;

namespace JobTracker.Repositories;

public interface IJobRepository
{
    Task<IEnumerable<Job>> GetJobsAsync(int pageNumber, int pageSize);
    Task<int> GetJobsCountAsync();
    Task<IEnumerable<Job>> GetActiveJobsAsync(int pageNumber, int pageSize);
    Task<int> GetActiveJobsCountAsync();
    Task<Job?> GetJobByIdAsync(int id);
    Task<Job> CreateJobAsync(Job job);
    Task UpdateJobAsync(Job job);
    Task DeleteJobAsync(int id);
    Task<IEnumerable<Job>> GetNewJobsAsync(int pageNumber, int pageSize);
    Task<int> GetNewJobsCountAsync();
    Task<IEnumerable<Job>> SearchJobsByTitleAsync(string searchTerm, int pageNumber, int pageSize);
    Task<int> CountJobsByTitleAsync(string searchTerm);
    Task<IEnumerable<Job>> SearchJobsByTitleAndCompanyAsync(string jobTitle, string companyName, int pageNumber, int pageSize);
}