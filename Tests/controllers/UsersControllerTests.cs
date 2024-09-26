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
    public class UsersControllerTests
    {
        private readonly Mock<IUserRepository> _mockRepo;
        private readonly UsersController _controller;

        public UsersControllerTests()
        {
            _mockRepo = new Mock<IUserRepository>();
            _controller = new UsersController(_mockRepo.Object);
        }

        [Fact]
        public async Task GetUsers_ReturnsOkResult_WithListOfUsers()
        {
            // Arrange
            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), Username = "user1" },
                new User { Id = Guid.NewGuid(), Username = "user2" }
            };
            _mockRepo.Setup(repo => repo.GetAllUsersAsync()).ReturnsAsync(users);

            // Act
            var result = await _controller.GetUsers();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedUsers = Assert.IsAssignableFrom<IEnumerable<User>>(okResult.Value);
            Assert.Equal(2, returnedUsers.Count());
        }

        [Fact]
        public async Task GetUser_ReturnsOkResult_WhenUserExists()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User { Id = userId, Username = "testuser" };
            _mockRepo.Setup(repo => repo.GetUserByIdAsync(userId)).ReturnsAsync(user);

            // Act
            var result = await _controller.GetUser(userId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedUser = Assert.IsType<User>(okResult.Value);
            Assert.Equal(userId, returnedUser.Id);
        }

        [Fact]
        public async Task GetUser_ReturnsNotFound_WhenUserDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockRepo.Setup(repo => repo.GetUserByIdAsync(userId)).ReturnsAsync((User)null);

            // Act
            var result = await _controller.GetUser(userId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreateUser_ReturnsCreatedAtAction_WithNewUser()
        {
            // Arrange
            var newUser = new User { Username = "newuser", PasswordHash = "validpasswordhash" };
            var createdUser = new User { Id = Guid.NewGuid(), Username = "newuser" };
            _mockRepo.Setup(repo => repo.CreateUserAsync(newUser)).ReturnsAsync(createdUser);

            // Act
            var result = await _controller.CreateUser(newUser);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(nameof(UsersController.GetUser), createdAtActionResult.ActionName);
            Assert.Equal(createdUser.Id, createdAtActionResult.RouteValues["id"]);
            var returnedUser = Assert.IsType<User>(createdAtActionResult.Value);
            Assert.Equal(createdUser.Id, returnedUser.Id);
        }
        [Fact]
        public async Task UpdateUser_ReturnsNoContent_WhenUpdateIsSuccessful()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User { Id = userId, Username = "updateduser" };
            _mockRepo.Setup(repo => repo.UpdateUserAsync(userId, user)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateUser(userId, user);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateUser_ReturnsBadRequest_WhenIdsMismatch()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User { Id = Guid.NewGuid(), Username = "updateduser" };

            // Act
            var result = await _controller.UpdateUser(userId, user);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task DeleteUser_ReturnsNoContent_WhenDeleteIsSuccessful()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockRepo.Setup(repo => repo.DeleteUserAsync(userId)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteUser(userId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }
    }
}