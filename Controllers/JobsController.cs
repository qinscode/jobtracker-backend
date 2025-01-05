using JobTracker.Models;
using JobTracker.Repositories;
using JobTracker.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Controllers;

[ApiController]
[Route("api/jobs")]
public class JobsController : ControllerBase
{
    private static readonly TimeZoneInfo PerthTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Perth");
    private readonly IJobMatchingService _jobMatchingService;
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IJobRepository jobRepository,
        IJobMatchingService jobMatchingService,
        ILogger<JobsController> logger)
    {
        _jobRepository = jobRepository;
        _jobMatchingService = jobMatchingService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<JobsResponseDto>> GetJobs(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var jobs = await _jobRepository.GetJobsAsync(pageNumber, pageSize);
        var totalCount = await _jobRepository.GetJobsCountAsync();

        var jobDtos = jobs.Select(job => new JobDto
        {
            Id = job.Id,
            JobTitle = job.JobTitle ?? "",
            BusinessName = job.BusinessName ?? "",
            WorkType = job.WorkType ?? "",
            JobType = job.JobType ?? "",
            PayRange = job.PayRange ?? "",
            Suburb = job.Suburb ?? "",
            Area = job.Area ?? "",
            Url = job.Url ?? "",
            Status = "New",
            PostedDate = job.PostedDate?.ToString("yyyy-MM-dd") ?? "",
            JobDescription = job.JobDescription ?? ""
        });

        return Ok(new JobsResponseDto
        {
            Jobs = jobDtos,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        });
    }

    [HttpGet("search")]
    public async Task<ActionResult<JobSearchResult>> SearchJobs([FromQuery] JobSearchParams searchParams)
    {
        try
        {
            _logger.LogInformation("Searching jobs with params: {@SearchParams}", searchParams);
            var result = await _jobMatchingService.SearchJobsAsync(searchParams);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching jobs");
            return StatusCode(500, new { message = "An error occurred while searching jobs" });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Job>> GetJob(int id)
    {
        var job = await _jobRepository.GetJobByIdAsync(id);
        if (job == null) return NotFound(new { message = $"Job with ID {id} not found" });

        return Ok(job);
    }

    [HttpPost]
    public async Task<ActionResult<Job>> CreateJob(Job job)
    {
        try
        {
            var createdJob = await _jobRepository.CreateJobAsync(job);
            return CreatedAtAction(nameof(GetJob), new { id = createdJob.Id }, createdJob);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job");
            return StatusCode(500, new { message = "An error occurred while creating the job" });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> UpdateJob(int id, Job job)
    {
        if (id != job.Id) return BadRequest(new { message = "ID mismatch" });

        try
        {
            await _jobRepository.UpdateJobAsync(job);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job");
            return StatusCode(500, new { message = "An error occurred while updating the job" });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteJob(int id)
    {
        try
        {
            await _jobRepository.DeleteJobAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting job");
            return StatusCode(500, new { message = "An error occurred while deleting the job" });
        }
    }

    [HttpPost("create")]
    public async Task<ActionResult<Job>> CreateJobManually([FromBody] CreateJobDto createJobDto)
    {
        try
        {
            _logger.LogInformation("Creating new job: {@JobDto}", createJobDto);

            var perthTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PerthTimeZone);

            var job = new Job
            {
                JobTitle = createJobDto.JobTitle ?? string.Empty,
                BusinessName = createJobDto.BusinessName ?? string.Empty,
                WorkType = createJobDto.WorkType ?? string.Empty,
                JobType = createJobDto.JobType ?? string.Empty,
                PayRange = createJobDto.PayRange ?? string.Empty,
                Suburb = createJobDto.Suburb ?? string.Empty,
                Area = createJobDto.Area ?? string.Empty,
                Url = createJobDto.Url ?? string.Empty,
                JobDescription = createJobDto.JobDescription ?? string.Empty,
                PostedDate = createJobDto.PostedDate ?? perthTime,
                CreatedAt = perthTime,
                UpdatedAt = perthTime,
                IsActive = true,
                IsNew = true
            };

            var createdJob = await _jobRepository.CreateJobAsync(job);

            _logger.LogInformation("Successfully created job with ID: {JobId}", createdJob.Id);

            return CreatedAtAction(
                nameof(GetJob),
                new { id = createdJob.Id },
                new { message = "Job created successfully", job = createdJob }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job manually");
            return StatusCode(500, new { message = "An error occurred while creating the job" });
        }
    }

    [HttpPut("{id:int}/status")]
    public async Task<ActionResult> UpdateJobStatus(int id, [FromBody] UpdateJobStatusDto updateDto)
    {
        try
        {
            var job = await _jobRepository.GetJobByIdAsync(id);
            if (job == null) return NotFound(new { message = $"Job with ID {id} not found" });

            job.IsActive = updateDto.IsActive;
            job.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PerthTimeZone);

            await _jobRepository.UpdateJobAsync(job);

            return Ok(new { message = "Job status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job status");
            return StatusCode(500, new { message = "An error occurred while updating the job status" });
        }
    }

    [HttpPut("{id:int}/mark-as-read")]
    public async Task<ActionResult> MarkJobAsRead(int id)
    {
        try
        {
            var job = await _jobRepository.GetJobByIdAsync(id);
            if (job == null) return NotFound(new { message = $"Job with ID {id} not found" });

            job.IsNew = false;
            job.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PerthTimeZone);

            await _jobRepository.UpdateJobAsync(job);

            return Ok(new { message = "Job marked as read successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking job as read");
            return StatusCode(500, new { message = "An error occurred while marking the job as read" });
        }
    }

    [HttpGet("new")]
    public async Task<ActionResult<JobsResponseDto>> GetNewJobs([FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var jobs = await _jobRepository.GetNewJobsAsync(pageNumber, pageSize);
        var totalCount = await _jobRepository.GetNewJobsCountAsync();

        var jobDtos = jobs.Select(j => new JobDto
        {
            Id = j.Id,
            JobTitle = j.JobTitle ?? string.Empty,
            BusinessName = j.BusinessName ?? string.Empty,
            WorkType = j.WorkType ?? string.Empty,
            JobType = j.JobType ?? string.Empty,
            PayRange = j.PayRange ?? string.Empty,
            MinSalary = j.MinSalary ?? 0,
            MaxSalary = j.MaxSalary ?? 0,
            Suburb = j.Suburb ?? string.Empty,
            Area = j.Area ?? string.Empty,
            Url = j.Url ?? string.Empty,
            IsNew = true,
            TechStack = j.TechStack ?? Array.Empty<string>(),
            PostedDate = j.PostedDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            JobDescription = j.JobDescription ?? string.Empty
        });

        var response = new JobsResponseDto
        {
            Jobs = jobDtos,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        return Ok(response);
    }

    [HttpGet("daily-counts")]
    public async Task<ActionResult<IEnumerable<DailyJobCount>>> GetDailyJobCounts([FromQuery] int days = 5)
    {
        try
        {
            var jobCounts = await _jobRepository.GetJobCountsByDateAsync(days);

            var result = jobCounts
                .Select(kv => new DailyJobCount
                {
                    Date = kv.Key.ToString("yyyy-MM-dd"),
                    Count = kv.Value
                })
                .ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily job counts");
            return StatusCode(500, new { message = "An error occurred while getting daily job counts" });
        }
    }

    [HttpGet("top-job-types")]
    public async Task<ActionResult<IEnumerable<JobTypeCount>>> GetTopJobTypes([FromQuery] int count = 5)
    {
        try
        {
            var topTypes = await _jobRepository.GetTopJobTypesAsync(count);

            // 如果没有数据，返回空列表而不是404
            if (!topTypes.Any()) return Ok(new List<JobTypeCount>());

            return Ok(topTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top job types");
            return StatusCode(500, new { message = "An error occurred while getting top job types" });
        }
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<JobStatisticsResponse>> GetJobStatistics([FromQuery] int days = 7)
    {
        try
        {
            // Validate days parameter
            if (days < 1 || days > 90) return BadRequest(new { message = "Days parameter must be between 1 and 90" });

            var statistics = await _jobRepository.GetJobStatisticsAsync(days);

            var response = new JobStatisticsResponse
            {
                DailyStatistics = statistics,
                Days = days
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job statistics");
            return StatusCode(500, new { message = "An error occurred while getting job statistics" });
        }
    }

    public class DailyJobCount
    {
        public string Date { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}

public class UpdateJobStatusDto
{
    public bool IsActive { get; set; }
}