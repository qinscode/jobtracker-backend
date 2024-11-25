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
    private readonly EmailAnalysisService _emailAnalysisService;

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
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task ProcessEmails_WithTechnicalAssessmentStatus_ShouldUpdateExistingUserJob()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var email = new EmailMessage
        {
            MessageId = "test123",
            Subject = "Technical Assessment Invitation",
            Body = "Please complete the assessment",
            ReceivedDate = DateTime.UtcNow
        };

        var existingJob = new Job { Id = 1, JobTitle = "Software Engineer", BusinessName = "TestCo" };
        var existingUserJob = new UserJob
        {
            Id = Guid.NewGuid(),
            UserId = config.UserId,
            JobId = existingJob.Id,
            Status = UserJobStatus.Interviewing
        };

        _aiAnalysisServiceMock.Setup(x => x.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync((
                "TestCo",
                "Software Engineer",
                UserJobStatus.TechnicalAssessment,
                new List<string> { "assessment invitation", "coding test" },
                "Complete technical assessment within 7 days"
            ));

        _jobMatchingServiceMock.Setup(x => x.FindMatchingJobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, existingJob, 0.9));

        _userJobRepositoryMock.Setup(x => x.GetUserJobByUserIdAndJobIdAsync(config.UserId, existingJob.Id))
            .ReturnsAsync(existingUserJob);

        // Act
        var results = await _emailAnalysisService.ProcessEmails(new[] { email }, config);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].IsRecognized);
        Assert.Equal(UserJobStatus.TechnicalAssessment, results[0].Status);

        _userJobRepositoryMock.Verify(
            x => x.UpdateUserJobAsync(It.Is<UserJob>(j =>
                j.Status == UserJobStatus.TechnicalAssessment)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessEmails_WithInterviewingStatus_ShouldCreateNewUserJob()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var email = new EmailMessage
        {
            MessageId = "test456",
            Subject = "Interview Invitation",
            Body = "We would like to invite you for an interview",
            ReceivedDate = DateTime.UtcNow
        };

        var newJob = new Job { Id = 2, JobTitle = "Developer", BusinessName = "NewCo" };

        _aiAnalysisServiceMock.Setup(x => x.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync((
                "NewCo",
                "Developer",
                UserJobStatus.Interviewing,
                new List<string> { "interview invitation", "next week" },
                "Prepare for the interview"
            ));

        _jobMatchingServiceMock.Setup(x => x.FindMatchingJobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, newJob, 0.8));

        _userJobRepositoryMock.Setup(x => x.GetUserJobByUserIdAndJobIdAsync(config.UserId, newJob.Id))
            .ReturnsAsync((UserJob?)null);

        // Act
        var results = await _emailAnalysisService.ProcessEmails(new[] { email }, config);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].IsRecognized);
        Assert.Equal(UserJobStatus.Interviewing, results[0].Status);

        _userJobRepositoryMock.Verify(
            x => x.CreateUserJobAsync(It.Is<UserJob>(j =>
                j.Status == UserJobStatus.Interviewing &&
                j.JobId == newJob.Id)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessEmails_WithNoCompanyMatch_ShouldCreateNewJobAndUserJob()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var email = new EmailMessage
        {
            MessageId = "test789",
            Subject = "Application Received",
            Body = "Thank you for your application",
            ReceivedDate = DateTime.UtcNow
        };

        _aiAnalysisServiceMock.Setup(x => x.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync((
                "UnknownCo",
                "Engineer",
                UserJobStatus.Applied,
                new List<string> { "application received", "will review" },
                "Wait for response"
            ));

        _jobMatchingServiceMock.Setup(x => x.FindMatchingJobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, null, 0.0));

        var newJobId = 3;
        _jobRepositoryMock.Setup(x => x.CreateJobAsync(It.IsAny<Job>()))
            .ReturnsAsync((Job j) =>
            {
                j.Id = newJobId;
                return j;
            });

        // Act
        var results = await _emailAnalysisService.ProcessEmails(new[] { email }, config);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].IsRecognized);
        Assert.Equal(UserJobStatus.Applied, results[0].Status);

        _jobRepositoryMock.Verify(x => x.CreateJobAsync(It.IsAny<Job>()), Times.Once);
        _userJobRepositoryMock.Verify(
            x => x.CreateUserJobAsync(It.Is<UserJob>(j =>
                j.Status == UserJobStatus.Applied &&
                j.JobId == newJobId)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessEmails_WithRejectionStatus_ShouldUpdateStatus()
    {
        // Arrange
        var config = new UserEmailConfig { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var email = new EmailMessage
        {
            MessageId = "test101",
            Subject = "Application Status Update",
            Body = "We regret to inform you",
            ReceivedDate = DateTime.UtcNow
        };

        var existingJob = new Job { Id = 4, JobTitle = "Senior Developer", BusinessName = "RejectCo" };
        var existingUserJob = new UserJob
        {
            Id = Guid.NewGuid(),
            UserId = config.UserId,
            JobId = existingJob.Id,
            Status = UserJobStatus.Interviewing
        };

        _aiAnalysisServiceMock.Setup(x => x.ExtractJobInfo(It.IsAny<string>()))
            .ReturnsAsync((
                "RejectCo",
                "Senior Developer",
                UserJobStatus.Rejected,
                new List<string> { "regret to inform", "other candidates" },
                "Consider other opportunities"
            ));

        _jobMatchingServiceMock.Setup(x => x.FindMatchingJobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, existingJob, 0.95));

        _userJobRepositoryMock.Setup(x => x.GetUserJobByUserIdAndJobIdAsync(config.UserId, existingJob.Id))
            .ReturnsAsync(existingUserJob);

        // Act
        var results = await _emailAnalysisService.ProcessEmails(new[] { email }, config);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].IsRecognized);
        Assert.Equal(UserJobStatus.Rejected, results[0].Status);

        _userJobRepositoryMock.Verify(
            x => x.UpdateUserJobAsync(It.Is<UserJob>(j =>
                j.Status == UserJobStatus.Rejected)),
            Times.Once);
    }
}