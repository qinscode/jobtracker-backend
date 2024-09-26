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
    public class UserJobsControllerTests
    {
        private readonly Mock<IUserJobRepository> _mockRepo;
        private readonly UserJobsController _controller;

        public UserJobsControllerTests()
        {
            _mockRepo = new Mock<IUserJobRepository>();
            _controller = new UserJobsController(_mockRepo.Object);
        }

        [Fact]
        public async Task GetUserJobs_ReturnsOkResult_WithListOfUserJobs()
        {
            // Arrange
            var userJobs = new List<UserJob>
            {
                new UserJob { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), JobId = Guid.NewGuid() },
                new UserJob { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), JobId = Guid.NewGuid() }
            };
            _mockRepo.Setup(repo => repo.GetAllUserJobsAsync()).ReturnsAsync(userJobs);

            // Act
            var result = await _controller.GetUserJobs();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedUserJobs = Assert.IsAssignableFrom<IEnumerable<UserJob>>(okResult.Value);
            Assert.Equal(2, returnedUserJobs.Count());
        }

        [Fact]
        public async Task GetUserJob_ReturnsOkResult_WhenUserJobExists()
        {
            // Arrange
            var userJobId = Guid.NewGuid();
            var userJob = new UserJob { Id = userJobId, UserId = Guid.NewGuid(), JobId = Guid.NewGuid() };
            _mockRepo.Setup(repo => repo.GetUserJobByIdAsync(userJobId)).ReturnsAsync(userJob);

            // Act
            var result = await _controller.GetUserJob(userJobId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedUserJob = Assert.IsType<UserJob>(okResult.Value);
            Assert.Equal(userJobId, returnedUserJob.Id);
        }

        [Fact]
        public async Task GetUserJob_ReturnsNotFound_WhenUserJobDoesNotExist()
        {
            // Arrange
            var userJobId = Guid.NewGuid();
            _mockRepo.Setup(repo => repo.GetUserJobByIdAsync(userJobId)).ReturnsAsync((UserJob)null);

            // Act
            var result = await _controller.GetUserJob(userJobId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreateUserJob_ReturnsCreatedAtAction_WithNewUserJob()
        {
            // Arrange
            var newUserJob = new UserJob { UserId = Guid.NewGuid(), JobId = Guid.NewGuid() };
            var createdUserJob = new UserJob { Id = Guid.NewGuid(), UserId = newUserJob.UserId, JobId = newUserJob.JobId };
            _mockRepo.Setup(repo => repo.CreateUserJobAsync(newUserJob)).ReturnsAsync(createdUserJob);

            // Act
            var result = await _controller.CreateUserJob(newUserJob);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(nameof(UserJobsController.GetUserJob), createdAtActionResult.ActionName);
            Assert.Equal(createdUserJob.Id, createdAtActionResult.RouteValues["id"]);
            var returnedUserJob = Assert.IsType<UserJob>(createdAtActionResult.Value);
            Assert.Equal(createdUserJob.Id, returnedUserJob.Id);
        }

        [Fact]
        public async Task UpdateUserJob_ReturnsNoContent_WhenUpdateIsSuccessful()
        {
            // Arrange
            var userJobId = Guid.NewGuid();
            var userJob = new UserJob { Id = userJobId, UserId = Guid.NewGuid(), JobId = Guid.NewGuid() };
            _mockRepo.Setup(repo => repo.UpdateUserJobAsync(userJob)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateUserJob(userJobId, userJob);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateUserJob_ReturnsBadRequest_WhenIdsMismatch()
        {
            // Arrange
            var userJobId = Guid.NewGuid();
            var userJob = new UserJob { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), JobId = Guid.NewGuid() };

            // Act
            var result = await _controller.UpdateUserJob(userJobId, userJob);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task DeleteUserJob_ReturnsNoContent_WhenDeleteIsSuccessful()
        {
            // Arrange
            var userJobId = Guid.NewGuid();
            _mockRepo.Setup(repo => repo.DeleteUserJobAsync(userJobId)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteUserJob(userJobId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateUserJobStatus_ReturnsNoContent_WhenUpdateIsSuccessful()
        {
            // Arrange
            var userJobId = Guid.NewGuid();
            var newStatus = UserJobStatus.Applied;
            var userJob = new UserJob { Id = userJobId, UserId = Guid.NewGuid(), JobId = Guid.NewGuid(), Status = UserJobStatus.New };
            _mockRepo.Setup(repo => repo.GetUserJobByIdAsync(userJobId)).ReturnsAsync(userJob);
            _mockRepo.Setup(repo => repo.UpdateUserJobAsync(It.IsAny<UserJob>())).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateUserJobStatus(userJobId, newStatus);

            // Assert
            Assert.IsType<NoContentResult>(result);
            Assert.Equal(newStatus, userJob.Status);
        }

        [Fact]
        public async Task UpdateUserJobStatus_ReturnsNotFound_WhenUserJobDoesNotExist()
        {
            // Arrange
            var userJobId = Guid.NewGuid();
            var newStatus = UserJobStatus.Applied;
            _mockRepo.Setup(repo => repo.GetUserJobByIdAsync(userJobId)).ReturnsAsync((UserJob)null);

            // Act
            var result = await _controller.UpdateUserJobStatus(userJobId, newStatus);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}