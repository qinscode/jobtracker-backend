namespace JobTracker.Models;

public class DailyJobStatistics
{
    public string Date { get; set; } = string.Empty;
    public int ActiveJobsCount { get; set; }
    public int NewJobsCount { get; set; }
}

public class JobStatisticsResponse
{
    public IEnumerable<DailyJobStatistics> DailyStatistics { get; set; } =
        Enumerable.Empty<DailyJobStatistics>();

    public int Days { get; set; }
}