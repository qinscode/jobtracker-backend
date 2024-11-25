using JobTracker.Models;
using JobTracker.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace JobTracker.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EmailAnalysisController : ControllerBase
{
    private readonly IAnalyzedEmailRepository _analyzedEmailRepository;
    private readonly IUserEmailConfigRepository _userEmailConfigRepository;
    private readonly ILogger<EmailAnalysisController> _logger;

    public EmailAnalysisController(
        IAnalyzedEmailRepository analyzedEmailRepository,
        IUserEmailConfigRepository userEmailConfigRepository,
        ILogger<EmailAnalysisController> logger)
    {
        _analyzedEmailRepository = analyzedEmailRepository;
        _userEmailConfigRepository = userEmailConfigRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<EmailAnalysisResponseDto>> GetAnalyzedEmails(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetUserIdFromToken();
            
            // 获取用户的邮件配置
            var configs = await _userEmailConfigRepository.GetByUserIdAsync(userId);
            if (!configs.Any())
            {
                return NotFound("No email configurations found for this user");
            }

            var allEmails = new List<AnalyzedEmail>();
            var totalCount = 0;

            // 获取所有配置的分析结果
            foreach (var config in configs)
            {
                var (emails, count) = await _analyzedEmailRepository.GetAnalyzedEmailsAsync(
                    config.Id, pageNumber, pageSize);
                allEmails.AddRange(emails);
                totalCount += count;
            }

            // 按接收时间排序并应用分页
            var pagedEmails = allEmails
                .OrderByDescending(e => e.ReceivedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);

            var emailDtos = pagedEmails.Select(e => new EmailAnalysisDto
            {
                Subject = e.Subject,
                ReceivedDate = e.ReceivedDate,
                IsRecognized = e.MatchedJob != null,
                Job = e.MatchedJob == null ? null : new JobBasicInfo
                {
                    Id = e.MatchedJob.Id,
                    JobTitle = e.MatchedJob.JobTitle ?? string.Empty,
                    BusinessName = e.MatchedJob.BusinessName ?? string.Empty
                },
                KeyPhrases = e.KeyPhrases.ToList(),
                SuggestedActions = e.SuggestedActions,
                Similarity = e.Similarity
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
            
            // 验证邮件配置属于当前用户
            var config = await _userEmailConfigRepository.GetByIdAsync(configId);
            if (config == null || config.UserId != userId)
            {
                return NotFound("Email configuration not found");
            }

            var (emails, totalCount) = await _analyzedEmailRepository.GetAnalyzedEmailsAsync(
                configId, pageNumber, pageSize);

            var emailDtos = emails.Select(e => new EmailAnalysisDto
            {
                Subject = e.Subject,
                ReceivedDate = e.ReceivedDate,
                IsRecognized = e.MatchedJob != null,
                Job = e.MatchedJob == null ? null : new JobBasicInfo
                {
                    Id = e.MatchedJob.Id,
                    JobTitle = e.MatchedJob.JobTitle ?? string.Empty,
                    BusinessName = e.MatchedJob.BusinessName ?? string.Empty
                },
                KeyPhrases = e.KeyPhrases.ToList(),
                SuggestedActions = e.SuggestedActions,
                Similarity = e.Similarity
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
}

public class EmailAnalysisResponseDto
{
    public List<EmailAnalysisDto> Emails { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
} 