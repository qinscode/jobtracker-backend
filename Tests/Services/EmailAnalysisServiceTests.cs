using JobTracker.Models;
using JobTracker.Repositories;
using JobTracker.Services;
using JobTracker.Services.Interfaces;
using Moq;
using Xunit;

namespace JobTracker.Tests.Services;

public class EmailAnalysisServiceTests
{
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IAIAnalysisService> _aiAnalysisServiceMock;
    private readonly Mock<IJobMatchingService> _jobMatchingServiceMock;
    private readonly Mock<IJobRepository> _jobRepositoryMock;
    private readonly Mock<IUserJobRepository> _userJobRepositoryMock;
    private readonly Mock<IAnalyzedEmailRepository> _analyzedEmailRepositoryMock;
    private readonly Mock<ILogger<EmailAnalysisService>> _loggerMock;
    private readonly EmailAnalysisService _service;

    public EmailAnalysisServiceTests()
    {
        _emailServiceMock = new Mock<IEmailService>();
        _aiAnalysisServiceMock = new Mock<IAIAnalysisService>();
        _jobMatchingServiceMock = new Mock<IJobMatchingService>();
        _jobRepositoryMock = new Mock<IJobRepository>();
        _userJobRepositoryMock = new Mock<IUserJobRepository>();
        _analyzedEmailRepositoryMock = new Mock<IAnalyzedEmailRepository>();
        _loggerMock = new Mock<ILogger<EmailAnalysisService>>();

        _service = new EmailAnalysisService(
            _emailServiceMock.Object,
            _aiAnalysisServiceMock.Object,
            _jobMatchingServiceMock.Object,
            _jobRepositoryMock.Object,
            _userJobRepositoryMock.Object,
            _analyzedEmailRepositoryMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task AnalyzeRecentEmails_WhenJobExistsWithoutUserJob_ShouldCreateUserJob()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var email = new EmailMessage
        {
            MessageId = "test1",
            Subject = "Test Email",
            Body = "Test Body",
            ReceivedDate = DateTime.UtcNow,
            Uid = 1
        };

        _emailServiceMock.Setup(x => x.FetchRecentEmailsAsync(config))
            .ReturnsAsync(new[] { email });

        var existingJob = new Job { Id = 1, JobTitle = "Developer", BusinessName = "Test Company" };

        _aiAnalysisServiceMock.Setup(x => x.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync(("Test Company", "Developer", UserJobStatus.TechnicalAssessment));

        _jobMatchingServiceMock.Setup(x => x.FindMatchingJobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, existingJob, 0.9));

        _userJobRepositoryMock.Setup(x => x.GetUserJobByUserIdAndJobIdAsync(config.UserId, existingJob.Id))
            .ReturnsAsync((UserJob?)null);

        // Act
        var result = await _service.AnalyzeRecentEmails(config);

        // Assert
        _userJobRepositoryMock.Verify(x => x.CreateUserJobAsync(
            It.Is<UserJob>(uj =>
                uj.UserId == config.UserId &&
                uj.JobId == existingJob.Id &&
                uj.Status == UserJobStatus.TechnicalAssessment)
        ), Times.Once);
    }

    [Fact]
    public async Task AnalyzeRecentEmails_WhenJobExistsWithUserJob_ShouldUpdateStatus()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var email = new EmailMessage
        {
            MessageId = "test2",
            Subject = "Test Email",
            Body = "Test Body",
            ReceivedDate = DateTime.UtcNow,
            Uid = 2
        };

        _emailServiceMock.Setup(x => x.FetchRecentEmailsAsync(config))
            .ReturnsAsync(new[] { email });

        var existingJob = new Job { Id = 1, JobTitle = "Developer", BusinessName = "Test Company" };
        var existingUserJob = new UserJob
        {
            UserId = config.UserId,
            JobId = existingJob.Id,
            Status = UserJobStatus.Applied
        };

        _aiAnalysisServiceMock.Setup(x => x.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync(("Test Company", "Developer", UserJobStatus.Interviewing));

        _jobMatchingServiceMock.Setup(x => x.FindMatchingJobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, existingJob, 0.9));

        _userJobRepositoryMock.Setup(x => x.GetUserJobByUserIdAndJobIdAsync(config.UserId, existingJob.Id))
            .ReturnsAsync(existingUserJob);

        // Act
        var result = await _service.AnalyzeRecentEmails(config);

        // Assert
        _userJobRepositoryMock.Verify(x => x.UpdateUserJobAsync(
            It.Is<UserJob>(uj =>
                uj.Status == UserJobStatus.Interviewing)
        ), Times.Once);
    }

    [Fact]
    public async Task AnalyzeRecentEmails_WhenJobDoesNotExist_ShouldCreateJobAndUserJob()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var email = new EmailMessage
        {
            MessageId = "test3",
            Subject = "Test Email",
            Body = "Test Body",
            ReceivedDate = DateTime.UtcNow,
            Uid = 3
        };

        _emailServiceMock.Setup(x => x.FetchRecentEmailsAsync(config))
            .ReturnsAsync(new[] { email });

        var newJob = new Job { Id = 1, JobTitle = "Developer", BusinessName = "New Company" };

        _aiAnalysisServiceMock.Setup(x => x.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync(("New Company", "Developer", UserJobStatus.Applied));

        _jobMatchingServiceMock.Setup(x => x.FindMatchingJobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, null, 0.0));

        _jobRepositoryMock.Setup(x => x.CreateJobAsync(It.IsAny<Job>()))
            .ReturnsAsync(newJob);

        // Act
        var result = await _service.AnalyzeRecentEmails(config);

        // Assert
        _jobRepositoryMock.Verify(x => x.CreateJobAsync(It.IsAny<Job>()), Times.Once);
        _userJobRepositoryMock.Verify(x => x.CreateUserJobAsync(
            It.Is<UserJob>(uj =>
                uj.UserId == config.UserId &&
                uj.JobId == newJob.Id &&
                uj.Status == UserJobStatus.Applied)
        ), Times.Once);
    }

    [Fact]
    public async Task AnalyzeRecentEmails_WhenStatusIsRejected_ShouldAlwaysUpdateStatus()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var email = new EmailMessage
        {
            MessageId = "test4",
            Subject = "Test Email",
            Body = "Test Body",
            ReceivedDate = DateTime.UtcNow,
            Uid = 4
        };

        _emailServiceMock.Setup(x => x.FetchRecentEmailsAsync(config))
            .ReturnsAsync(new[] { email });

        var existingJob = new Job { Id = 1, JobTitle = "Developer", BusinessName = "Test Company" };
        var existingUserJob = new UserJob
        {
            UserId = config.UserId,
            JobId = existingJob.Id,
            Status = UserJobStatus.Interviewing // 即使是高级状态
        };

        _aiAnalysisServiceMock.Setup(x => x.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync(("Test Company", "Developer", UserJobStatus.Rejected));

        _jobMatchingServiceMock.Setup(x => x.FindMatchingJobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, existingJob, 0.9));

        _userJobRepositoryMock.Setup(x => x.GetUserJobByUserIdAndJobIdAsync(config.UserId, existingJob.Id))
            .ReturnsAsync(existingUserJob);

        // Act
        var result = await _service.AnalyzeRecentEmails(config);

        // Assert
        _userJobRepositoryMock.Verify(x => x.UpdateUserJobAsync(
            It.Is<UserJob>(uj =>
                uj.Status == UserJobStatus.Rejected)
        ), Times.Once);
    }

    [Fact]
    public async Task AnalyzeRecentEmails_WhenEmailAlreadyAnalyzed_ShouldSkip()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var email = new EmailMessage
        {
            MessageId = "test5",
            Subject = "Test Email",
            Body = "Test Body",
            ReceivedDate = DateTime.UtcNow,
            Uid = 5
        };

        _emailServiceMock.Setup(x => x.FetchRecentEmailsAsync(config))
            .ReturnsAsync(new[] { email });

        _analyzedEmailRepositoryMock.Setup(x => x.ExistsAsync(config.Id, email.MessageId))
            .ReturnsAsync(true);

        // Act
        var result = await _service.AnalyzeRecentEmails(config);

        // Assert
        _aiAnalysisServiceMock.Verify(x => x.ExtractJobInfo(It.IsAny<string>()), Times.Never);
        Assert.Empty(result);
    }

    [Fact]
    public async Task AnalyzeRecentEmails_WhenNoCompanyName_ShouldSkipJobProcessing()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var email = new EmailMessage
        {
            MessageId = "test6",
            Subject = "Test Email",
            Body = "Test Body",
            ReceivedDate = DateTime.UtcNow,
            Uid = 6
        };

        _emailServiceMock.Setup(x => x.FetchRecentEmailsAsync(config))
            .ReturnsAsync(new[] { email });

        _aiAnalysisServiceMock.Setup(x => x.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync(("", "Developer", UserJobStatus.Applied));

        // Act
        var result = await _service.AnalyzeRecentEmails(config);

        // Assert
        _jobMatchingServiceMock.Verify(x => x.FindMatchingJobAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.Single(result);
        Assert.False(result[0].IsRecognized);
    }
}