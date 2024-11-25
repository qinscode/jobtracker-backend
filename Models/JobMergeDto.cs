namespace JobTracker.Models;

public class JobMatchDto
{
    public JobBasicInfo Job { get; set; } = null!;
    public double Similarity { get; set; }
}

public class MergeJobsRequest
{
    public int SourceJobId { get; set; }
    public int TargetJobId { get; set; }
}

public class MergeResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}