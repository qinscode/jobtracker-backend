using JobTracker.Models;
using JobTracker.Repositories;
using JobTracker.Repositories.Interfaces;
using JobTracker.Services.Interfaces;

namespace JobTracker.Services;

public class JobMergeService : IJobMergeService
{
    private readonly IAnalyzedEmailRepository _analyzedEmailRepository;
    private readonly IJobMatchingService _jobMatchingService;
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<JobMergeService> _logger;
    private readonly IUserJobRepository _userJobRepository;

    public JobMergeService(
        IJobRepository jobRepository,
        IUserJobRepository userJobRepository,
        IAnalyzedEmailRepository analyzedEmailRepository,
        IJobMatchingService jobMatchingService,
        ILogger<JobMergeService> logger)
    {
        _jobRepository = jobRepository;
        _userJobRepository = userJobRepository;
        _analyzedEmailRepository = analyzedEmailRepository;
        _jobMatchingService = jobMatchingService;
        _logger = logger;
    }

    public async Task<List<(Job Job, double Similarity)>> FindPotentialMatchesAsync(int jobId)
    {
        var sourceJob = await _jobRepository.GetJobByIdAsync(jobId);
        if (sourceJob == null)
        {
            _logger.LogWarning("Job with ID {JobId} not found", jobId);
            return new List<(Job, double)>();
        }

        var matches = new List<(Job Job, double Similarity)>();
        var allJobs = await _jobRepository.GetJobsAsync(1, int.MaxValue);

        foreach (var job in allJobs.Where(j => j.Id != jobId))
        {
            var titleSimilarity = _jobMatchingService.CalculateSimilarity(
                sourceJob.JobTitle ?? "",
                job.JobTitle ?? "");

            var companySimilarity = _jobMatchingService.CalculateSimilarity(
                sourceJob.BusinessName ?? "",
                job.BusinessName ?? "");

            // 综合相似度（公司名称权重更高）
            var overallSimilarity = (titleSimilarity + companySimilarity * 2) / 3;

            if (overallSimilarity > 0.8) // 设置一个较高的阈值
                matches.Add((job, overallSimilarity));
        }

        return matches.OrderByDescending(m => m.Similarity).ToList();
    }

    public async Task<bool> MergeJobsAsync(int sourceJobId, int targetJobId)
    {
        try
        {
            var sourceJob = await _jobRepository.GetJobByIdAsync(sourceJobId);
            var targetJob = await _jobRepository.GetJobByIdAsync(targetJobId);

            if (sourceJob == null || targetJob == null)
            {
                _logger.LogWarning("Source job {SourceId} or target job {TargetId} not found",
                    sourceJobId, targetJobId);
                return false;
            }

            // 1. 更新所有关联的 UserJob 记录
            var userJobs = await _userJobRepository.GetUserJobsByJobIdAsync(sourceJobId);
            foreach (var userJob in userJobs)
            {
                // 检查是否已存在目标工作的记录
                var existingUserJob = await _userJobRepository.GetUserJobByUserIdAndJobIdAsync(
                    userJob.UserId, targetJobId);

                if (existingUserJob != null)
                {
                    // 如果存在，根据状态决定是否更新
                    if (ShouldUpdateStatus(existingUserJob.Status, userJob.Status))
                    {
                        existingUserJob.Status = userJob.Status;
                        existingUserJob.UpdatedAt = DateTime.UtcNow;
                        await _userJobRepository.UpdateUserJobAsync(existingUserJob);
                    }
                }
                else
                {
                    // 如果不存在，创建新记录
                    var newUserJob = new UserJob
                    {
                        UserId = userJob.UserId,
                        JobId = targetJobId,
                        Status = userJob.Status,
                        CreatedAt = userJob.CreatedAt,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _userJobRepository.CreateUserJobAsync(newUserJob);
                }
            }

            // 2. 更新所有关联的 AnalyzedEmail 记录
            var analyzedEmails = await _analyzedEmailRepository.GetEmailsByJobIdAsync(sourceJobId);
            foreach (var email in analyzedEmails)
            {
                email.MatchedJobId = targetJobId;
                await _analyzedEmailRepository.UpdateAsync(email);
            }

            // 3. 删除源工作
            await _jobRepository.DeleteJobAsync(sourceJobId);

            _logger.LogInformation(
                "Successfully merged job {SourceId} into {TargetId}",
                sourceJobId, targetJobId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error merging job {SourceId} into {TargetId}",
                sourceJobId, targetJobId);
            return false;
        }
    }

    private bool ShouldUpdateStatus(UserJobStatus currentStatus, UserJobStatus newStatus)
    {
        // 定义状态的进展顺序
        var statusOrder = new Dictionary<UserJobStatus, int>
        {
            { UserJobStatus.Applied, 0 },
            { UserJobStatus.Reviewed, 1 },
            { UserJobStatus.Interviewing, 2 },
            { UserJobStatus.TechnicalAssessment, 3 },
            { UserJobStatus.Offered, 4 },
            { UserJobStatus.Rejected, 5 }
        };

        // 如果新状态是 Rejected，总是更新
        if (newStatus == UserJobStatus.Rejected)
            return true;

        // 否则只有当新状态的顺序更高时才更新
        return statusOrder.TryGetValue(newStatus, out var newOrder) &&
               statusOrder.TryGetValue(currentStatus, out var currentOrder) &&
               newOrder > currentOrder;
    }
}