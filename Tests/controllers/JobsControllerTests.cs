using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JobTracker.Controllers;
using JobTracker.Models;
using JobTracker.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace JobTracker.Tests.Controllers
{
    public class JobsControllerTests
    {
        private readonly Mock<IJobRepository> _mockRepo;
        private readonly JobsController _controller;

        public JobsControllerTests()
        {
            _mockRepo = new Mock<IJobRepository>();
            _controller = new JobsController(_mockRepo.Object);
        }

        [Fact]
        public async Task GetJobs_ReturnsOkResult_WithListOfJobs()
        {
            // Arrange
            var jobs = new List<Job>
            {
                new Job { Id = Guid.NewGuid(), JobTitle = "Software Developer" },
                new Job { Id = Guid.NewGuid(), JobTitle = "Data Analyst" }
            };
            _mockRepo.Setup(repo => repo.GetAllJobsAsync()).ReturnsAsync(jobs);

            // Act
            var result = await _controller.GetJobs();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedJobs = Assert.IsAssignableFrom<IEnumerable<Job>>(okResult.Value);
            Assert.Equal(2, returnedJobs.Count());
        }

        [Fact]
        public async Task GetJob_ReturnsOkResult_WhenJobExists()
        {
            // Arrange
            var jobId = Guid.NewGuid();
            var job = new Job { Id = jobId, JobTitle = "Test Job" };
            _mockRepo.Setup(repo => repo.GetJobByIdAsync(jobId)).ReturnsAsync(job);

            // Act
            var result = await _controller.GetJob(jobId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedJob = Assert.IsType<Job>(okResult.Value);
            Assert.Equal(jobId, returnedJob.Id);
        }

        [Fact]
        public async Task GetJob_ReturnsNotFound_WhenJobDoesNotExist()
        {
            // Arrange
            var jobId = Guid.NewGuid();
            _mockRepo.Setup(repo => repo.GetJobByIdAsync(jobId)).ReturnsAsync((Job)null);

            // Act
            var result = await _controller.GetJob(jobId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreateJob_ReturnsCreatedAtAction_WithNewJob()
        {
            // Arrange
            var newJob = new Job { JobTitle = "New Job" };
            var createdJob = new Job { Id = Guid.NewGuid(), JobTitle = "New Job" };
            _mockRepo.Setup(repo => repo.CreateJobAsync(newJob)).ReturnsAsync(createdJob);

            // Act
            var result = await _controller.CreateJob(newJob);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(nameof(JobsController.GetJob), createdAtActionResult.ActionName);
            Assert.Equal(createdJob.Id, createdAtActionResult.RouteValues["id"]);
            var returnedJob = Assert.IsType<Job>(createdAtActionResult.Value);
            Assert.Equal(createdJob.Id, returnedJob.Id);
        }

        [Fact]
        public async Task UpdateJob_ReturnsNoContent_WhenUpdateIsSuccessful()
        {
            // Arrange
            var jobId = Guid.NewGuid();
            var job = new Job { Id = jobId, JobTitle = "Updated Job" };
            _mockRepo.Setup(repo => repo.UpdateJobAsync(job)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateJob(jobId, job);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateJob_ReturnsBadRequest_WhenIdsMismatch()
        {
            // Arrange
            var jobId = Guid.NewGuid();
            var job = new Job { Id = Guid.NewGuid(), JobTitle = "Updated Job" };

            // Act
            var result = await _controller.UpdateJob(jobId, job);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task DeleteJob_ReturnsNoContent_WhenDeleteIsSuccessful()
        {
            // Arrange
            var jobId = Guid.NewGuid();
            _mockRepo.Setup(repo => repo.DeleteJobAsync(jobId)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteJob(jobId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }
    }
}