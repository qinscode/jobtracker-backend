namespace JobTracker.Models;

public class JobsResponseDto
{
    public IEnumerable<JobDto> Jobs { get; set; }
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}