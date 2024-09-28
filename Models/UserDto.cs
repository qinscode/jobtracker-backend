namespace JobTracker.Models;

public class UserDto
{
    public string? Username { get; set; }
    public string? Email { get; set; }

    public static UserDto FromUser(User user)
    {
        return new UserDto
        {
            Username = user.Username,
            Email = user.Email
        };
    }
}