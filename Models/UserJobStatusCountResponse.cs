namespace JobTracker.Models;

public class UserJobStatusCountResponse
{
    public List<UserJobStatusCountDto> StatusCounts { get; set; }
    public int TotalJobsCount { get; set; }
    public int NewJobsCount { get; set; }
}