using FuzzySharp;
using FuzzySharp.SimilarityRatio;
using FuzzySharp.PreProcess;
using JobTracker.Models;
using JobTracker.Repositories;
using JobTracker.Services.Interfaces;

namespace JobTracker.Services;

public class JobMatchingService : IJobMatchingService
{
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<JobMatchingService> _logger;
    private const double MinimumSimilarity = 70; // FuzzySharp 返回 0-100 的分数

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
        
        Console.WriteLine("\n========== Job Matching Analysis ==========");
        Console.WriteLine($"Searching for: {companyName} - {jobTitle}");
        Console.WriteLine($"Found {matchingJobs.Count()} potential matches");
        
        var bestMatch = (Job?)null;
        var bestScore = 0.0;
        
        foreach (var job in matchingJobs)
        {
            // 计算公司名称相似度
            var companyScore = CalculateCompanyScore(job.BusinessName, companyName);
            
            // 计算职位名称相似度
            var titleScore = CalculateTitleScore(job.JobTitle, jobTitle);
            
            // 综合得分
            var combinedScore = (companyScore + titleScore) / 2.0;
                
            Console.WriteLine($"\nComparing with database job (ID: {job.Id}):");
            Console.WriteLine($"Database: {job.BusinessName} - {job.JobTitle}");
            Console.WriteLine($"Email: {companyName} - {jobTitle}");
            Console.WriteLine($"Company Score Details:");
            Console.WriteLine($"- Ratio: {Fuzz.Ratio(job.BusinessName?.ToLower() ?? "", companyName.ToLower())}");
            Console.WriteLine($"- Partial Ratio: {Fuzz.PartialRatio(job.BusinessName?.ToLower() ?? "", companyName.ToLower())}");
            Console.WriteLine($"- Token Sort Ratio: {Fuzz.TokenSortRatio(job.BusinessName?.ToLower() ?? "", companyName.ToLower())}");
            Console.WriteLine($"- Token Set Ratio: {Fuzz.TokenSetRatio(job.BusinessName?.ToLower() ?? "", companyName.ToLower())}");
            Console.WriteLine($"Final Company Score: {companyScore:F1}");
            Console.WriteLine($"Title Score Details:");
            Console.WriteLine($"- Ratio: {Fuzz.Ratio(job.JobTitle?.ToLower() ?? "", jobTitle.ToLower())}");
            Console.WriteLine($"- Partial Ratio: {Fuzz.PartialRatio(job.JobTitle?.ToLower() ?? "", jobTitle.ToLower())}");
            Console.WriteLine($"- Token Sort Ratio: {Fuzz.TokenSortRatio(job.JobTitle?.ToLower() ?? "", jobTitle.ToLower())}");
            Console.WriteLine($"- Token Set Ratio: {Fuzz.TokenSetRatio(job.JobTitle?.ToLower() ?? "", jobTitle.ToLower())}");
            Console.WriteLine($"Final Title Score: {titleScore:F1}");
            Console.WriteLine($"Combined Score: {combinedScore:F1}");

            if (companyScore >= MinimumSimilarity && combinedScore > bestScore)
            {
                bestMatch = job;
                bestScore = combinedScore;
            }
        }

        var isMatch = bestMatch != null;
        Console.WriteLine($"\nFinal Result: {(isMatch ? "MATCH FOUND" : "NO MATCH")}");
        if (isMatch)
        {
            Console.WriteLine($"Best Match ID: {bestMatch!.Id}");
            Console.WriteLine($"Best Match: {bestMatch.BusinessName} - {bestMatch.JobTitle}");
            Console.WriteLine($"Overall Score: {bestScore:F1}");
        }
        Console.WriteLine("=========================================\n");

        return (isMatch, bestMatch, bestScore / 100.0); // 转换为 0-1 范围
    }

    private double CalculateCompanyScore(string? company1, string company2)
    {
        if (string.IsNullOrEmpty(company1)) return 0;

        // 预处理
        company1 = company1.ToLower();
        company2 = company2.ToLower();

        // 移除常见后缀
        var commonTerms = new[]
        {
            "ltd", "limited", "pty", "proprietary",
            "inc", "incorporated", "corp", "corporation",
            "group", "holdings", "international", "aust",
            "australia", "asia", "pacific", "the",
            "services", "solutions", "technologies", "technology"
        };

        foreach (var term in commonTerms)
        {
            company1 = company1.Replace($" {term}", "").Replace($"{term} ", "");
            company2 = company2.Replace($" {term}", "").Replace($"{term} ", "");
        }

        // 计算不同类型的相似度
        var ratio = Fuzz.Ratio(company1, company2);
        var partialRatio = Fuzz.PartialRatio(company1, company2);
        var tokenSetRatio = Fuzz.TokenSetRatio(company1, company2);
        var tokenSortRatio = Fuzz.TokenSortRatio(company1, company2);

        // 如果有完全匹配的部分，给予更高的权重
        if (partialRatio > 95) return partialRatio;

        // 加权平均，给予 token 相关的比较更高的权重
        return (ratio + partialRatio * 2 + tokenSetRatio * 3 + tokenSortRatio * 2) / 8.0;
    }

    private double CalculateTitleScore(string? title1, string title2)
    {
        if (string.IsNullOrEmpty(title1)) return 0;

        // 预处理
        title1 = title1.ToLower();
        title2 = title2.ToLower();

        // 移除常见词
        var commonTerms = new[]
        {
            "senior", "junior", "lead", "principal",
            "manager", "specialist", "analyst", "consultant",
            "applications", "application", "technical", "developer",
            "engineer", "level", "grade", "i", "ii", "iii", "iv", "v"
        };

        foreach (var term in commonTerms)
        {
            title1 = title1.Replace($" {term}", "").Replace($"{term} ", "");
            title2 = title2.Replace($" {term}", "").Replace($"{term} ", "");
        }

        // 计算不同类型的相似度
        var ratio = Fuzz.Ratio(title1, title2);
        var partialRatio = Fuzz.PartialRatio(title1, title2);
        var tokenSetRatio = Fuzz.TokenSetRatio(title1, title2);
        var tokenSortRatio = Fuzz.TokenSortRatio(title1, title2);

        // 如果有完全匹配的部分，给予更高的权重
        if (partialRatio > 95) return partialRatio;

        // 加权平均，给予 token 相关的比较更高的权重
        return (ratio + partialRatio * 2 + tokenSetRatio * 3 + tokenSortRatio * 2) / 8.0;
    }

    public double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0;
        
        // 使用 FuzzySharp 的 TokenSetRatio，它对词序不敏感
        return Fuzz.TokenSetRatio(s1.ToLower(), s2.ToLower()) / 100.0;
    }
} 