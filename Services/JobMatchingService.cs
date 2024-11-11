using FuzzySharp;
using JobTracker.Models;
using JobTracker.Repositories;
using JobTracker.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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

    public async Task<(bool IsMatch, Job? MatchedJob, double Similarity)> FindMatchingJobAsync(string jobTitle,
        string companyName)
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
            Console.WriteLine(
                $"- Partial Ratio: {Fuzz.PartialRatio(job.BusinessName?.ToLower() ?? "", companyName.ToLower())}");
            Console.WriteLine(
                $"- Token Sort Ratio: {Fuzz.TokenSortRatio(job.BusinessName?.ToLower() ?? "", companyName.ToLower())}");
            Console.WriteLine(
                $"- Token Set Ratio: {Fuzz.TokenSetRatio(job.BusinessName?.ToLower() ?? "", companyName.ToLower())}");
            Console.WriteLine($"Final Company Score: {companyScore:F1}");
            Console.WriteLine($"Title Score Details:");
            Console.WriteLine($"- Ratio: {Fuzz.Ratio(job.JobTitle?.ToLower() ?? "", jobTitle.ToLower())}");
            Console.WriteLine(
                $"- Partial Ratio: {Fuzz.PartialRatio(job.JobTitle?.ToLower() ?? "", jobTitle.ToLower())}");
            Console.WriteLine(
                $"- Token Sort Ratio: {Fuzz.TokenSortRatio(job.JobTitle?.ToLower() ?? "", jobTitle.ToLower())}");
            Console.WriteLine(
                $"- Token Set Ratio: {Fuzz.TokenSetRatio(job.JobTitle?.ToLower() ?? "", jobTitle.ToLower())}");
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

    public async Task<JobSearchResult> SearchJobsAsync(JobSearchParams searchParams)
    {
        var query = _jobRepository.GetQueryable();

        // 应用搜索条件
        if (!string.IsNullOrWhiteSpace(searchParams.SearchTerm))
        {
            var searchTerm = searchParams.SearchTerm.ToLower();
            query = query.Where(j =>
                (j.JobTitle != null && EF.Functions.ILike(j.JobTitle, $"%{searchTerm}%")) ||
                (j.BusinessName != null && EF.Functions.ILike(j.BusinessName, $"%{searchTerm}%")));
        }

        // 如果同时提供了具体的职位名称和公司名称，使用 AND 条件
        if (!string.IsNullOrWhiteSpace(searchParams.JobTitle) && !string.IsNullOrWhiteSpace(searchParams.CompanyName))
        {
            query = query.Where(j =>
                (j.JobTitle != null && EF.Functions.ILike(j.JobTitle, $"%{searchParams.JobTitle}%")) &&
                (j.BusinessName != null && EF.Functions.ILike(j.BusinessName, $"%{searchParams.CompanyName}%")));
        }
        // 否则分别应用职位名称和公司名称的搜索条件
        else
        {
            if (!string.IsNullOrWhiteSpace(searchParams.JobTitle))
            {
                query = query.Where(j => j.JobTitle != null &&
                                         EF.Functions.ILike(j.JobTitle, $"%{searchParams.JobTitle}%"));
            }

            if (!string.IsNullOrWhiteSpace(searchParams.CompanyName))
            {
                query = query.Where(j => j.BusinessName != null &&
                                         EF.Functions.ILike(j.BusinessName, $"%{searchParams.CompanyName}%"));
            }
        }

        if (searchParams.IsActive.HasValue)
        {
            query = query.Where(j => j.IsActive == searchParams.IsActive.Value);
        }

        // 计算总数
        var totalCount = await query.CountAsync();

        // 应用排序
        if (!string.IsNullOrWhiteSpace(searchParams.SortBy))
        {
            query = searchParams.SortBy.ToLower() switch
            {
                "title" => searchParams.SortDescending
                    ? query.OrderByDescending(j => j.JobTitle)
                    : query.OrderBy(j => j.JobTitle),
                "company" => searchParams.SortDescending
                    ? query.OrderByDescending(j => j.BusinessName)
                    : query.OrderBy(j => j.BusinessName),
                "date" => searchParams.SortDescending
                    ? query.OrderByDescending(j => j.CreatedAt)
                    : query.OrderBy(j => j.CreatedAt),
                _ => searchParams.SortDescending
                    ? query.OrderByDescending(j => j.CreatedAt)
                    : query.OrderBy(j => j.CreatedAt)
            };
        }
        else
        {
            query = searchParams.SortDescending
                ? query.OrderByDescending(j => j.CreatedAt)
                : query.OrderBy(j => j.CreatedAt);
        }

        // 输出生成的SQL查询（用于调试）
        var sql = query.ToQueryString();
        Console.WriteLine($"\nGenerated SQL Query:\n{sql}\n");

        // 应用分页
        var jobs = await query
            .Skip((searchParams.PageNumber - 1) * searchParams.PageSize)
            .Take(searchParams.PageSize)
            .ToListAsync();

        // 输出搜索结果
        Console.WriteLine($"\nFound {jobs.Count} matches:");
        foreach (var job in jobs)
        {
            Console.WriteLine($"- {job.BusinessName}: {job.JobTitle}");
        }

        return new JobSearchResult
        {
            Jobs = jobs,
            TotalCount = totalCount,
            PageNumber = searchParams.PageNumber,
            PageSize = searchParams.PageSize
        };
    }
}