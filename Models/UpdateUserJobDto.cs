namespace JobTracker.Models;

public class UpdateUserJobDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid JobId { get; set; }
    public string Status { get; set; }
}