using JobTracker.Models;
using JobTracker.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class JobMergeController : ControllerBase
{
    private readonly IJobMergeService _jobMergeService;
    private readonly ILogger<JobMergeController> _logger;

    public JobMergeController(
        IJobMergeService jobMergeService,
        ILogger<JobMergeController> logger)
    {
        _jobMergeService = jobMergeService;
        _logger = logger;
    }

    /// <summary>
    /// 查找可能重复的工作
    /// </summary>
    /// <param name="jobId">工作ID</param>
    /// <returns>可能匹配的工作列表</returns>
    [HttpGet("potential-matches/{jobId}")]
    public async Task<ActionResult<List<JobMatchDto>>> FindPotentialMatches(int jobId)
    {
        try
        {
            var matches = await _jobMergeService.FindPotentialMatchesAsync(jobId);
            var result = matches.Select(m => new JobMatchDto
            {
                Job = new JobBasicInfo
                {
                    Id = m.Job.Id,
                    JobTitle = m.Job.JobTitle ?? string.Empty,
                    BusinessName = m.Job.BusinessName ?? string.Empty
                },
                Similarity = m.Similarity
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding potential matches for job {JobId}", jobId);
            return StatusCode(500, "Error finding potential matches");
        }
    }

    /// <summary>
    /// 合并两个工作
    /// </summary>
    /// <param name="request">合并请求</param>
    /// <returns>合并结果</returns>
    [HttpPost("merge")]
    public async Task<ActionResult<MergeResultDto>> MergeJobs([FromBody] MergeJobsRequest request)
    {
        try
        {
            _logger.LogInformation(
                "Attempting to merge job {SourceId} into {TargetId}",
                request.SourceJobId, request.TargetJobId);

            if (request.SourceJobId == request.TargetJobId)
            {
                return BadRequest("Source and target jobs cannot be the same");
            }

            var success = await _jobMergeService.MergeJobsAsync(
                request.SourceJobId, request.TargetJobId);

            if (success)
            {
                _logger.LogInformation(
                    "Successfully merged job {SourceId} into {TargetId}",
                    request.SourceJobId, request.TargetJobId);

                return Ok(new MergeResultDto
                {
                    Success = true,
                    Message = "Jobs merged successfully"
                });
            }

            return BadRequest(new MergeResultDto
            {
                Success = false,
                Message = "Failed to merge jobs"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error merging job {SourceId} into {TargetId}",
                request.SourceJobId, request.TargetJobId);

            return StatusCode(500, new MergeResultDto
            {
                Success = false,
                Message = "An error occurred while merging jobs"
            });
        }
    }
}

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