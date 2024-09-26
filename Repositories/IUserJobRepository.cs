using JobTracker.Models;

namespace JobTracker.Repositories
{
    public interface IUserJobRepository
    {
        Task<IEnumerable<UserJob>> GetAllUserJobsAsync();
        Task<UserJob?> GetUserJobByIdAsync(Guid id);
        Task<UserJob> CreateUserJobAsync(UserJob userJob);
        Task UpdateUserJobAsync(UserJob userJob);
        Task DeleteUserJobAsync(Guid id);
    }
}