using JobTracker.Models;

namespace JobTracker.Services.Interfaces;

public interface IJobMatchingService
{
    Task<(bool IsMatch, Job? MatchedJob, double Similarity)> FindMatchingJobAsync(string jobTitle, string companyName);
    double CalculateSimilarity(string s1, string s2);
    Task<JobSearchResult> SearchJobsAsync(JobSearchParams searchParams);
}