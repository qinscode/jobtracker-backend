namespace JobTracker.Models;

public class EmailAnalysisDto
{
    public string Subject { get; set; }
    public DateTime ReceivedDate { get; set; }
    public JobBasicInfo? Job { get; set; }
    public bool IsRecognized { get; set; }
}

public class JobBasicInfo
{
    public int Id { get; set; }
    public string JobTitle { get; set; }
    public string BusinessName { get; set; }
} 