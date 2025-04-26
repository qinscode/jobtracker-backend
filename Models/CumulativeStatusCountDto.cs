namespace JobTracker.Models;

public class CumulativeStatusCountDto
{
    public int Applied { get; set; }
    public int Reviewed { get; set; }
    public int Interviewing { get; set; }
    public int TechnicalAssessment { get; set; }
    public int Offered { get; set; }

    public int Rejected { get; set; }
}