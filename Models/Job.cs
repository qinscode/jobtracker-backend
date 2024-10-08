using System.ComponentModel.DataAnnotations;

namespace JobTracker.Models;

public class Job
{
    [Key] public int Id { get; set; } // Changed from Guid to int

    public string? JobTitle { get; set; }
    public string? BusinessName { get; set; }
    public string? WorkType { get; set; }
    public string? JobType { get; set; }
    public string? PayRange { get; set; }
    public string? Suburb { get; set; }
    public string? Area { get; set; }
    public string? Url { get; set; }
    public DateTime? PostedDate { get; set; }
    public string? JobDescription { get; set; }
    public int? AdvertiserId { get; set; } // Changed from Guid to int and made nullable
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool? IsNew { get; set; }

    public bool? IsActive { get; set; } = true;
}