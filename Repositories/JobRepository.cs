using JobTracker.Data;
using JobTracker.Models;
using JobTracker.Utilities;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Repositories;

public class JobRepository : IJobRepository
{
    private readonly JobTrackerContext _context;

    public JobRepository(JobTrackerContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Job>> GetJobsAsync(int pageNumber, int pageSize)
    {
        return await _context.Jobs
            .Where(j => j.IsActive == true)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetJobsCountAsync()
    {
        return await _context.Jobs.Where(j => j.IsActive == true).CountAsync();
    }

    public async Task<IEnumerable<Job>> GetActiveJobsAsync(int pageNumber, int pageSize)
    {
        return await _context.Jobs
            .Where(j => j.IsActive == true)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetActiveJobsCountAsync()
    {
        return await _context.Jobs
            .Where(j => j.IsActive == true)
            .CountAsync();
    }

    public async Task<Job?> GetJobByIdAsync(int id)
    {
        return await _context.Jobs
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task<Job> CreateJobAsync(Job job)
    {
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task UpdateJobAsync(Job job)
    {
        _context.Entry(job).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteJobAsync(int id)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job != null)
        {
            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Job>> GetNewJobsAsync(int pageNumber, int pageSize)
    {
        return await _context.Jobs
            .Where(j => j.IsNew == true && j.IsActive == true)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetNewJobsCountAsync()
    {
        return await _context.Jobs
            .Where(j => j.IsNew == true && j.IsActive == true)
            .CountAsync();
    }

    public async Task<IEnumerable<Job>> SearchJobsByTitleAsync(string searchTerm, int pageNumber, int pageSize)
    {
        return await _context.Jobs
            .Where(j => j.JobTitle != null && EF.Functions.ILike(j.JobTitle, $"%{searchTerm}%"))
            .Where(j => j.IsActive == true)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountJobsByTitleAsync(string searchTerm)
    {
        return await _context.Jobs
            .Where(j => j.JobTitle != null && EF.Functions.ILike(j.JobTitle, $"%{searchTerm}%"))
            .Where(j => j.IsActive == true)
            .CountAsync();
    }

    public async Task<IEnumerable<Job>> SearchJobsByTitleAndCompanyAsync(string jobTitle, string companyName,
        int pageNumber, int pageSize)
    {
        // 清理搜索词
        jobTitle = jobTitle.Trim().ToLower();
        companyName = companyName.Trim().ToLower();

        // 移除常见的公司后缀和前缀
        var commonTerms = new[]
        {
            "ltd", "limited", "pty", "proprietary",
            "inc", "incorporated", "corp", "corporation",
            "group", "holdings", "international", "aust",
            "australia", "asia", "pacific", "the"
        };

        // 清理公司名称
        var cleanCompanyName = companyName;
        foreach (var term in commonTerms)
        {
            cleanCompanyName = cleanCompanyName.Replace($" {term} ", " ")
                .Replace($" {term}", "")
                .Replace($"{term} ", "");
        }

        cleanCompanyName = cleanCompanyName.Trim();

        // 分词并移除短词
        var companyKeywords = cleanCompanyName
            .Split(new[] { ' ', '-', '/', '&' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(k => k.Length > 2) // 忽略太短的词
            .ToList();

        Console.WriteLine($"Clean company name: {cleanCompanyName}");
        Console.WriteLine($"Company keywords: {string.Join(", ", companyKeywords)}");
        Console.WriteLine($"Job title: {jobTitle}");

        // 构建查询 - 先精确匹配职位
        var query = _context.Jobs
            .Where(j => j.JobTitle != null && EF.Functions.ILike(j.JobTitle, $"%{jobTitle}%"))
            .AsQueryable();

        // 然后在职位匹配的结果中搜索公司
        if (companyKeywords.Any())
        {
            var companyPredicate = PredicateBuilder.New<Job>();
            foreach (var keyword in companyKeywords.Where(k => !string.IsNullOrEmpty(k)))
            {
                companyPredicate = companyPredicate.Or(j =>
                    j.BusinessName != null && EF.Functions.ILike(j.BusinessName, $"%{keyword}%"));
            }

            query = query.Where(companyPredicate);
        }

        // 输出生成的SQL查询（用于调试）
        var sql = query.ToQueryString();
        Console.WriteLine($"\nGenerated SQL Query:\n{sql}\n");

        // 获取结果并按创建时间排序
        var results = await query
            .OrderByDescending(j => j.CreatedAt)
            .Take(50) // 增加返回数量以提高匹配概率
            .ToListAsync();

        // 输出搜索结果
        Console.WriteLine($"\nFound {results.Count} potential matches:");
        foreach (var job in results)
        {
            Console.WriteLine($"- {job.BusinessName}: {job.JobTitle}");
        }

        return results;
    }

    public IQueryable<Job> GetQueryable()
    {
        return _context.Jobs.AsQueryable();
    }

    public async Task<Dictionary<DateTime, int>> GetJobCountsByDateAsync(int numberOfDays)
    {
        var startDate = DateTime.UtcNow.Date.AddDays(-numberOfDays + 1);

        var jobCounts = await _context.Jobs
            .Where(j => j.PostedDate.HasValue &&
                        j.PostedDate.Value.Date >= startDate &&
                        j.IsActive == true)
            .GroupBy(j => j.PostedDate.Value.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        // Fill in missing dates with zero counts
        var result = new Dictionary<DateTime, int>();
        for (var date = startDate; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
        {
            result[date] = jobCounts.ContainsKey(date) ? jobCounts[date] : 0;
        }

        return result.OrderByDescending(x => x.Key) // 按日期降序排序
            .ToDictionary(x => x.Key, x => x.Value);
    }

    public async Task<IEnumerable<JobTypeCount>> GetTopJobTypesAsync(int count)
    {
        return await _context.Jobs
            .Where(j => j.IsActive == true && !string.IsNullOrEmpty(j.JobType))
            .GroupBy(j => j.JobType)
            .Select(g => new JobTypeCount
            {
                JobType = g.Key!,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(count)
            .ToListAsync();
    }
}