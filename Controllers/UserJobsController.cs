using System.IdentityModel.Tokens.Jwt;
using JobTracker.Models;
using JobTracker.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UserJobsController : ControllerBase
{
    private readonly IJobRepository _jobRepository;
    private readonly IUserJobRepository _userJobRepository;
    private readonly IUserRepository _userRepository;

    public UserJobsController(IUserJobRepository userJobRepository, IUserRepository userRepository,
        IJobRepository jobRepository)
    {
        _userJobRepository = userJobRepository;
        _userRepository = userRepository;
        _jobRepository = jobRepository;
    }

    [HttpGet]
    public async Task<ActionResult<UserJobsResponseDto>> GetUserJobs([FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userJobs = await _userJobRepository.GetUserJobsAsync(pageNumber, pageSize);
        var totalCount = await _userJobRepository.GetUserJobsCountAsync();

        if (totalCount == 0) return NotFound(new { message = "No user jobs found" });

        var response = CreateUserJobsResponse(userJobs, totalCount, pageNumber, pageSize);
        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserJobDto>> GetUserJob(Guid id)
    {
        var userJob = await _userJobRepository.GetUserJobByIdAsync(id);
        if (userJob == null) return NotFound(new { message = $"UserJob with id {id} not found" });

        var userJobDto = CreateUserJobDto(userJob);
        return Ok(userJobDto);
    }

    [HttpPost]
    public async Task<ActionResult<UserJob>> CreateUserJob(CreateUserJobDto createUserJobDto)
    {
        var validationResult = await ValidateCreateUserJobDto(createUserJobDto);
        if (validationResult != null) return validationResult;

        var userJob = new UserJob
        {
            UserId = createUserJobDto.UserId,
            JobId = createUserJobDto.JobId,
            Status = createUserJobDto.Status
        };

        var createdUserJob = await _userJobRepository.CreateUserJobAsync(userJob);
        return CreatedAtAction(nameof(GetUserJob), new { id = createdUserJob.Id }, createdUserJob);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUserJob(Guid id, UpdateUserJobDto updateUserJobDto)
    {
        if (id != updateUserJobDto.Id)
            return BadRequest(new { message = "Id in URL does not match Id in request body" });

        var validationResult = await ValidateUpdateUserJobDto(id, updateUserJobDto);
        if (validationResult != null) return validationResult;

        var existingUserJob = await _userJobRepository.GetUserJobByIdAsync(id);
        UpdateUserJobFromDto(existingUserJob, updateUserJobDto);

        await _userJobRepository.UpdateUserJobAsync(existingUserJob);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUserJob(Guid id)
    {
        var existingUserJob = await _userJobRepository.GetUserJobByIdAsync(id);
        if (existingUserJob == null) return NotFound(new { message = $"UserJob with id {id} not found" });

        await _userJobRepository.DeleteUserJobAsync(id);
        return NoContent();
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateUserJobStatus(Guid id, [FromBody] UserJobStatus newStatus)
    {
        var userJob = await _userJobRepository.GetUserJobByIdAsync(id);
        if (userJob == null) return NotFound(new { message = $"UserJob with id {id} not found" });

        userJob.Status = newStatus;
        userJob.UpdatedAt = DateTime.UtcNow;
        await _userJobRepository.UpdateUserJobAsync(userJob);
        return NoContent();
    }

    [HttpGet("user/{userId}/status/{status}")]
    public async Task<ActionResult<UserJobsResponseDto>> GetUserJobsByUserIdAndStatus(
        Guid userId, UserJobStatus status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var userJobs = await _userJobRepository.GetUserJobsByUserIdAndStatusAsync(userId, status, pageNumber, pageSize);
        var totalCount = await _userJobRepository.GetUserJobsCountByUserIdAndStatusAsync(userId, status);

        if (totalCount == 0)
            return NotFound(new { message = $"No user jobs found for user {userId} with status {status}" });

        var response = CreateUserJobsResponse(userJobs, totalCount, pageNumber, pageSize);
        return Ok(response);
    }

    [HttpGet("status/{status}")]
    public async Task<ActionResult<UserJobsResponseDto>> GetUserJobsByStatus(
        UserJobStatus status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var userGuid = GetUserIdFromToken();

        var userJobs =
            await _userJobRepository.GetUserJobsByUserIdAndStatusAsync(userGuid, status, pageNumber, pageSize);
        var totalCount = await _userJobRepository.GetUserJobsCountByUserIdAndStatusAsync(userGuid, status);

        // if (totalCount == 0) return NotFound(new { message = $"No user jobs found with status {status}" });

        var response = CreateUserJobsResponse(userJobs, totalCount, pageNumber, pageSize);
        return Ok(response);
    }

    [HttpGet("recent")]
    public async Task<ActionResult<IEnumerable<UserJobDto>>> GetRecentUserJobs([FromQuery] int count = 10)
    {
        var userGuid = GetUserIdFromToken();

        var statuses = new[]
        {
            UserJobStatus.Interviewing,
            UserJobStatus.TechnicalAssessment,
            UserJobStatus.Offered,
            UserJobStatus.Rejected
        };

        var recentUserJobs = await _userJobRepository.GetRecentUserJobsAsync(userGuid, count, statuses);
        var userJobDtos = recentUserJobs.Select(uj => CreateUserJobDto(uj)).ToList();

        return Ok(userJobDtos);
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

    private UserJobsResponseDto CreateUserJobsResponse(IEnumerable<UserJob> userJobs, int totalCount, int pageNumber,
        int pageSize)
    {
        return new UserJobsResponseDto
        {
            UserJobs = userJobs.Select(uj => CreateUserJobDto(uj)).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    private UserJobDto CreateUserJobDto(UserJob userJob)
    {
        return new UserJobDto
        {
            Id = userJob.Id,
            UserId = userJob.UserId,
            UserName = userJob.User?.Username,
            JobId = userJob.JobId,
            JobTitle = userJob.Job?.JobTitle,
            Status = userJob.Status,
            CreatedAt = userJob.CreatedAt,
            UpdatedAt = userJob.UpdatedAt
        };
    }

    private async Task<ActionResult> ValidateCreateUserJobDto(CreateUserJobDto createUserJobDto)
    {
        var user = await _userRepository.GetUserByIdAsync(createUserJobDto.UserId);
        if (user == null) return BadRequest(new { message = "Invalid UserId" });

        var job = await _jobRepository.GetJobByIdAsync(createUserJobDto.JobId);
        if (job == null) return BadRequest(new { message = "Invalid JobId" });

        var existingUserJob =
            await _userJobRepository.GetUserJobByUserIdAndJobIdAsync(createUserJobDto.UserId, createUserJobDto.JobId);
        if (existingUserJob != null)
            return Conflict(new { message = "A UserJob with the same UserId and JobId already exists." });

        return null;
    }

    private async Task<ActionResult> ValidateUpdateUserJobDto(Guid id, UpdateUserJobDto updateUserJobDto)
    {
        var existingUserJob = await _userJobRepository.GetUserJobByIdAsync(id);
        if (existingUserJob == null) return NotFound(new { message = $"UserJob with id {id} not found" });

        var user = await _userRepository.GetUserByIdAsync(updateUserJobDto.UserId);
        if (user == null) return BadRequest(new { message = "Invalid UserId" });

        var job = await _jobRepository.GetJobByIdAsync(updateUserJobDto.JobId);
        if (job == null) return BadRequest(new { message = "Invalid JobId" });

        if (!Enum.TryParse<UserJobStatus>(updateUserJobDto.Status, true, out _))
            return BadRequest(new { message = $"Invalid status value: {updateUserJobDto.Status}" });

        return null;
    }

    private void UpdateUserJobFromDto(UserJob userJob, UpdateUserJobDto updateUserJobDto)
    {
        userJob.UserId = updateUserJobDto.UserId;
        userJob.JobId = updateUserJobDto.JobId;
        if (Enum.TryParse<UserJobStatus>(updateUserJobDto.Status, true, out var status))
            userJob.Status = status;
        else
            throw new ArgumentException($"Invalid status value: {updateUserJobDto.Status}");
        userJob.UpdatedAt = DateTime.UtcNow;
    }
}