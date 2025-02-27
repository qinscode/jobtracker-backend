using System.IdentityModel.Tokens.Jwt;
using JobTracker.Models;
using JobTracker.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EmailAnalysisController : ControllerBase
{
    private readonly IAnalyzedEmailRepository _analyzedEmailRepository;
    private readonly IUserEmailConfigRepository _userEmailConfigRepository;
    private readonly ILogger<EmailAnalysisController> _logger;
    private readonly IUserJobRepository _userJobRepository;

    public EmailAnalysisController(
        IAnalyzedEmailRepository analyzedEmailRepository,
        IUserEmailConfigRepository userEmailConfigRepository,
        IUserJobRepository userJobRepository,
        ILogger<EmailAnalysisController> logger)
    {
        _analyzedEmailRepository = analyzedEmailRepository;
        _userEmailConfigRepository = userEmailConfigRepository;
        _userJobRepository = userJobRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<EmailAnalysisResponseDto>> GetAnalyzedEmails(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "receivedDate",
        [FromQuery] bool sortDescending = true)
    {
        try
        {
            var userId = GetUserIdFromToken();
            var configs = await _userEmailConfigRepository.GetByUserIdAsync(userId);

            if (!configs.Any())
            {
                return Ok(new EmailAnalysisResponseDto
                {
                    Emails = new List<EmailAnalysisDto>(),
                    TotalCount = 0,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }

            var allEmails = new List<AnalyzedEmail>();
            var totalCount = 0;

            foreach (var config in configs)
            {
                var (emails, count) = await _analyzedEmailRepository.GetAnalyzedEmailsAsync(
                    config.Id, 1, int.MaxValue);
                allEmails.AddRange(emails);
                totalCount += count;
            }

            var orderedEmails = (sortBy?.ToLower() switch
            {
                "subject" => sortDescending
                    ? allEmails.OrderByDescending(e => e.Subject)
                    : allEmails.OrderBy(e => e.Subject),
                "jobtitle" => sortDescending
                    ? allEmails.OrderByDescending(e => e.MatchedJob != null ? e.MatchedJob.JobTitle : string.Empty)
                    : allEmails.OrderBy(e => e.MatchedJob != null ? e.MatchedJob.JobTitle : string.Empty),
                "company" => sortDescending
                    ? allEmails.OrderByDescending(e => e.MatchedJob != null ? e.MatchedJob.BusinessName : string.Empty)
                    : allEmails.OrderBy(e => e.MatchedJob != null ? e.MatchedJob.BusinessName : string.Empty),
                "similarity" => sortDescending
                    ? allEmails.OrderByDescending(e => e.Similarity ?? 0)
                    : allEmails.OrderBy(e => e.Similarity ?? 0),
                _ => sortDescending
                    ? allEmails.OrderByDescending(e => e.ReceivedDate)
                    : allEmails.OrderBy(e => e.ReceivedDate)
            }).ToList();

            var pagedEmails = orderedEmails
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var jobStatuses = new Dictionary<int, UserJobStatus>();
            foreach (var email in pagedEmails.Where(e => e.MatchedJob != null))
            {
                var jobId = email.MatchedJob!.Id;
                if (!jobStatuses.ContainsKey(jobId))
                {
                    var userJob = await _userJobRepository.GetUserJobByUserIdAndJobIdAsync(userId, jobId);
                    jobStatuses[jobId] = userJob?.Status ?? UserJobStatus.New;
                }
            }

            var emailDtos = pagedEmails.Select(e => new EmailAnalysisDto
            {
                Subject = e.Subject,
                ReceivedDate = e.ReceivedDate,
                IsRecognized = e.MatchedJob != null,
                Job = e.MatchedJob == null
                    ? null
                    : new JobBasicInfo
                    {
                        Id = e.MatchedJob.Id,
                        JobTitle = e.MatchedJob.JobTitle ?? string.Empty,
                        BusinessName = e.MatchedJob.BusinessName ?? string.Empty
                    },
                KeyPhrases = e.KeyPhrases.ToList(),
                SuggestedActions = e.SuggestedActions,
                ReasonForRejection = e.ReasonForRejection,
                Similarity = e.Similarity,
                Status = e.MatchedJob != null ? jobStatuses[e.MatchedJob.Id].ToString() : UserJobStatus.New.ToString()
            }).ToList();

            return Ok(new EmailAnalysisResponseDto
            {
                Emails = emailDtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analyzed emails");
            return StatusCode(500, "An error occurred while retrieving analyzed emails");
        }
    }

    [HttpGet("config/{configId}")]
    public async Task<ActionResult<EmailAnalysisResponseDto>> GetAnalyzedEmailsByConfig(
        Guid configId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetUserIdFromToken();
            var config = await _userEmailConfigRepository.GetByIdAsync(configId);
            if (config == null || config.UserId != userId)
            {
                return NotFound("Email configuration not found");
            }

            var (emails, totalCount) = await _analyzedEmailRepository.GetAnalyzedEmailsAsync(
                configId, pageNumber, pageSize);

            // 预先获取所有需要的 UserJob 状态
            var jobStatuses = new Dictionary<int, UserJobStatus>();
            foreach (var email in emails.Where(e => e.MatchedJob != null))
            {
                var jobId = email.MatchedJob!.Id;
                if (!jobStatuses.ContainsKey(jobId))
                {
                    var userJob = await _userJobRepository.GetUserJobByUserIdAndJobIdAsync(userId, jobId);
                    jobStatuses[jobId] = userJob?.Status ?? UserJobStatus.New;
                }
            }

            var emailDtos = emails.Select(e => new EmailAnalysisDto
            {
                Subject = e.Subject,
                ReceivedDate = e.ReceivedDate,
                IsRecognized = e.MatchedJob != null,
                Job = e.MatchedJob == null
                    ? null
                    : new JobBasicInfo
                    {
                        Id = e.MatchedJob.Id,
                        JobTitle = e.MatchedJob.JobTitle ?? string.Empty,
                        BusinessName = e.MatchedJob.BusinessName ?? string.Empty
                    },
                KeyPhrases = e.KeyPhrases.ToList(),
                SuggestedActions = e.SuggestedActions,
                ReasonForRejection = e.ReasonForRejection,
                Similarity = e.Similarity,
                Status = e.MatchedJob != null ? jobStatuses[e.MatchedJob.Id].ToString() : UserJobStatus.New.ToString()
            }).ToList();

            return Ok(new EmailAnalysisResponseDto
            {
                Emails = emailDtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analyzed emails for config {ConfigId}", configId);
            return StatusCode(500, "An error occurred while retrieving analyzed emails");
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<EmailAnalysisResponseDto>> SearchAnalyzedEmails(
        [FromQuery] string searchTerm,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "receivedDate",
        [FromQuery] bool sortDescending = true)
    {
        try
        {
            var userId = GetUserIdFromToken();

            var (emails, totalCount) = await _analyzedEmailRepository.SearchAnalyzedEmailsAsync(
                userId, searchTerm, pageNumber, pageSize);

            // 应用排序
            var orderedEmails = (sortBy?.ToLower() switch
            {
                "subject" => sortDescending
                    ? emails.OrderByDescending(e => e.Subject)
                    : emails.OrderBy(e => e.Subject),
                "jobtitle" => sortDescending
                    ? emails.OrderByDescending(e => e.MatchedJob != null ? e.MatchedJob.JobTitle : string.Empty)
                    : emails.OrderBy(e => e.MatchedJob != null ? e.MatchedJob.JobTitle : string.Empty),
                "company" => sortDescending
                    ? emails.OrderByDescending(e => e.MatchedJob != null ? e.MatchedJob.BusinessName : string.Empty)
                    : emails.OrderBy(e => e.MatchedJob != null ? e.MatchedJob.BusinessName : string.Empty),
                "similarity" => sortDescending
                    ? emails.OrderByDescending(e => e.Similarity ?? 0)
                    : emails.OrderBy(e => e.Similarity ?? 0),
                _ => sortDescending
                    ? emails.OrderByDescending(e => e.ReceivedDate)
                    : emails.OrderBy(e => e.ReceivedDate)
            }).ToList();

            // 预先获取所有需要的 UserJob 状态
            var jobStatuses = new Dictionary<int, UserJobStatus>();
            foreach (var email in orderedEmails.Where(e => e.MatchedJob != null))
            {
                var jobId = email.MatchedJob!.Id;
                if (!jobStatuses.ContainsKey(jobId))
                {
                    var userJob = await _userJobRepository.GetUserJobByUserIdAndJobIdAsync(userId, jobId);
                    jobStatuses[jobId] = userJob?.Status ?? UserJobStatus.New;
                }
            }

            var emailDtos = orderedEmails.Select(e => new EmailAnalysisDto
            {
                Subject = e.Subject,
                ReceivedDate = e.ReceivedDate,
                IsRecognized = e.MatchedJob != null,
                Job = e.MatchedJob == null
                    ? null
                    : new JobBasicInfo
                    {
                        Id = e.MatchedJob.Id,
                        JobTitle = e.MatchedJob.JobTitle ?? string.Empty,
                        BusinessName = e.MatchedJob.BusinessName ?? string.Empty
                    },
                KeyPhrases = e.KeyPhrases.ToList(),
                SuggestedActions = e.SuggestedActions,
                ReasonForRejection = e.ReasonForRejection,
                Similarity = e.Similarity,
                Status = e.MatchedJob != null ? jobStatuses[e.MatchedJob.Id].ToString() : UserJobStatus.New.ToString()
            }).ToList();

            return Ok(new EmailAnalysisResponseDto
            {
                Emails = emailDtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching analyzed emails with term: {SearchTerm}", searchTerm);
            return StatusCode(500, "An error occurred while searching analyzed emails");
        }
    }

    private Guid GetUserIdFromToken()
    {
        var token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

        var userId = jsonToken?.Claims.FirstOrDefault(claim =>
            claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            throw new UnauthorizedAccessException("Invalid or missing user ID in the token");

        return userGuid;
    }

    private async Task<UserJobStatus> GetJobStatus(int? jobId, Guid userId)
    {
        if (!jobId.HasValue)
            return UserJobStatus.New;

        var userJob = await _userJobRepository.GetUserJobByUserIdAndJobIdAsync(userId, jobId.Value);
        return userJob?.Status ?? UserJobStatus.New;
    }
}

public class EmailAnalysisResponseDto
{
    public List<EmailAnalysisDto> Emails { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}