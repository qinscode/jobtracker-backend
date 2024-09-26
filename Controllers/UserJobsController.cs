using JobTracker.Models;
using JobTracker.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserJobsController : ControllerBase
{
    private readonly IUserJobRepository _userJobRepository;

    public UserJobsController(IUserJobRepository userJobRepository)
    {
        _userJobRepository = userJobRepository;
    }

    [HttpGet]
    public async Task<ActionResult<UserJobsResponseDto>> GetUserJobs([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var userJobs = await _userJobRepository.GetUserJobsAsync(pageNumber, pageSize);
        var totalCount = await _userJobRepository.GetUserJobsCountAsync();

        if (totalCount == 0)
        {
            return NotFound(new { message = "No user jobs found" });
        }

        var response = new UserJobsResponseDto
        {
            UserJobs = userJobs.Select(uj => new UserJobDto
            {
                Id = uj.Id,
                UserId = uj.UserId,
                UserName = uj.User?.Username,
                JobId = uj.JobId,
                JobTitle = uj.Job?.JobTitle,
                Status = uj.Status,
                CreatedAt = uj.CreatedAt,
                UpdatedAt = uj.UpdatedAt
            }),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserJobDto>> GetUserJob(Guid id)
    {
        var userJob = await _userJobRepository.GetUserJobByIdAsync(id);
        if (userJob == null) return NotFound(new { message = $"UserJob with id {id} not found" });
        
        var userJobDto = new UserJobDto
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
        
        return Ok(userJobDto);
    }

    [HttpPost]
    public async Task<ActionResult<UserJob>> CreateUserJob(UserJob userJob)
    {
        var createdUserJob = await _userJobRepository.CreateUserJobAsync(userJob);
        return CreatedAtAction(nameof(GetUserJob), new { id = createdUserJob.Id }, createdUserJob);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUserJob(Guid id, UserJob userJob)
    {
        if (id != userJob.Id) return BadRequest(new { message = "Id in URL does not match Id in request body" });

        var existingUserJob = await _userJobRepository.GetUserJobByIdAsync(id);
        if (existingUserJob == null) return NotFound(new { message = $"UserJob with id {id} not found" });

        await _userJobRepository.UpdateUserJobAsync(userJob);
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
        await _userJobRepository.UpdateUserJobAsync(userJob);
        return NoContent();
    }
}