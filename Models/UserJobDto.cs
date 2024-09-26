namespace JobTracker.Models;

public class UserJobDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public int JobId { get; set; }  // Changed from Guid to int
    public string? JobTitle { get; set; }
    public UserJobStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}