using JobTracker.Models;
using JobTracker.Repositories;
using JobTracker.Services.Interfaces;

namespace JobTracker.Services;

public class EmailAnalysisService : IEmailAnalysisService
{
    private static readonly TimeZoneInfo PerthTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Perth");
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly IEmailService _emailService;
    private readonly IJobMatchingService _jobMatchingService;
    private readonly IJobRepository _jobRepository;
    private readonly IUserJobRepository _userJobRepository;
    private readonly ILogger<EmailAnalysisService> _logger;

    public EmailAnalysisService(
        IEmailService emailService,
        IAIAnalysisService aiAnalysisService,
        IJobMatchingService jobMatchingService,
        IJobRepository jobRepository,
        IUserJobRepository userJobRepository,
        ILogger<EmailAnalysisService> logger)
    {
        _emailService = emailService;
        _aiAnalysisService = aiAnalysisService;
        _jobMatchingService = jobMatchingService;
        _jobRepository = jobRepository;
        _userJobRepository = userJobRepository;
        _logger = logger;
    }

    public async Task<List<EmailAnalysisDto>> AnalyzeAllEmails(UserEmailConfig config)
    {
        _logger.LogInformation("Starting to analyze emails for {Email}", config.EmailAddress);
        var results = new List<EmailAnalysisDto>();

        try
        {
            var emails = await _emailService.FetchNewEmailsAsync(config);
            _logger.LogInformation("Found {Count} emails to analyze", emails.Count());

            foreach (var email in emails)
            {
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
                        IsRecognized = false // 默认为未识别
                    };

                    // 如果提取到了公司名称和职位名称
                    if (!string.IsNullOrWhiteSpace(jobInfo.CompanyName) && !string.IsNullOrWhiteSpace(jobInfo.JobTitle))
                    {
                        var (isMatch, matchedJob, similarity) = await _jobMatchingService.FindMatchingJobAsync(
                            jobInfo.JobTitle,
                            jobInfo.CompanyName);

                        Job job;
                        if (isMatch && matchedJob != null)
                        {
                            // 使用现有的工作记录
                            job = matchedJob;
                            
                            // 更新用户工作状态
                            var userJob = await _userJobRepository.GetUserJobByUserIdAndJobIdAsync(config.UserId, job.Id);
                            if (userJob != null && status == UserJobStatus.Rejected)
                            {
                                userJob.Status = status;
                                userJob.UpdatedAt = DateTime.UtcNow;
                                await _userJobRepository.UpdateUserJobAsync(userJob);
                                _logger.LogInformation("Updated job status to Rejected for JobId: {JobId}", job.Id);
                            }
                        }
                        else
                        {
                            // 创建新的工作记录
                            var perthTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PerthTimeZone);
                            job = new Job
                            {
                                JobTitle = jobInfo.JobTitle,
                                BusinessName = jobInfo.CompanyName,
                                CreatedAt = perthTime,
                                UpdatedAt = perthTime,
                                IsActive = true,
                                PostedDate = email.ReceivedDate,
                                IsNew = true
                            };
                            job = await _jobRepository.CreateJobAsync(job);
                            _logger.LogInformation("Created new job with ID: {JobId}", job.Id);

                            // 创建用户工作关系
                            var userJob = new UserJob
                            {
                                UserId = config.UserId,
                                JobId = job.Id,
                                Status = status,
                                CreatedAt = perthTime,
                                UpdatedAt = perthTime
                            };
                            await _userJobRepository.CreateUserJobAsync(userJob);
                            _logger.LogInformation("Created new user job relationship with status: {Status}", status);
                        }

                        analysisResult.Job = new JobBasicInfo
                        {
                            Id = job.Id,
                            JobTitle = job.JobTitle,
                            BusinessName = job.BusinessName
                        };
                        analysisResult.IsRecognized = true;

                        // 输出分析结果到控制台
                        Console.WriteLine("\n========== Email Analysis Result ==========");
                        Console.WriteLine($"Subject: {analysisResult.Subject}");
                        Console.WriteLine($"Date: {analysisResult.ReceivedDate:yyyy-MM-dd HH:mm:ss} AWST");
                        Console.WriteLine($"Company: {job.BusinessName}");
                        Console.WriteLine($"Job Title: {job.JobTitle}");
                        Console.WriteLine($"Job ID: {job.Id}");
                        Console.WriteLine($"Status: {status}");
                        Console.WriteLine($"Is New Job: {!isMatch}");
                        Console.WriteLine("=========================================\n");
                    }
                    else
                    {
                        Console.WriteLine("\n========== Email Analysis Result ==========");
                        Console.WriteLine($"Subject: {analysisResult.Subject}");
                        Console.WriteLine($"Date: {analysisResult.ReceivedDate:yyyy-MM-dd HH:mm:ss} AWST");
                        Console.WriteLine("Job information could not be extracted");
                        Console.WriteLine("=========================================\n");
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during email analysis for {Email}", config.EmailAddress);
            throw;
        }

        _logger.LogInformation("Completed analyzing {Count} emails for {Email}", results.Count, config.EmailAddress);
        return results;
    }

    public async Task ScanEmailsAsync(UserEmailConfig config)
    {
        // 暂时不实现
        await Task.CompletedTask;
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
}