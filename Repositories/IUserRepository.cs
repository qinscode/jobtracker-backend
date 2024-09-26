using JobTracker.Models;

namespace JobTracker.Repositories
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(Guid id);
        Task<User> CreateUserAsync(User user);
        Task UpdateUserAsync(Guid id, User user);
        Task DeleteUserAsync(Guid id);
    }
}