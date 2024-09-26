namespace JobTracker.Models;

public class UserJobDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public Guid JobId { get; set; }
    public string? JobTitle { get; set; }
    public UserJobStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}