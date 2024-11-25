namespace JobTracker.Models;

public class EmailAnalysisDto
{
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public bool IsRecognized { get; set; }
    public JobBasicInfo? Job { get; set; }
    public UserJobStatus Status { get; set; }
    public List<string> KeyPhrases { get; set; } = new();
    public string? SuggestedAction { get; set; }
}

public class JobBasicInfo
{
    public int Id { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
}