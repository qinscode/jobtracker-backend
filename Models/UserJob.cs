using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobTracker.Models;

public class UserJob
{
    [Key] public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [ForeignKey("UserId")] public User? User { get; set; }

    public Guid JobId { get; set; }

    [ForeignKey("JobId")] public Job? Job { get; set; }

    public UserJobStatus Status { get; set; } = UserJobStatus.New;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum UserJobStatus
{
    New,
    Pending,
    Archived,
    Reviewed,
    Ghosting,
    Applied,
    Interviewing,
    TechnicalAssessment,
    Offered,
    Rejected
}