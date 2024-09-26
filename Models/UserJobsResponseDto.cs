namespace JobTracker.Models;

public class UserJobsResponseDto
{
    public IEnumerable<UserJobDto> UserJobs { get; set; }
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}