using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JobTracker.Controllers;
using JobTracker.Models;
using JobTracker.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace JobTracker.Tests.Controllers;

public class UserJobsControllerTests
{
    private readonly UserJobsController _controller;
    private readonly Mock<IJobRepository> _mockJobRepo;
    private readonly Mock<IUserJobRepository> _mockUserJobRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Guid _testUserId = Guid.NewGuid();

    public UserJobsControllerTests()
    {
        _mockUserJobRepo = new Mock<IUserJobRepository>();
        _mockJobRepo = new Mock<IJobRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _controller = new UserJobsController(_mockUserJobRepo.Object, _mockUserRepo.Object, _mockJobRepo.Object);

        // 创建一个有效的JWT token
        var token = CreateTestToken(_testUserId);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {token}";

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private string CreateTestToken(Guid userId)
    {
        var securityKey =
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your-test-secret-key-with-sufficient-length"));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var token = new JwtSecurityToken(
            "test-issuer",
            "test-audience",
            claims,
            expires: DateTime.Now.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task GetDailyApplicationCounts_ReturnsCorrectData()
    {
        // Arrange
        var testData = new List<(DateTime Date, int Count)>
        {
            (DateTime.UtcNow.Date.AddDays(-2), 3),
            (DateTime.UtcNow.Date.AddDays(-1), 2),
            (DateTime.UtcNow.Date, 1)
        };

        _mockUserJobRepo.Setup(repo => repo.GetDailyApplicationCountsAsync(_testUserId, 7))
            .ReturnsAsync(testData);

        // Act
        var result = await _controller.GetDailyApplicationCounts();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnValue = Assert.IsAssignableFrom<IEnumerable<DailyApplicationCountDto>>(okResult.Value);
        Assert.Equal(3, returnValue.Count());
        Assert.Equal(3, returnValue.First().Count);
    }

    [Fact]
    public async Task GetCumulativeStatusCounts_ReturnsCorrectData()
    {
        // Arrange
        var testData = new CumulativeStatusCountDto
        {
            Applied = 10,
            Reviewed = 8,
            Interviewing = 5,
            TechnicalAssessment = 3,
            Offered = 0
        };

        _mockUserJobRepo.Setup(repo => repo.GetCumulativeStatusCountsAsync(_testUserId))
            .ReturnsAsync(testData);

        // Act
        var result = await _controller.GetCumulativeStatusCounts();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnValue = Assert.IsType<CumulativeStatusCountDto>(okResult.Value);
        Assert.Equal(10, returnValue.Applied);
        Assert.Equal(8, returnValue.Reviewed);
        Assert.Equal(5, returnValue.Interviewing);
        Assert.Equal(3, returnValue.TechnicalAssessment);
        Assert.Equal(0, returnValue.Offered);
    }

    [Fact]
    public async Task GetWorkTypeCounts_ReturnsCorrectData()
    {
        // Arrange
        var testData = new List<WorkTypeCountDto>
        {
            new() { WorkType = "Full Time", Count = 5 },
            new() { WorkType = "Part Time", Count = 3 },
            new() { WorkType = "Contract", Count = 2 }
        };

        _mockUserJobRepo.Setup(repo => repo.GetWorkTypeCountsAsync(_testUserId))
            .ReturnsAsync(testData);

        // Act
        var result = await _controller.GetWorkTypeCounts();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnValue = Assert.IsAssignableFrom<IEnumerable<WorkTypeCountDto>>(okResult.Value);
        Assert.Equal(3, returnValue.Count());
        Assert.Equal(5, returnValue.First().Count);
        Assert.Equal("Full Time", returnValue.First().WorkType);
    }

    [Fact]
    public async Task GetWorkTypeCounts_ReturnsEmptyList_WhenNoData()
    {
        // Arrange
        _mockUserJobRepo.Setup(repo => repo.GetWorkTypeCountsAsync(_testUserId))
            .ReturnsAsync(new List<WorkTypeCountDto>());

        // Act
        var result = await _controller.GetWorkTypeCounts();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnValue = Assert.IsAssignableFrom<IEnumerable<WorkTypeCountDto>>(okResult.Value);
        Assert.Empty(returnValue);
    }

    [Fact]
    public async Task GetSuburbCounts_ReturnsCorrectData()
    {
        // Arrange
        var testData = new List<SuburbCountDto>
        {
            new() { Suburb = "Perth CBD", Count = 5 },
            new() { Suburb = "Subiaco", Count = 3 },
            new() { Suburb = "Osborne Park", Count = 2 }
        };

        _mockUserJobRepo.Setup(repo => repo.GetSuburbCountsAsync(_testUserId))
            .ReturnsAsync(testData);

        // Act
        var result = await _controller.GetSuburbCounts();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnValue = Assert.IsAssignableFrom<IEnumerable<SuburbCountDto>>(okResult.Value);
        Assert.Equal(3, returnValue.Count());
        Assert.Equal(5, returnValue.First().Count);
        Assert.Equal("Perth CBD", returnValue.First().Suburb);
    }

    [Fact]
    public async Task GetSuburbCounts_ReturnsEmptyList_WhenNoData()
    {
        // Arrange
        _mockUserJobRepo.Setup(repo => repo.GetSuburbCountsAsync(_testUserId))
            .ReturnsAsync(new List<SuburbCountDto>());

        // Act
        var result = await _controller.GetSuburbCounts();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnValue = Assert.IsAssignableFrom<IEnumerable<SuburbCountDto>>(okResult.Value);
        Assert.Empty(returnValue);
    }
}