using JobTracker.Models;

namespace JobTracker.Repositories;

public interface IUserJobRepository
{
    Task<IEnumerable<UserJob>> GetUserJobsAsync(int pageNumber, int pageSize);
    Task<int> GetUserJobsCountAsync();
    Task<UserJob?> GetUserJobByIdAsync(Guid id);
    Task<UserJob> CreateUserJobAsync(UserJob userJob);
    Task UpdateUserJobAsync(UserJob userJob);
    Task DeleteUserJobAsync(Guid id);
    Task<UserJob?> GetUserJobByUserIdAndJobIdAsync(Guid userId, int jobId); // Changed from Guid to int

    Task<IEnumerable<UserJob>> GetUserJobsByUserIdAndStatusAsync(Guid userId, UserJobStatus status, int pageNumber,
        int pageSize);

    Task<int> GetUserJobsCountByUserIdAndStatusAsync(Guid userId, UserJobStatus status);
    Task<IEnumerable<UserJob>> GetRecentUserJobsAsync(Guid userId, int count, UserJobStatus[] statuses);

    Task<IEnumerable<Job>> GetJobsByUserIdAndStatusAsync(Guid userId, UserJobStatus? status, int pageNumber,
        int pageSize);

    Task<int> GetJobsCountByUserIdAndStatusAsync(Guid userId, UserJobStatus? status);

    Task<Dictionary<UserJobStatus, int>> GetUserJobStatusCountsAsync(Guid userId);
    Task<int> GetTotalJobsCountAsync();
    Task<int> GetNewJobsCountAsync();

    Task<IEnumerable<Job>> SearchJobsByTitleAsync(Guid userId, string searchTerm, UserJobStatus? status,
        int pageNumber, int pageSize);

    Task<int> CountJobsByTitleAsync(Guid userId, string searchTerm, UserJobStatus? status);

    Task<int> GetUserJobsCountInLastDaysAsync(Guid userId, int days);

    Task<IEnumerable<(DateTime Date, int Count)>> GetDailyApplicationCountsAsync(Guid userId, int days);

    Task<CumulativeStatusCountDto> GetCumulativeStatusCountsAsync(Guid userId);

    Task<IEnumerable<WorkTypeCountDto>> GetWorkTypeCountsAsync(Guid userId);

    Task<IEnumerable<SuburbCountDto>> GetSuburbCountsAsync(Guid userId);

    Task<List<UserJob>> GetUserJobsByJobIdAsync(int jobId);

    Task<(IEnumerable<UserJob> Jobs, int TotalCount)> GetMyUserJobsByUserIdAsync(
        Guid userId,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 10,
        string? sortBy = null,
        bool sortDescending = true);
}