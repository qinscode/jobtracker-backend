using JobTracker.Repositories;
using Microsoft.Extensions.Options;

namespace JobTracker.Services.BackgroundServices;

public class EmailAnalysisBackgroundServiceOptions
{
    public int IntervalMinutes { get; set; }
}

public class EmailAnalysisBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailAnalysisBackgroundService> _logger;
    private readonly int _intervalMinutes;

    public EmailAnalysisBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<EmailAnalysisBackgroundServiceOptions> options,
        ILogger<EmailAnalysisBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _intervalMinutes = options.Value.IntervalMinutes;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Analysis Background Service is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEmailAnalysis(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing email analysis");
            }

            _logger.LogInformation("Waiting for {Minutes} minutes before next email analysis", _intervalMinutes);
            await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), stoppingToken);
        }
    }

    private async Task ProcessEmailAnalysis(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting scheduled email analysis at: {time}", DateTimeOffset.Now);

        using var scope = _serviceProvider.CreateScope();
        var emailConfigRepository = scope.ServiceProvider.GetRequiredService<IUserEmailConfigRepository>();
        var analyzedEmailRepository = scope.ServiceProvider.GetRequiredService<IAnalyzedEmailRepository>();
        var emailAnalysisService = scope.ServiceProvider.GetRequiredService<IEmailAnalysisService>();

        try
        {
            var activeConfigs = await emailConfigRepository.GetAllActiveConfigsAsync();
            foreach (var config in activeConfigs)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("Processing emails for config: {Email}", config.EmailAddress);

                    // 获取最后分析的UID
                    var lastUid = await analyzedEmailRepository.GetLastAnalyzedUidAsync(config.Id);

                    // 执行增量分析
                    var results = await emailAnalysisService.AnalyzeIncrementalEmails(config, lastUid);

                    _logger.LogInformation("Analyzed {Count} emails for {Email}",
                        results.Count, config.EmailAddress);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing emails for config {Email}", config.EmailAddress);
                    // 继续处理下一个配置
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email configurations");
            throw;
        }
    }
}