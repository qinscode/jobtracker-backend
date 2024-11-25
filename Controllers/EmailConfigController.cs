using System.IdentityModel.Tokens.Jwt;
using System.Text;
using JobTracker.Models;
using JobTracker.Repositories;
using JobTracker.Services;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EmailConfigController : ControllerBase
{
    private readonly IAnalyzedEmailRepository _analyzedEmailRepository;
    private readonly IUserEmailConfigRepository _configRepository;
    private readonly IEmailAnalysisService _emailAnalysisService;
    private readonly ILogger<EmailConfigController> _logger;

    public EmailConfigController(
        IEmailAnalysisService emailAnalysisService,
        IUserEmailConfigRepository configRepository,
        IAnalyzedEmailRepository analyzedEmailRepository,
        ILogger<EmailConfigController> logger)
    {
        _emailAnalysisService = emailAnalysisService;
        _configRepository = configRepository;
        _logger = logger;
        _analyzedEmailRepository = analyzedEmailRepository;
    }

    [HttpPost]
    public async Task<IActionResult> AddEmailConfig(AddEmailConfigDto dto)
    {
        if (dto.Provider.ToLower() != "gmail") return BadRequest(new { message = "Only Gmail is supported" });

        if (string.IsNullOrEmpty(dto.Password)) return BadRequest(new { message = "App Password is required" });

        var userId = GetUserIdFromToken();

        // 检查该用户是否已经添加过这个邮箱
        var existingConfigs = await _configRepository.GetByUserIdAsync(userId);
        if (existingConfigs.Any(c => c.EmailAddress.ToLower() == dto.EmailAddress.ToLower()))
            return BadRequest(new { message = "This email address has already been configured" });

        var config = new UserEmailConfig
        {
            UserId = userId,
            EmailAddress = dto.EmailAddress,
            EncryptedPassword = EncryptPassword(dto.Password),
            Provider = dto.Provider,
            LastSyncTime = DateTime.UtcNow
        };

        try
        {
            // 测试连接
            using var emailClient = new ImapClient();
            await emailClient.ConnectAsync("imap.gmail.com", 993, true);
            await emailClient.AuthenticateAsync(dto.EmailAddress, dto.Password);
            await emailClient.DisconnectAsync(true);

            await _configRepository.CreateAsync(config);
            await _emailAnalysisService.AnalyzeRecentEmails(config);

            return Ok(new { message = "Email configuration added successfully" });
        }
        catch (AuthenticationException)
        {
            return BadRequest(new { message = "Invalid email or App Password" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to configure email access", error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetEmailConfigs()
    {
        var userId = GetUserIdFromToken();
        var configs = await _configRepository.GetByUserIdAsync(userId);

        var configDtos = configs.Select(c => new
        {
            c.Id,
            c.EmailAddress,
            c.Provider,
            c.LastSyncTime,
            c.IsActive
        });

        return Ok(configDtos);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEmailConfig(Guid id)
    {
        var userId = GetUserIdFromToken();
        var configs = await _configRepository.GetByUserIdAsync(userId);
        var config = configs.FirstOrDefault(c => c.Id == id);

        if (config == null) return NotFound(new { message = "Email configuration not found" });

        await _configRepository.DeleteAsync(id);
        return Ok(new { message = "Email configuration deleted successfully" });
    }


    [HttpPost("{id}/scan-incremental")]
    public async Task<ActionResult<List<EmailAnalysisDto>>> ScanIncrementalEmails(Guid id)
    {
        try
        {
            var config = await _configRepository.GetByIdAsync(id);
            if (config == null) return NotFound("Email configuration not found");

            var lastUid = await _analyzedEmailRepository.GetLastAnalyzedUidAsync(config.Id);

            _logger.LogInformation("Starting incremental email scan for config {Id} from UID {LastUid}", id,
                lastUid ?? 0);

            var results = await _emailAnalysisService.AnalyzeIncrementalEmails(config, lastUid);

            _logger.LogInformation("Completed incremental email scan for config {Id}, found {Count} new emails",
                id, results.Count);

            // 格式化响应，包含新的字段
            var response = results.Select(r => new
            {
                r.Subject,
                r.ReceivedDate,
                r.IsRecognized,
                Job = r.Job == null
                    ? null
                    : new
                    {
                        r.Job.Id,
                        r.Job.JobTitle,
                        r.Job.BusinessName
                    },
                Status = r.Status.ToString(),
                r.KeyPhrases,
                r.SuggestedActions
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during incremental email scan for config {Id}", id);
            return StatusCode(500, "An error occurred while scanning emails incrementally");
        }
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeEmails()
    {
        try
        {
            var userId = GetUserIdFromToken();
            var configs = await _configRepository.GetByUserIdAsync(userId);
            var allResults = new List<EmailAnalysisDto>();

            foreach (var config in configs)
            {
                var results = await _emailAnalysisService.AnalyzeRecentEmails(config);
                allResults.AddRange(results);
            }

            // 格式化响应，包含新的字段
            var response = new
            {
                message = "Email analysis completed",
                totalEmails = allResults.Count,
                recognizedEmails = allResults.Count(r => r.IsRecognized),
                results = allResults.Select(r => new
                {
                    r.Subject,
                    r.ReceivedDate,
                    r.IsRecognized,
                    Job = r.Job == null
                        ? null
                        : new
                        {
                            r.Job.Id,
                            r.Job.JobTitle,
                            r.Job.BusinessName
                        },
                    Status = r.Status.ToString(),
                    r.KeyPhrases,
                    r.SuggestedActions
                }).OrderByDescending(r => r.ReceivedDate)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to analyze emails", error = ex.Message });
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

    private string EncryptPassword(string password)
    {
        // TODO: Implement proper encryption
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
    }
}