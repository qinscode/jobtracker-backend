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
    }
}