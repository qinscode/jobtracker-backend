namespace JobTracker.Models;

public class UpdateUserJobDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int JobId { get; set; }  // Changed from Guid to int
    public string Status { get; set; }
}