namespace JobTracker.Models;

public class JobDto
{
    public Guid Id { get; set; }
    public string? JobTitle { get; set; }
    public string? BusinessName { get; set; }
    public string? WorkType { get; set; }
    public string? JobType { get; set; }
    public string? PayRange { get; set; }
    public string? Suburb { get; set; }
    public string? Area { get; set; }
    public string? Url { get; set; }
    public DateTime? PostedDate { get; set; }
    public string? AdvertiserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}