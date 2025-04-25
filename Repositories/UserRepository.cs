using JobTracker.Data;
using JobTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Repositories;

public class UserRepository : IUserRepository
{
    private readonly JobTrackerContext _context;

    public UserRepository(JobTrackerContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _context.Users.ToListAsync();
    }

    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> GetUserByEmailAsync(string? email)
    {
        if (string.IsNullOrEmpty(email))
            return null;

        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> CreateUserAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task UpdateUserAsync(Guid id, User updatedUser)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) throw new KeyNotFoundException($"User with id {id} not found.");

        var blacklist = new HashSet<string> { "Id", "CreatedAt", "Password" };

        var properties = typeof(User).GetProperties();

        foreach (var property in properties)
        {
            if (blacklist.Contains(property.Name))
                continue;

            var updatedValue = property.GetValue(updatedUser);

            if (updatedValue != null) property.SetValue(user, updatedValue);
        }

        // Handle password update separately and securely
        if (!string.IsNullOrEmpty(updatedUser.Password)) user.SetPassword(updatedUser.Password);

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }
}