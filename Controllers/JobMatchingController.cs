using JobTracker.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class JobMatchingController : ControllerBase
{
    private readonly IJobMatchingService _jobMatchingService;
    private readonly ILogger<JobMatchingController> _logger;

    public JobMatchingController(
        IJobMatchingService jobMatchingService,
        ILogger<JobMatchingController> logger)
    {
        _jobMatchingService = jobMatchingService;
        _logger = logger;
    }

    [HttpPost("test-match")]
    public async Task<IActionResult> TestMatch([FromBody] JobMatchTestRequest request)
    {
        try
        {
            var (isMatch, matchedJob, similarity) = await _jobMatchingService.FindMatchingJobAsync(
                request.JobTitle,
                request.CompanyName);

            return Ok(new JobMatchResponse
            {
                IsMatch = isMatch,
                Similarity = similarity,
                MatchedJob = matchedJob != null ? new JobMatchInfo
                {
                    Id = matchedJob.Id,
                    JobTitle = matchedJob.JobTitle,
                    BusinessName = matchedJob.BusinessName,
                    CreatedAt = matchedJob.CreatedAt
                } : null,
                InputInfo = new JobMatchInfo
                {
                    JobTitle = request.JobTitle,
                    BusinessName = request.CompanyName
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing job match");
            return StatusCode(500, new { message = "Error testing job match", error = ex.Message });
        }
    }
}

public class JobMatchTestRequest
{
    public string JobTitle { get; set; }
    public string CompanyName { get; set; }
}

public class JobMatchResponse
{
    public bool IsMatch { get; set; }
    public double Similarity { get; set; }
    public JobMatchInfo? MatchedJob { get; set; }
    public JobMatchInfo InputInfo { get; set; }
}

public class JobMatchInfo
{
    public int? Id { get; set; }
    public string JobTitle { get; set; }
    public string BusinessName { get; set; }
    public DateTime? CreatedAt { get; set; }
} 