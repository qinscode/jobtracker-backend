namespace JobTracker.Models;

public class UserJobDto
{
    public Guid Id { get; set; }
    public int JobId { get; set; }
    public string? JobTitle { get; set; }
    public string? BusinessName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateUserJobDto
{
    public int JobId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class UpdateUserJobDto
{
    public int JobId { get; set; }
    public UserJobStatus Status { get; set; }
}

public class UserJobsResponseDto
{
    public IEnumerable<UserJobDto> UserJobs { get; set; } = new List<UserJobDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class UserJobStatusCountDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class UserJobStatusCountResponse
{
    public List<UserJobStatusCountDto> StatusCounts { get; set; } = new List<UserJobStatusCountDto>();
    public int TotalJobsCount { get; set; }
    public int NewJobsCount { get; set; }
}