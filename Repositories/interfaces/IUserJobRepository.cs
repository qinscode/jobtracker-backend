using JobTracker.Models;

namespace JobTracker.Repositories
{
    public interface IUserJobRepository
    {
        Task<IEnumerable<UserJob>> GetUserJobsAsync(int pageNumber, int pageSize);
        Task<int> GetUserJobsCountAsync();
        Task<UserJob?> GetUserJobByIdAsync(Guid id);
        Task<UserJob> CreateUserJobAsync(UserJob userJob);
        Task UpdateUserJobAsync(UserJob userJob);
        Task DeleteUserJobAsync(Guid id);
        Task<UserJob?> GetUserJobByUserIdAndJobIdAsync(Guid userId, int jobId);  // Changed from Guid to int
        Task<IEnumerable<UserJob>> GetUserJobsByUserIdAndStatusAsync(Guid userId, UserJobStatus status, int pageNumber, int pageSize);
        Task<int> GetUserJobsCountByUserIdAndStatusAsync(Guid userId, UserJobStatus status);
        Task<IEnumerable<UserJob>> GetRecentUserJobsAsync(Guid userId, int count, UserJobStatus[] statuses);

        Task<IEnumerable<Job>> GetJobsByUserIdAndStatusAsync(Guid userId, UserJobStatus status, int pageNumber, int pageSize);
        Task<int> GetJobsCountByUserIdAndStatusAsync(Guid userId, UserJobStatus status);

        Task<Dictionary<UserJobStatus, int>> GetUserJobStatusCountsAsync(Guid userId);
        Task<int> GetTotalJobsCountAsync();
    }
}