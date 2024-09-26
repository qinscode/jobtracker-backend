namespace JobTracker.Models;

public class CreateUserJobDto
{
    public Guid UserId { get; set; }
    public Guid JobId { get; set; }
    public UserJobStatus Status { get; set; } = UserJobStatus.New;
}