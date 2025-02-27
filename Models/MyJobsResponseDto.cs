namespace JobTracker.Models;

public class MyJobsResponseDto
{
    public IEnumerable<MyJobDto> Jobs { get; set; } = new List<MyJobDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}