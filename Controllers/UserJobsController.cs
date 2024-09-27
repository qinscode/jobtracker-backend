using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
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
    public async Task<ActionResult<MessageResponseDto>> CreateUserJob([FromBody] CreateUserJobDto createUserJobDto)
    {
        var userId = GetUserIdFromToken();
        Console.WriteLine($"Received request: {JsonSerializer.Serialize(createUserJobDto)}");

        if (createUserJobDto == null) return BadRequest(new { message = "Invalid request body" });

        var existingUserJob = await _userJobRepository.GetUserJobByUserIdAndJobIdAsync(userId, createUserJobDto.JobId);
        if (existingUserJob != null) return Conflict(new { message = "UserJob already exists for this user and job" });

        var validationResult = await ValidateCreateUserJobDto(createUserJobDto.JobId);
        if (validationResult != null) return validationResult;

        var userJob = new UserJob
        {
            UserId = userId,
            JobId = createUserJobDto.JobId,
            Status = createUserJobDto.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userJobRepository.CreateUserJobAsync(userJob);

        var response = new MessageResponseDto
        {
            Message = "Successfully created"
        };

        return Ok(response);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUserJob(Guid id, UpdateUserJobDto updateUserJobDto)
    {
        var existingUserJob = await _userJobRepository.GetUserJobByIdAsync(id);
        if (existingUserJob == null)
            return NotFound(new { message = $"UserJob with id {id} not found" });

        var validationResult = await ValidateUpdateUserJobDto(id, updateUserJobDto);
        if (validationResult != null) return validationResult;

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

    [HttpPut("{jobId}/status/{status}")]
    [Authorize]
    public async Task<IActionResult> UpdateUserJobStatus(int jobId, UserJobStatus status)
    {
        // Get the user ID from the token
        var userId = GetUserIdFromToken();

        // Find the UserJob
        var userJob = await _userJobRepository.GetUserJobByUserIdAndJobIdAsync(userId, jobId);

        if (userJob == null) return NotFound(new { message = $"UserJob not found for user {userId} and job {jobId}" });

        // Update the status
        userJob.Status = status;
        userJob.UpdatedAt = DateTime.UtcNow;

        // Save the changes
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
    public async Task<ActionResult<JobsResponseDto>> GetUserJobsByStatus(
        UserJobStatus status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var userGuid = GetUserIdFromToken();

        var jobs = await _userJobRepository.GetJobsByUserIdAndStatusAsync(userGuid, status, pageNumber, pageSize);
        var totalCount = await _userJobRepository.GetJobsCountByUserIdAndStatusAsync(userGuid, status);

        var response = new JobsResponseDto
        {
            Jobs = jobs.Select(j => new JobDto
            {
                Id = j.Id, // Changed from string to int
                JobTitle = j.JobTitle ?? "",
                BusinessName = j.BusinessName ?? "",
                WorkType = j.WorkType ?? "",
                JobType = j.JobType ?? "",
                PayRange = j.PayRange ?? "",
                Suburb = j.Suburb ?? "",
                Area = j.Area ?? "",
                Url = j.Url ?? "",
                Status = status.ToString(),
                PostedDate = j.PostedDate?.ToString("yyyy-MM-dd") ?? "",
                JobDescription = j.JobDescription ?? ""
            }),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        return Ok(response);
    }

    [HttpGet("recent")]
    public async Task<ActionResult<IEnumerable<UserJobDto>>> GetRecentUserJobs([FromQuery] int count = 5)
    {
        var userGuid = GetUserIdFromToken();

        var statuses = new[]
        {
            UserJobStatus.Applied,
            UserJobStatus.Interviewing,
            UserJobStatus.TechnicalAssessment,
            UserJobStatus.Offered,
            UserJobStatus.Rejected
        };

        var recentUserJobs = await _userJobRepository.GetRecentUserJobsAsync(userGuid, count, statuses);
        var userJobDtos = recentUserJobs.Select(uj => CreateUserJobDto(uj)).ToList();

        return Ok(userJobDtos);
    }

    [HttpGet("count")]
    public async Task<ActionResult<UserJobStatusCountResponse>> GetUserJobStatusCounts()
    {
        var userId = GetUserIdFromToken();

        var statusCounts = await _userJobRepository.GetUserJobStatusCountsAsync(userId);
        var totalJobsCount = await _userJobRepository.GetTotalJobsCountAsync();
        var newJobsCount = await _userJobRepository.GetNewJobsCountAsync();

        var result = new UserJobStatusCountResponse
        {
            StatusCounts = Enum.GetValues(typeof(UserJobStatus))
                .Cast<UserJobStatus>()
                .Select(status => new UserJobStatusCountDto
                {
                    Status = status.ToString(),
                    Count = statusCounts.ContainsKey(status) ? statusCounts[status] : 0
                })
                .ToList(),
            TotalJobsCount = totalJobsCount,
            NewJobsCount = newJobsCount
        };

        return Ok(result);
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
            JobId = userJob.JobId,
            JobTitle = userJob.Job?.JobTitle,
            Status = userJob.Status.ToString(),
            CreatedAt = userJob.CreatedAt,
            UpdatedAt = userJob.UpdatedAt
        };
    }

    private async Task<ActionResult> ValidateCreateUserJobDto(int jobId)
    {
        var job = await _jobRepository.GetJobByIdAsync(jobId);
        if (job == null) return BadRequest(new { message = "Invalid JobId" });

        return null;
    }

    private async Task<ActionResult> ValidateUpdateUserJobDto(Guid id, UpdateUserJobDto updateUserJobDto)
    {
        var existingUserJob = await _userJobRepository.GetUserJobByIdAsync(id);
        if (existingUserJob == null) return NotFound(new { message = $"UserJob with id {id} not found" });

        var job = await _jobRepository.GetJobByIdAsync(updateUserJobDto.JobId);
        if (job == null) return BadRequest(new { message = "Invalid JobId" });

        return null;
    }

    private void UpdateUserJobFromDto(UserJob userJob, UpdateUserJobDto updateUserJobDto)
    {
        userJob.JobId = updateUserJobDto.JobId;
        userJob.Status = updateUserJobDto.Status;
        userJob.UpdatedAt = DateTime.UtcNow;
    }
}