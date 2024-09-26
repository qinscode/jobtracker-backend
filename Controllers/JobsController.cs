using JobTracker.Models;
using JobTracker.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly IJobRepository _jobRepository;

        public JobsController(IJobRepository jobRepository)
        {
            _jobRepository = jobRepository;
        }

        [HttpGet]
        public async Task<ActionResult<JobsResponseDto>> GetJobs([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var jobs = await _jobRepository.GetJobsAsync(pageNumber, pageSize);
            var totalCount = await _jobRepository.GetJobsCountAsync();

            if (totalCount == 0)
            {
                return NotFound(new { message = "No jobs found" });
            }

            var response = new JobsResponseDto
            {
                Jobs = jobs.Select(j => new JobDto
                {
                    Id = j.Id,
                    JobTitle = j.JobTitle,
                    BusinessName = j.BusinessName,
                    WorkType = j.WorkType,
                    JobType = j.JobType,
                    PayRange = j.PayRange,
                    Suburb = j.Suburb,
                    Area = j.Area,
                    Url = j.Url,
                    PostedDate = j.PostedDate,
                    AdvertiserName = j.Advertiser?.Name,
                    CreatedAt = j.CreatedAt,
                    UpdatedAt = j.UpdatedAt,
                    // IsNew is not set here
                }),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<JobDto>> GetJob(Guid id)
        {
            var job = await _jobRepository.GetJobByIdAsync(id);
            if (job == null)
            {
                return NotFound(new { message = $"Job with id {id} not found" });
            }
            var jobDto = new JobDto
            {
                Id = job.Id,
                JobTitle = job.JobTitle,
                BusinessName = job.BusinessName,
                WorkType = job.WorkType,
                JobType = job.JobType,
                PayRange = job.PayRange,
                Suburb = job.Suburb,
                Area = job.Area,
                Url = job.Url,
                PostedDate = job.PostedDate,
                AdvertiserName = job.Advertiser?.Name,
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt,
                // IsNew is not set here
            };
            return Ok(jobDto);
        }

        [HttpPost]
        public async Task<ActionResult<Job>> CreateJob(Job job)
        {
            var createdJob = await _jobRepository.CreateJobAsync(job);
            return CreatedAtAction(nameof(GetJob), new { id = createdJob.Id }, createdJob);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateJob(Guid id, Job job)
        {
            if (id != job.Id)
            {
                return BadRequest(new { message = "Id in URL does not match Id in request body" });
            }

            var existingJob = await _jobRepository.GetJobByIdAsync(id);
            if (existingJob == null)
            {
                return NotFound(new { message = $"Job with id {id} not found" });
            }

            await _jobRepository.UpdateJobAsync(job);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteJob(Guid id)
        {
            var existingJob = await _jobRepository.GetJobByIdAsync(id);
            if (existingJob == null)
            {
                return NotFound(new { message = $"Job with id {id} not found" });
            }

            await _jobRepository.DeleteJobAsync(id);
            return NoContent();
        }

        [HttpGet("new")]
        public async Task<ActionResult<JobsResponseDto>> GetNewJobs([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var jobs = await _jobRepository.GetNewJobsAsync(pageNumber, pageSize);
            var totalCount = await _jobRepository.GetNewJobsCountAsync();

            if (totalCount == 0)
            {
                return NotFound(new { message = "No new jobs found" });
            }

            var response = new JobsResponseDto
            {
                Jobs = jobs.Select(j => new JobDto
                {
                    Id = j.Id,
                    JobTitle = j.JobTitle,
                    BusinessName = j.BusinessName,
                    WorkType = j.WorkType,
                    JobType = j.JobType,
                    PayRange = j.PayRange,
                    Suburb = j.Suburb,
                    Area = j.Area,
                    Url = j.Url,
                    PostedDate = j.PostedDate,
                    AdvertiserName = j.Advertiser?.Name,
                    CreatedAt = j.CreatedAt,
                    UpdatedAt = j.UpdatedAt,
                    IsNew = true // All jobs returned by this endpoint are new
                }),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Ok(response);
        }
    }
}