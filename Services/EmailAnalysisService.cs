using JobTracker.Models;
using JobTracker.Repositories;
using JobTracker.Services.Interfaces;

namespace JobTracker.Services;

public class EmailAnalysisService : IEmailAnalysisService
{
    private static readonly TimeZoneInfo PerthTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Perth");
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly IAnalyzedEmailRepository _analyzedEmailRepository;
    private readonly IEmailService _emailService;
    private readonly IJobMatchingService _jobMatchingService;
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<EmailAnalysisService> _logger;
    private readonly IUserJobRepository _userJobRepository;

    public EmailAnalysisService(
        IEmailService emailService,
        IAIAnalysisService aiAnalysisService,
        IJobMatchingService jobMatchingService,
        IJobRepository jobRepository,
        IUserJobRepository userJobRepository,
        IAnalyzedEmailRepository analyzedEmailRepository,
        ILogger<EmailAnalysisService> logger)
    {
        _emailService = emailService;
        _aiAnalysisService = aiAnalysisService;
        _jobMatchingService = jobMatchingService;
        _jobRepository = jobRepository;
        _userJobRepository = userJobRepository;
        _analyzedEmailRepository = analyzedEmailRepository;
        _logger = logger;
    }

    public async Task<List<EmailAnalysisDto>> AnalyzeRecentEmails(UserEmailConfig config)
    {
        _logger.LogInformation("Starting to analyze recent emails for {Email}", config.EmailAddress);
        var results = new List<EmailAnalysisDto>();

        try
        {
            var emails = await _emailService.FetchRecentEmailsAsync(config);
            return await ProcessEmails(emails, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during recent email analysis for {Email}", config.EmailAddress);
            throw;
        }
    }

    public async Task<List<EmailAnalysisDto>> AnalyzeAllEmails(UserEmailConfig config)
    {
        _logger.LogInformation("Starting to analyze all emails for {Email}", config.EmailAddress);
        var results = new List<EmailAnalysisDto>();

        try
        {
            var emails = await _emailService.FetchAllEmailsAsync(config);
            return await ProcessEmails(emails, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during full email analysis for {Email}", config.EmailAddress);
            throw;
        }
    }

    public async Task<bool> IsRejectionEmail(string emailContent)
    {
        return await _aiAnalysisService.IsRejectionEmail(emailContent);
    }

    public async Task ProcessRejectionEmail(string emailContent, Guid userId)
    {
        // 暂时不实现
        await Task.CompletedTask;
    }

    public async Task<List<EmailAnalysisDto>> AnalyzeIncrementalEmails(UserEmailConfig config, uint? lastUid = null)
    {
        _logger.LogInformation("Starting incremental email analysis for {Email} from UID {LastUid}",
            config.EmailAddress, lastUid ?? 0);

        try
        {
            var emails = await _emailService.FetchIncrementalEmailsAsync(config, lastUid);
            return await ProcessEmails(emails, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during incremental email analysis for {Email}", config.EmailAddress);
            throw;
        }
    }

    protected internal async Task<List<EmailAnalysisDto>> ProcessEmails(IEnumerable<EmailMessage> emails,
        UserEmailConfig config)
    {
        var results = new List<EmailAnalysisDto>();

        foreach (var email in emails)
        {
            if (await _analyzedEmailRepository.ExistsAsync(config.Id, email.MessageId))
            {
                _logger.LogInformation("Skipping already analyzed email: {Subject}", email.Subject);
                continue;
            }

            try
            {
                var (companyName, jobTitle, status, keyPhrases, SuggestedActions) =
                    await _aiAnalysisService.ExtractJobInfo(email.Body);

                // 创建基本的分析结果
                var analysisResult = new EmailAnalysisDto
                {
                    Subject = email.Subject,
                    ReceivedDate = email.ReceivedDate,
                    IsRecognized = false,
                    Status = status,
                    KeyPhrases = keyPhrases,
                    SuggestedActions = SuggestedActions
                };

                Job? matchedJob = null;

                // 修改这里：如果有公司名称就继续处理，不要求必须有职位名称
                if (!string.IsNullOrWhiteSpace(companyName))
                {
                    var searchJobTitle = string.IsNullOrWhiteSpace(jobTitle) ? "Unknown Position" : jobTitle;
                    var (isMatch, job, similarity) = await _jobMatchingService.FindMatchingJobAsync(
                        searchJobTitle,
                        companyName);

                    if (isMatch && job != null)
                    {
                        matchedJob = job;
                        var userJob = await _userJobRepository.GetUserJobByUserIdAndJobIdAsync(config.UserId, job.Id);
                        if (userJob != null)
                        {
                            _logger.LogInformation(
                                "Found existing UserJob - Current Status: {CurrentStatus}, New Status: {NewStatus}",
                                userJob.Status, status);

                            // 只有新状态比当前状态更晚时才更新
                            if (ShouldUpdateStatus(userJob.Status, status))
                            {
                                _logger.LogInformation(
                                    "Updating job status from {CurrentStatus} to {NewStatus}",
                                    userJob.Status, status);

                                userJob.Status = status;
                                userJob.UpdatedAt = DateTime.UtcNow;
                                await _userJobRepository.UpdateUserJobAsync(userJob);
                            }
                            else
                            {
                                _logger.LogInformation(
                                    "Keeping current status {CurrentStatus} (new status {NewStatus} not applied)",
                                    userJob.Status, status);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Creating new UserJob with status: {Status}", status);
                            userJob = new UserJob
                            {
                                UserId = config.UserId,
                                JobId = job.Id,
                                Status = status,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            await _userJobRepository.CreateUserJobAsync(userJob);
                        }

                        var analyzedEmail = new AnalyzedEmail
                        {
                            UserEmailConfigId = config.Id,
                            MessageId = email.MessageId,
                            Subject = email.Subject,
                            ReceivedDate = email.ReceivedDate,
                            MatchedJobId = job.Id,
                            Uid = email.Uid,
                            KeyPhrases = keyPhrases.ToArray(),
                            SuggestedActions = SuggestedActions
                        };
                        await _analyzedEmailRepository.CreateAsync(analyzedEmail);

                        _logger.LogInformation(
                            "Successfully processed email - Subject: {Subject}, Status: {Status}, JobId: {JobId}",
                            email.Subject, status, job.Id);
                    }
                    else
                    {
                        var perthTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PerthTimeZone);
                        var newJob = new Job
                        {
                            JobTitle = searchJobTitle,
                            BusinessName = companyName,
                            CreatedAt = perthTime,
                            UpdatedAt = perthTime,
                            IsActive = true,
                            PostedDate = email.ReceivedDate,
                            IsNew = true,
                            WorkType = "",
                            JobType = "",
                            PayRange = "",
                            Suburb = "",
                            Area = "",
                            Url = "",
                            JobDescription = "",
                            AdvertiserId = 0
                        };

                        try
                        {
                            newJob = await _jobRepository.CreateJobAsync(newJob);

                            var userJob = new UserJob
                            {
                                UserId = config.UserId,
                                JobId = newJob.Id,
                                Status = status,
                                CreatedAt = perthTime,
                                UpdatedAt = perthTime
                            };
                            await _userJobRepository.CreateUserJobAsync(userJob);

                            var analyzedEmail = new AnalyzedEmail
                            {
                                UserEmailConfigId = config.Id,
                                MessageId = email.MessageId,
                                Subject = email.Subject,
                                ReceivedDate = email.ReceivedDate,
                                MatchedJobId = newJob.Id,
                                Uid = email.Uid,
                                KeyPhrases = keyPhrases.ToArray(),
                                SuggestedActions = SuggestedActions
                            };
                            await _analyzedEmailRepository.CreateAsync(analyzedEmail);

                            matchedJob = newJob;

                            analysisResult.Job = new JobBasicInfo
                            {
                                Id = newJob.Id,
                                JobTitle = newJob.JobTitle ?? string.Empty,
                                BusinessName = newJob.BusinessName ?? string.Empty
                            };
                            analysisResult.IsRecognized = true;
                            analysisResult.Status = status;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to create new job for {Company} - {Title}",
                                companyName, searchJobTitle);
                            throw;
                        }
                    }

                    if (matchedJob != null)
                    {
                        analysisResult.Job = new JobBasicInfo
                        {
                            Id = matchedJob.Id,
                            JobTitle = matchedJob.JobTitle ?? string.Empty,
                            BusinessName = matchedJob.BusinessName ?? string.Empty
                        };
                        analysisResult.IsRecognized = true;
                        analysisResult.Status = status;
                    }
                }
                else
                {
                    _logger.LogWarning("Could not extract company name from email: {Subject}", email.Subject);
                }

                results.Add(analysisResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing email with subject: {Subject}", email.Subject);
                results.Add(new EmailAnalysisDto
                {
                    Subject = email.Subject,
                    ReceivedDate = email.ReceivedDate,
                    IsRecognized = false
                });
            }
        }

        return results;
    }

    private bool ShouldUpdateStatus(UserJobStatus currentStatus, UserJobStatus newStatus)
    {
        // 定义状态的进展顺序
        var statusOrder = new Dictionary<UserJobStatus, int>
        {
            { UserJobStatus.Applied, 0 },
            { UserJobStatus.Reviewed, 1 },
            { UserJobStatus.Interviewing, 2 },
            { UserJobStatus.TechnicalAssessment, 3 },
            { UserJobStatus.Offered, 4 },
            { UserJobStatus.Rejected, 5 } // Rejected 可以发生在任何阶段
        };

        // 如果新状态是 Rejected，总是更新
        if (newStatus == UserJobStatus.Rejected)
            return true;

        // 否则只有当新状态的顺序更高时才更新
        return statusOrder.TryGetValue(newStatus, out var newOrder) &&
               statusOrder.TryGetValue(currentStatus, out var currentOrder) &&
               newOrder > currentOrder;
    }

    public async Task ScanEmailsAsync(UserEmailConfig config)
    {
        // 暂时不实现
        await Task.CompletedTask;
    }
}