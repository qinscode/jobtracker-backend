using JobTracker.Repositories;
using JobTracker.Services;

public class EmailMonitoringService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailMonitoringService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public EmailMonitoringService(
        IServiceProvider serviceProvider,
        ILogger<EmailMonitoringService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var emailConfigRepo = scope.ServiceProvider.GetRequiredService<IUserEmailConfigRepository>();
                var emailAnalysisService = scope.ServiceProvider.GetRequiredService<IEmailAnalysisService>();
                var analyzedEmailRepo = scope.ServiceProvider.GetRequiredService<IAnalyzedEmailRepository>();

                // 获取所有活跃的邮件配置
                var configs = await emailConfigRepo.GetAllActiveConfigsAsync();

                foreach (var config in configs)
                    try
                    {
                        // 获取上次分析的时间
                        var lastAnalyzedDate = await analyzedEmailRepo.GetLastAnalyzedDateAsync(config.Id);

                        // 分析新邮件
                        await emailAnalysisService.AnalyzeNewEmails(config, lastAnalyzedDate);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing emails for {Email}", config.EmailAddress);
                    }

                // 等待配置的间隔时间
                var interval = _configuration.GetValue("EmailMonitoring:IntervalMinutes", 15);
                await Task.Delay(TimeSpan.FromMinutes(interval), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in email monitoring service");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
    }
}