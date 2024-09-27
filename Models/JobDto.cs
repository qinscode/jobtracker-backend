namespace JobTracker.Models;

public class JobDto
{
    public int Id { get; set; } // Changed from string to int
    public string JobTitle { get; set; }
    public string BusinessName { get; set; }
    public string WorkType { get; set; }
    public string JobType { get; set; }
    public string PayRange { get; set; }
    public string Suburb { get; set; }
    public string Area { get; set; }
    public string Url { get; set; }
    public string Status { get; set; }
    public string PostedDate { get; set; }
    public string JobDescription { get; set; }
}