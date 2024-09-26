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
    public class CompaniesControllerTests
    {
        private readonly Mock<ICompanyRepository> _mockRepo;
        private readonly CompaniesController _controller;

        public CompaniesControllerTests()
        {
            _mockRepo = new Mock<ICompanyRepository>();
            _controller = new CompaniesController(_mockRepo.Object);
        }

        [Fact]
        public async Task GetCompanies_ReturnsOkResult_WithListOfCompanies()
        {
            // Arrange
            var companies = new List<Company>
            {
                new Company { Id = Guid.NewGuid(), Name = "Company 1" },
                new Company { Id = Guid.NewGuid(), Name = "Company 2" }
            };
            _mockRepo.Setup(repo => repo.GetAllCompaniesAsync()).ReturnsAsync(companies);

            // Act
            var result = await _controller.GetCompanies();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedCompanies = Assert.IsAssignableFrom<IEnumerable<Company>>(okResult.Value);
            Assert.Equal(2, returnedCompanies.Count());
        }

        [Fact]
        public async Task GetCompany_ReturnsOkResult_WhenCompanyExists()
        {
            // Arrange
            var companyId = Guid.NewGuid();
            var company = new Company { Id = companyId, Name = "Test Company" };
            _mockRepo.Setup(repo => repo.GetCompanyByIdAsync(companyId)).ReturnsAsync(company);

            // Act
            var result = await _controller.GetCompany(companyId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedCompany = Assert.IsType<Company>(okResult.Value);
            Assert.Equal(companyId, returnedCompany.Id);
        }

        [Fact]
        public async Task GetCompany_ReturnsNotFound_WhenCompanyDoesNotExist()
        {
            // Arrange
            var companyId = Guid.NewGuid();
            _mockRepo.Setup(repo => repo.GetCompanyByIdAsync(companyId)).ReturnsAsync((Company)null);

            // Act
            var result = await _controller.GetCompany(companyId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreateCompany_ReturnsCreatedAtAction_WithNewCompany()
        {
            // Arrange
            var newCompany = new Company { Name = "New Company" };
            var createdCompany = new Company { Id = Guid.NewGuid(), Name = "New Company" };
            _mockRepo.Setup(repo => repo.CreateCompanyAsync(newCompany)).ReturnsAsync(createdCompany);

            // Act
            var result = await _controller.CreateCompany(newCompany);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(nameof(CompaniesController.GetCompany), createdAtActionResult.ActionName);
            Assert.Equal(createdCompany.Id, createdAtActionResult.RouteValues["id"]);
            var returnedCompany = Assert.IsType<Company>(createdAtActionResult.Value);
            Assert.Equal(createdCompany.Id, returnedCompany.Id);
        }

        [Fact]
        public async Task UpdateCompany_ReturnsNoContent_WhenUpdateIsSuccessful()
        {
            // Arrange
            var companyId = Guid.NewGuid();
            var company = new Company { Id = companyId, Name = "Updated Company" };
            _mockRepo.Setup(repo => repo.UpdateCompanyAsync(company)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateCompany(companyId, company);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateCompany_ReturnsBadRequest_WhenIdsMismatch()
        {
            // Arrange
            var companyId = Guid.NewGuid();
            var company = new Company { Id = Guid.NewGuid(), Name = "Updated Company" };

            // Act
            var result = await _controller.UpdateCompany(companyId, company);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task DeleteCompany_ReturnsNoContent_WhenDeleteIsSuccessful()
        {
            // Arrange
            var companyId = Guid.NewGuid();
            _mockRepo.Setup(repo => repo.DeleteCompanyAsync(companyId)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteCompany(companyId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }
    }
}