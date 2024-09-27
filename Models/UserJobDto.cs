namespace JobTracker.Models;

public class UserJobDto
{

    public int JobId { get; set; } // Changed from Guid to int
    public string? JobTitle { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}