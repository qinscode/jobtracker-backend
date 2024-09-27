namespace JobTracker.Models;

public class UpdateUserJobDto
{
    public int JobId { get; set; }
    public UserJobStatus Status { get; set; }
}