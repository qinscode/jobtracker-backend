namespace JobTracker.Models;

public class JobDto
{
    public int Id { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string WorkType { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string PayRange { get; set; } = string.Empty;
    public int MinSalary { get; set; } = 0;
    public int MaxSalary { get; set; } = 0;
    public string Suburb { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PostedDate { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
    public bool IsNew { get; set; } = false;
    public string[] TechStack { get; set; } = Array.Empty<string>();
}