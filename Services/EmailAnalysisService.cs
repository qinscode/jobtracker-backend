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
    private readonly ILogger<EmailAnalysisService> _logger;
    private readonly IUserJobRepository _userJobRepository;

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

            Console.WriteLine("\n========== Email Analysis Summary ==========");
            Console.WriteLine($"Analyzing emails for: {config.EmailAddress}");
            Console.WriteLine($"Total emails found: {emails.Count()}");
            Console.WriteLine("=========================================\n");

            foreach (var email in emails)
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

                    Console.WriteLine("\n========== Processing Email ==========");
                    Console.WriteLine($"Subject: {email.Subject}");
                    Console.WriteLine($"Date: {email.ReceivedDate:yyyy-MM-dd HH:mm:ss} AWST");

                    // 如果提取到了公司名称和职位名称
                    if (!string.IsNullOrWhiteSpace(jobInfo.CompanyName) && !string.IsNullOrWhiteSpace(jobInfo.JobTitle))
                    {
                        Console.WriteLine("\nAI Analysis Result:");
                        Console.WriteLine($"Company: {jobInfo.CompanyName}");
                        Console.WriteLine($"Job Title: {jobInfo.JobTitle}");
                        Console.WriteLine($"Status: {status}");

                        var (isMatch, matchedJob, similarity) = await _jobMatchingService.FindMatchingJobAsync(
                            jobInfo.JobTitle,
                            jobInfo.CompanyName);

                        Job job;
                        if (isMatch && matchedJob != null)
                        {
                            job = matchedJob;
                            Console.WriteLine("\nMatched with existing job:");
                            Console.WriteLine($"Job ID: {job.Id}");
                            Console.WriteLine($"Action: Updating status to {status}");

                            var userJob =
                                await _userJobRepository.GetUserJobByUserIdAndJobIdAsync(config.UserId, job.Id);
                            if (userJob != null && status == UserJobStatus.Rejected)
                            {
                                userJob.Status = status;
                                userJob.UpdatedAt = DateTime.UtcNow;
                                await _userJobRepository.UpdateUserJobAsync(userJob);
                            }
                        }
                        else
                        {
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

                            var userJob = new UserJob
                            {
                                UserId = config.UserId,
                                JobId = job.Id,
                                Status = status,
                                CreatedAt = perthTime,
                                UpdatedAt = perthTime
                            };
                            await _userJobRepository.CreateUserJobAsync(userJob);

                            Console.WriteLine("\nCreated new job record:");
                            Console.WriteLine($"Job ID: {job.Id}");
                            Console.WriteLine($"Initial Status: {status}");
                        }

                        analysisResult.Job = new JobBasicInfo
                        {
                            Id = job.Id,
                            JobTitle = job.JobTitle,
                            BusinessName = job.BusinessName
                        };
                        analysisResult.IsRecognized = true;
                    }
                    else
                    {
                        Console.WriteLine("Result: No job information could be extracted");
                    }

                    Console.WriteLine("====================================\n");
                    results.Add(analysisResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error analyzing email with subject: {Subject}", email.Subject);
                    Console.WriteLine($"Error processing email: {ex.Message}\n");
                    results.Add(new EmailAnalysisDto
                    {
                        Subject = email.Subject,
                        ReceivedDate = email.ReceivedDate,
                        IsRecognized = false
                    });
                }

            Console.WriteLine("\n========== Analysis Complete ==========");
            Console.WriteLine($"Total emails processed: {results.Count}");
            Console.WriteLine($"Successfully recognized: {results.Count(r => r.IsRecognized)}");
            Console.WriteLine("=====================================\n");
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