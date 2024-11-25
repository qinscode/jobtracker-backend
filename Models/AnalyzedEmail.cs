using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobTracker.Models;

public class AnalyzedEmail
{
    [Key] public Guid Id { get; set; }

    public Guid UserEmailConfigId { get; set; }
    [ForeignKey("UserEmailConfigId")] public UserEmailConfig? UserEmailConfig { get; set; }

    [Required] public string MessageId { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    [Required] public int MatchedJobId { get; set; }
    [ForeignKey("MatchedJobId")] public Job? MatchedJob { get; set; }

    public uint? Uid { get; set; }
}