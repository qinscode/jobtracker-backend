using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JobTracker.Models;

public class UserEmailConfig
{
    [Key] public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [ForeignKey("UserId")] public User? User { get; set; }

    public string EmailAddress { get; set; }

    // 我们需要加密存储密码
    public string EncryptedPassword { get; set; }

    public string Provider { get; set; } // e.g., "Gmail", "Outlook"

    public DateTime LastSyncTime { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}