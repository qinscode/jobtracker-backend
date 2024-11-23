namespace JobTracker.Repositories;

public interface IUserEmailConfigRepository
{
    Task<UserEmailConfig> CreateAsync(UserEmailConfig config);
    Task<IEnumerable<UserEmailConfig>> GetByUserIdAsync(Guid userId);
    Task UpdateAsync(UserEmailConfig config);
    Task DeleteAsync(Guid id);
    Task<UserEmailConfig?> GetByIdAsync(Guid id);
    Task<IEnumerable<UserEmailConfig>> GetAllActiveConfigsAsync();
}