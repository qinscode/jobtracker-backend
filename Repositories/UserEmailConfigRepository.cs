using JobTracker.Data;
using JobTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Repositories;

public class UserEmailConfigRepository : IUserEmailConfigRepository
{
    private readonly JobTrackerContext _context;

    public UserEmailConfigRepository(JobTrackerContext context)
    {
        _context = context;
    }

    public async Task<UserEmailConfig> CreateAsync(UserEmailConfig config)
    {
        _context.UserEmailConfigs.Add(config);
        await _context.SaveChangesAsync();
        return config;
    }

    public async Task<IEnumerable<UserEmailConfig>> GetByUserIdAsync(Guid userId)
    {
        return await _context.UserEmailConfigs
            .Where(c => c.UserId == userId && c.IsActive)
            .ToListAsync();
    }

    public async Task UpdateAsync(UserEmailConfig config)
    {
        _context.Entry(config).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var config = await _context.UserEmailConfigs.FindAsync(id);
        if (config != null)
        {
            config.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<UserEmailConfig?> GetByIdAsync(Guid id)
    {
        return await _context.UserEmailConfigs
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
    }

    public async Task<IEnumerable<UserEmailConfig>> GetAllActiveConfigsAsync()
    {
        return await _context.UserEmailConfigs
            .Where(c => c.IsActive)
            .ToListAsync();
    }
} 