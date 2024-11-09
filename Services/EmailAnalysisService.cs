using JobTracker.Models;
using JobTracker.Services.Interfaces;

namespace JobTracker.Services;

public class EmailAnalysisService : IEmailAnalysisService
{
    private static readonly TimeZoneInfo PerthTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Perth");
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly IEmailService _emailService;
    private readonly IJobMatchingService _jobMatchingService;
    private readonly ILogger<EmailAnalysisService> _logger;

    public EmailAnalysisService(
        IEmailService emailService,
        IAIAnalysisService aiAnalysisService,
        IJobMatchingService jobMatchingService,
        ILogger<EmailAnalysisService> logger)
    {
        _emailService = emailService;
        _aiAnalysisService = aiAnalysisService;
        _jobMatchingService = jobMatchingService;
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
                try
                {
                    var jobInfo = await _aiAnalysisService.ExtractJobInfo(email.Body);
                    var status = await _aiAnalysisService.IsRejectionEmail(email.Body)
                        ? UserJobStatus.Rejected
                        : UserJobStatus.Applied;

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

                        // 输出分析结果到控制台
                        Console.WriteLine("\n========== Email Analysis Result ==========");
                        Console.WriteLine($"Subject: {analysisResult.Subject}");
                        Console.WriteLine($"Date: {analysisResult.ReceivedDate.ToString("yyyy-MM-dd HH:mm:ss")} AWST");
                        Console.WriteLine("\nAI Analysis:");
                        Console.WriteLine($"Company: {jobInfo.CompanyName}");
                        Console.WriteLine($"Job Title: {jobInfo.JobTitle}");

                        if (isMatch && matchedJob != null)
                        {
                            analysisResult.Job = new JobBasicInfo
                            {
                                Id = matchedJob.Id,
                                JobTitle = matchedJob.JobTitle,
                                BusinessName = matchedJob.BusinessName
                            };
                            Console.WriteLine($"\nMatched with existing job (Similarity: {similarity:P})");
                        }
                        else
                        {
                            analysisResult.Job = new JobBasicInfo
                            {
                                JobTitle = jobInfo.JobTitle,
                                BusinessName = jobInfo.CompanyName
                            };
                            Console.WriteLine("\nNo matching job found in database");
                        }

                        analysisResult.IsRecognized = true;
                        Console.WriteLine($"\nFinal Result: {(isMatch ? "MATCHED WITH EXISTING JOB" : "NEW JOB")}");
                        Console.WriteLine("=========================================\n");
                    }
                    else
                    {
                        Console.WriteLine("\n========== Email Analysis Result ==========");
                        Console.WriteLine($"Subject: {analysisResult.Subject}");
                        Console.WriteLine($"Date: {analysisResult.ReceivedDate.ToString("yyyy-MM-dd HH:mm:ss")} AWST");
                        Console.WriteLine("Job information could not be extracted");
                        Console.WriteLine("=========================================\n");
                    }

                    results.Add(analysisResult);

                    _logger.LogInformation(
                        "Analyzed email: Subject={Subject}, Recognized={Recognized}",
                        email.Subject,
                        analysisResult.IsRecognized);
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