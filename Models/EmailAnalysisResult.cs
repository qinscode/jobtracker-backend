namespace JobTracker.Models;

public class EmailAnalysisResult
{
    public string Subject { get; set; }
    public DateTime ReceivedDate { get; set; }
    public Job? Job { get; set; }
    public bool IsRecognized { get; set; }
    public string RawContent { get; set; }
} 