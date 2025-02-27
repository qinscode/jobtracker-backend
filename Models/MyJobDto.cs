namespace JobTracker.Models;

public class MyJobDto
{
    public int Id { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string PayRange { get; set; } = string.Empty;
    public string Suburb { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}