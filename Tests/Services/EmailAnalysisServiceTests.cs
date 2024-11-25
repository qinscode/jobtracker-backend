using JobTracker.Models;
using JobTracker.Repositories;
using JobTracker.Services;
using JobTracker.Services.Interfaces;
using Moq;
using Xunit;

namespace JobTracker.Tests.Services;

public class EmailAnalysisServiceTests
{
    private readonly Mock<IAIAnalysisService> _aiAnalysisServiceMock;
    private readonly Mock<IAnalyzedEmailRepository> _analyzedEmailRepositoryMock;
    private readonly EmailAnalysisService _emailAnalysisService;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IJobMatchingService> _jobMatchingServiceMock;
    private readonly Mock<IJobRepository> _jobRepositoryMock;
    private readonly Mock<ILogger<EmailAnalysisService>> _loggerMock;
    private readonly Mock<IUserJobRepository> _userJobRepositoryMock;

    public EmailAnalysisServiceTests()
    {
        _emailServiceMock = new Mock<IEmailService>();
        _aiAnalysisServiceMock = new Mock<IAIAnalysisService>();
        _jobMatchingServiceMock = new Mock<IJobMatchingService>();
        _jobRepositoryMock = new Mock<IJobRepository>();
        _userJobRepositoryMock = new Mock<IUserJobRepository>();
        _analyzedEmailRepositoryMock = new Mock<IAnalyzedEmailRepository>();
        _loggerMock = new Mock<ILogger<EmailAnalysisService>>();

        _emailAnalysisService = new EmailAnalysisService(
            _emailServiceMock.Object,
            _aiAnalysisServiceMock.Object,
            _jobMatchingServiceMock.Object,
            _jobRepositoryMock.Object,
            _userJobRepositoryMock.Object,
            _analyzedEmailRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task AnalyzeRecentEmails_ShouldProcessEmailsCorrectly()
    {
        // Arrange
        var config = new UserEmailConfig
            { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), EmailAddress = "test@example.com" };

        var emails = new List<EmailMessage>
        {
            new()
            {
                MessageId = "1", Subject = "Job Application", Body = "Test email content",
                ReceivedDate = DateTime.UtcNow
            }
        };

        _emailServiceMock.Setup(service => service.FetchRecentEmailsAsync(config))
            .ReturnsAsync(emails);

        _analyzedEmailRepositoryMock.Setup(repo => repo.ExistsAsync(config.Id, It.IsAny<string>()))
            .ReturnsAsync(false);

        _aiAnalysisServiceMock.Setup(service => service.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync(("Test Company", "Test Job", UserJobStatus.Applied, new List<string> { "key phrase" },
                "Suggested action"));

        _jobMatchingServiceMock.Setup(service => service.FindMatchingJobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, new Job { Id = 1, JobTitle = "Test Job", BusinessName = "Test Company" }, 0.9));

        _userJobRepositoryMock.Setup(repo => repo.GetUserJobByUserIdAndJobIdAsync(config.UserId, 1))
            .ReturnsAsync((UserJob)null!);

        // Act
        var result = await _emailAnalysisService.AnalyzeRecentEmails(config);

        // Assert
        Assert.Single(result);
        var analysisResult = result[0];
        Assert.True(analysisResult.IsRecognized);
        Assert.Equal("Test Company", analysisResult.Job!.BusinessName);
        Assert.Equal("Test Job", analysisResult.Job.JobTitle);
    }

    [Fact]
    public async Task AnalyzeRecentEmails_SkipsAlreadyAnalyzedEmails()
    {
        // Arrange
        var config = new UserEmailConfig
            { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), EmailAddress = "test@example.com" };
        var emails = new List<EmailMessage>
        {
            new() { MessageId = "msg1", Subject = "Test Subject", Body = "Test Body", ReceivedDate = DateTime.UtcNow }
        };

        _emailServiceMock.Setup(s => s.FetchRecentEmailsAsync(config))
            .ReturnsAsync(emails);

        _analyzedEmailRepositoryMock.Setup(r => r.ExistsAsync(config.Id, "msg1"))
            .ReturnsAsync(true);

        // Act
        var result = await _emailAnalysisService.AnalyzeRecentEmails(config);

        // Assert
        Assert.Empty(result);
        _aiAnalysisServiceMock.Verify(s => s.ExtractJobInfo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AnalyzeRecentEmails_ProcessesEmailWithoutCompanyName()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var emails = new List<EmailMessage>
        {
            new()
            {
                MessageId = "msg2",
                Subject = "Subject without company",
                Body = "Body content",
                ReceivedDate = DateTime.UtcNow
            }
        };

        _emailServiceMock.Setup(s => s.FetchRecentEmailsAsync(config))
            .ReturnsAsync(emails);

        _analyzedEmailRepositoryMock.Setup(r => r.ExistsAsync(config.Id, "msg2"))
            .ReturnsAsync(false);

        _aiAnalysisServiceMock.Setup(s => s.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync(("", "", UserJobStatus.Applied, new List<string>(), null));

        // Act
        var result = await _emailAnalysisService.AnalyzeRecentEmails(config);

        // Assert
        Assert.Single(result);
        var analysisResult = result[0];
        Assert.False(analysisResult.IsRecognized);
        Assert.Null(analysisResult.Job);
    }

    [Fact]
    public async Task AnalyzeRecentEmails_CreatesNewJobWhenMatchNotFound()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var emails = new List<EmailMessage>
        {
            new()
            {
                MessageId = "msg3",
                Subject = "New Job",
                Body = "Job content",
                ReceivedDate = DateTime.UtcNow
            }
        };

        _emailServiceMock.Setup(s => s.FetchRecentEmailsAsync(config))
            .ReturnsAsync(emails);

        _analyzedEmailRepositoryMock.Setup(r => r.ExistsAsync(config.Id, "msg3"))
            .ReturnsAsync(false);

        _aiAnalysisServiceMock.Setup(s => s.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync(("New Company", "Developer", UserJobStatus.Applied, new List<string>(), null));

        _jobMatchingServiceMock.Setup(s => s.FindMatchingJobAsync("Developer", "New Company"))
            .ReturnsAsync((false, null, 0.0));

        _jobRepositoryMock.Setup(r => r.CreateJobAsync(It.IsAny<Job>()))
            .ReturnsAsync((Job job) => job);

        _userJobRepositoryMock.Setup(r => r.CreateUserJobAsync(It.IsAny<UserJob>()))
            .ReturnsAsync((UserJob uj) => uj);

        _analyzedEmailRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<AnalyzedEmail>()))
            .ReturnsAsync((AnalyzedEmail ae) => ae);

        // Act
        var result = await _emailAnalysisService.AnalyzeRecentEmails(config);

        // Assert
        Assert.Single(result);
        var analysisResult = result[0];
        Assert.True(analysisResult.IsRecognized);
        Assert.NotNull(analysisResult.Job);
        Assert.Equal("New Company", analysisResult.Job.BusinessName);
        Assert.Equal("Developer", analysisResult.Job.JobTitle);

        _jobRepositoryMock.Verify(r => r.CreateJobAsync(It.IsAny<Job>()), Times.Once);
        _userJobRepositoryMock.Verify(r => r.CreateUserJobAsync(It.IsAny<UserJob>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeRecentEmails_UpdatesExistingUserJobStatus()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var emails = new List<EmailMessage>
        {
            new()
            {
                MessageId = "msg4",
                Subject = "Interview Invitation",
                Body = "Interview details",
                ReceivedDate = DateTime.UtcNow
            }
        };

        var existingJob = new Job { Id = 1, JobTitle = "Developer", BusinessName = "Existing Company" };
        var existingUserJob = new UserJob
        {
            Id = Guid.NewGuid(),
            JobId = existingJob.Id,
            UserId = config.UserId,
            Status = UserJobStatus.Applied
        };

        _emailServiceMock.Setup(s => s.FetchRecentEmailsAsync(config))
            .ReturnsAsync(emails);

        _analyzedEmailRepositoryMock.Setup(r => r.ExistsAsync(config.Id, "msg4"))
            .ReturnsAsync(false);

        _aiAnalysisServiceMock.Setup(s => s.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync(("Existing Company", "Developer", UserJobStatus.Interviewing, new List<string>(), null));

        _jobMatchingServiceMock.Setup(s => s.FindMatchingJobAsync("Developer", "Existing Company"))
            .ReturnsAsync((true, existingJob, 0.9));

        _userJobRepositoryMock.Setup(r => r.GetUserJobByUserIdAndJobIdAsync(config.UserId, existingJob.Id))
            .ReturnsAsync(existingUserJob);

        _userJobRepositoryMock.Setup(r => r.UpdateUserJobAsync(existingUserJob))
            .Returns(Task.CompletedTask);

        _analyzedEmailRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<AnalyzedEmail>()))
            .ReturnsAsync((AnalyzedEmail ae) => ae);

        // Act
        var result = await _emailAnalysisService.AnalyzeRecentEmails(config);

        // Assert
        Assert.Single(result);
        var analysisResult = result[0];
        Assert.True(analysisResult.IsRecognized);
        Assert.Equal(UserJobStatus.Interviewing, analysisResult.Status);

        _userJobRepositoryMock.Verify(r => r.UpdateUserJobAsync(existingUserJob), Times.Once);
        Assert.Equal(UserJobStatus.Interviewing, existingUserJob.Status);
    }

    [Fact]
    public async Task AnalyzeRecentEmails_HandlesExceptionGracefully()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var emails = new List<EmailMessage>
        {
            new()
            {
                MessageId = "msg5",
                Subject = "Faulty Email",
                Body = "Content causing exception",
                ReceivedDate = DateTime.UtcNow
            }
        };

        _emailServiceMock.Setup(s => s.FetchRecentEmailsAsync(config))
            .ReturnsAsync(emails);

        _analyzedEmailRepositoryMock.Setup(r => r.ExistsAsync(config.Id, "msg5"))
            .ReturnsAsync(false);

        _aiAnalysisServiceMock.Setup(s => s.ExtractJobInfo(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _emailAnalysisService.AnalyzeRecentEmails(config);

        // Assert
        Assert.Single(result);
        var analysisResult = result[0];
        Assert.False(analysisResult.IsRecognized);
        Assert.Equal("Faulty Email", analysisResult.Subject);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)!),
            Times.Once);
    }

    // 更多测试方法...
}