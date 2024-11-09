using JobTracker.Models;
using JobTracker.Repositories;
using JobTracker.Services.Interfaces;

namespace JobTracker.Services;

public class JobMatchingService : IJobMatchingService
{
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<JobMatchingService> _logger;
    private const double MinimumSimilarity = 0.7;

    public JobMatchingService(
        IJobRepository jobRepository,
        ILogger<JobMatchingService> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task<(bool IsMatch, Job? MatchedJob, double Similarity)> FindMatchingJobAsync(string jobTitle, string companyName)
    {
        var matchingJobs = await _jobRepository.SearchJobsByTitleAndCompanyAsync(jobTitle, companyName, 1, 10);
        
        foreach (var job in matchingJobs)
        {
            var companySimilarity = CalculateSimilarity(
                job.BusinessName?.ToLower() ?? "", 
                companyName.ToLower());
                
            // 输出匹配详情
            Console.WriteLine($"\nComparing with database job:");
            Console.WriteLine($"Database: {job.BusinessName} - {job.JobTitle}");
            Console.WriteLine($"Email: {companyName} - {jobTitle}");
            Console.WriteLine($"Company Similarity: {companySimilarity:P}");

            if (companySimilarity >= MinimumSimilarity)
            {
                return (true, job, companySimilarity);
            }
        }

        return (false, null, 0);
    }

    public double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0;
        
        var distance = ComputeLevenshteinDistance(s1, s2);
        var maxLength = Math.Max(s1.Length, s2.Length);
        return 1 - ((double)distance / maxLength);
    }

    private int ComputeLevenshteinDistance(string s1, string s2)
    {
        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (var i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;

        for (var j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (var i = 1; i <= s1.Length; i++)
        for (var j = 1; j <= s2.Length; j++)
        {
            var cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
            matrix[i, j] = Math.Min(
                Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                matrix[i - 1, j - 1] + cost);
        }

        return matrix[s1.Length, s2.Length];
    }
} 