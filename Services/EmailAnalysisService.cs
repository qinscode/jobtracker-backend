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

    private async Task<List<EmailAnalysisDto>> ProcessEmails(IEnumerable<EmailMessage> emails, UserEmailConfig config)
    {
        var results = new List<EmailAnalysisDto>();

        foreach (var email in emails)
        {
            // 检查是否已经分析过这封邮件
            if (await _analyzedEmailRepository.ExistsAsync(config.Id, email.MessageId))
            {
                _logger.LogInformation("Skipping already analyzed email: {Subject}", email.Subject);
                continue;
            }

            try
            {
                var jobInfo = await _aiAnalysisService.ExtractJobInfo(email.Body);
                var isRejection = await _aiAnalysisService.IsRejectionEmail(email.Body);
                var status = isRejection ? UserJobStatus.Rejected : UserJobStatus.Applied;

                // 创建基本的分析结果
                var analysisResult = new EmailAnalysisDto
                {
                    Subject = email.Subject,
                    ReceivedDate = email.ReceivedDate,
                    IsRecognized = false
                };

                Job? matchedJob = null;

                // 修改这里：如果有公司名称就继续处理，不要求必须有职位名称
                if (!string.IsNullOrWhiteSpace(jobInfo.CompanyName))
                {
                    var searchJobTitle = string.IsNullOrWhiteSpace(jobInfo.JobTitle)
                        ? "Unknown Position"
                        : jobInfo.JobTitle;
                    var (isMatch, job, similarity) = await _jobMatchingService.FindMatchingJobAsync(
                        searchJobTitle,
                        jobInfo.CompanyName);

                    if (isMatch && job != null)
                    {
                        matchedJob = job;
                        var userJob = await _userJobRepository.GetUserJobByUserIdAndJobIdAsync(config.UserId, job.Id);
                        if (userJob != null && status == UserJobStatus.Rejected)
                        {
                            userJob.Status = status;
                            userJob.UpdatedAt = DateTime.UtcNow;
                            await _userJobRepository.UpdateUserJobAsync(userJob);
                        }

                        var analyzedEmail = new AnalyzedEmail
                        {
                            UserEmailConfigId = config.Id,
                            MessageId = email.MessageId,
                            Subject = email.Subject,
                            ReceivedDate = email.ReceivedDate,
                            MatchedJobId = job.Id
                        };
                        await _analyzedEmailRepository.CreateAsync(analyzedEmail);
                    }
                    else
                    {
                        var perthTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PerthTimeZone);
                        var newJob = new Job
                        {
                            JobTitle = searchJobTitle,
                            BusinessName = jobInfo.CompanyName,
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
                                MatchedJobId = newJob.Id
                            };
                            await _analyzedEmailRepository.CreateAsync(analyzedEmail);

                            matchedJob = newJob;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to create new job for {Company} - {Title}",
                                jobInfo.CompanyName, searchJobTitle);
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

    public async Task ScanEmailsAsync(UserEmailConfig config)
    {
        // 暂时不实现
        await Task.CompletedTask;
    }
}