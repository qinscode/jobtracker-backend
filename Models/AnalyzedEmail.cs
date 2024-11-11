using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobTracker.Models;

public class AnalyzedEmail
{
    [Key]
    public Guid Id { get; set; }
    
    public Guid UserEmailConfigId { get; set; }
    [ForeignKey("UserEmailConfigId")]
    public UserEmailConfig? UserEmailConfig { get; set; }
    
    [Required]
    public string MessageId { get; set; }  // 邮件的唯一标识符
    
    public string Subject { get; set; }
    public DateTime ReceivedDate { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    public int MatchedJobId { get; set; }  // 如果匹配到了工作，记录下来
    [ForeignKey("MatchedJobId")]
    public Job? MatchedJob { get; set; }
} 