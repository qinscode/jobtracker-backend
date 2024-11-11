using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JobTracker.Models;

public class UserEmailConfig
{
    [Key] public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [ForeignKey("UserId")] public User? User { get; set; }

    [Required] public string EmailAddress { get; set; } = string.Empty;

    [Required] public string EncryptedPassword { get; set; } = string.Empty;

    [Required] public string Provider { get; set; } = "Gmail";

    public DateTime LastSyncTime { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}