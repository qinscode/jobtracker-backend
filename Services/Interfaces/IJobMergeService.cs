using JobTracker.Models;

namespace JobTracker.Services.Interfaces;

public interface IJobMergeService
{
    /// <summary>
    ///     尝试将一个工作与另一个工作合并
    /// </summary>
    /// <param name="sourceJobId">源工作ID（将被合并的工作）</param>
    /// <param name="targetJobId">目标工作ID（保留的工作）</param>
    /// <returns>合并是否成功</returns>
    Task<bool> MergeJobsAsync(int sourceJobId, int targetJobId);

    /// <summary>
    ///     查找可能匹配的工作
    /// </summary>
    /// <param name="jobId">要查找匹配的工作ID</param>
    /// <returns>可能匹配的工作列表，包含相似度分数</returns>
    Task<List<(Job Job, double Similarity)>> FindPotentialMatchesAsync(int jobId);
}