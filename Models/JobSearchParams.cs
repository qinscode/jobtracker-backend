namespace JobTracker.Models;

public class JobSearchParams
{
    public string? SearchTerm { get; set; }
    public string? JobTitle { get; set; }
    public string? CompanyName { get; set; }
    public bool? IsActive { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}

public class JobSearchResult
{
    public IEnumerable<Job> Jobs { get; set; } = new List<Job>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}