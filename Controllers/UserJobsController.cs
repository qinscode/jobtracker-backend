using JobTracker.Models;
using JobTracker.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Controllers
{
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
        public async Task<ActionResult<IEnumerable<UserJob>>> GetUserJobs()
        {
            var userJobs = await _userJobRepository.GetAllUserJobsAsync();
            return Ok(userJobs);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserJob>> GetUserJob(Guid id)
        {
            var userJob = await _userJobRepository.GetUserJobByIdAsync(id);
            if (userJob == null)
            {
                return NotFound();
            }
            return Ok(userJob);
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
            if (id != userJob.Id)
            {
                return BadRequest();
            }

            await _userJobRepository.UpdateUserJobAsync(userJob);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUserJob(Guid id)
        {
            await _userJobRepository.DeleteUserJobAsync(id);
            return NoContent();
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateUserJobStatus(Guid id, [FromBody] UserJobStatus newStatus)
        {
            var userJob = await _userJobRepository.GetUserJobByIdAsync(id);
            if (userJob == null)
            {
                return NotFound();
            }

            userJob.Status = newStatus;
            await _userJobRepository.UpdateUserJobAsync(userJob);
            return NoContent();
        }
    }
}