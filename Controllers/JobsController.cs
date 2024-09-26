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
        public async Task<ActionResult<IEnumerable<Job>>> GetJobs()
        {
            var jobs = await _jobRepository.GetAllJobsAsync();
            return Ok(jobs);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Job>> GetJob(Guid id)
        {
            var job = await _jobRepository.GetJobByIdAsync(id);
            if (job == null)
            {
                return NotFound();
            }
            return Ok(job);
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
                return BadRequest();
            }

            await _jobRepository.UpdateJobAsync(job);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteJob(Guid id)
        {
            await _jobRepository.DeleteJobAsync(id);
            return NoContent();
        }
    }
}