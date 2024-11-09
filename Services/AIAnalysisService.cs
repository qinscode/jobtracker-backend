using System.Text.Json;
using System.Text.Json.Serialization;
using JobTracker.Services.Interfaces;
using JobTracker.Templates;
using Polly;
using Polly.Retry;

namespace JobTracker.Services;

public class AIAnalysisService : IAIAnalysisService
{
    private const int MaxRetries = 3;
    private const int QuotaExceededDelayMs = 60000; // 1分钟延迟
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AIAnalysisService> _logger;
    private readonly AsyncRetryPolicy<string> _retryPolicy;

    public AIAnalysisService(
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<AIAnalysisService> logger)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;

        _retryPolicy = Policy<string>
            .Handle<HttpRequestException>(ex =>
                ex.Message.Contains("TooManyRequests") ||
                ex.Message.Contains("RESOURCE_EXHAUSTED"))
            .WaitAndRetryAsync(
                MaxRetries,
                retryAttempt =>
                {
                    // 如果是配额限制，等待1分钟
                    var delay = TimeSpan.FromMilliseconds(QuotaExceededDelayMs);
                    _logger.LogWarning(
                        "Quota exceeded. Waiting {DelayMs}ms before retry {RetryCount}/{MaxRetries}",
                        delay.TotalMilliseconds,
                        retryAttempt,
                        MaxRetries);
                    Console.WriteLine(
                        $"API quota exceeded. Waiting 1 minute before retry {retryAttempt}/{MaxRetries}...");
                    return delay;
                },
                (exception, timeSpan, retryCount, _) =>
                {
                    _logger.LogWarning(
                        "Gemini API rate limit hit. Waiting {DelayMs}ms before retry {RetryCount}/{MaxRetries}. Error: {Error}",
                        timeSpan.TotalMilliseconds,
                        retryCount,
                        MaxRetries,
                        exception.ToString());
                });
    }

    public async Task<bool> IsRejectionEmail(string emailContent)
    {
        try
        {
            var result = await CallGeminiApiWithRetry(emailContent);
            _logger.LogInformation("Gemini Analysis Result: {Result}", result);
            Console.WriteLine($"Gemini Analysis Result: {result}");

            if (string.IsNullOrEmpty(result)) return false;

            var jsonContent = result.Replace("json\n", "").Trim();
            var jobInfo = JsonSerializer.Deserialize<JobInfo>(jsonContent);
            return jobInfo?.Status?.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing rejection email");
            return false;
        }
    }

    public async Task<(string CompanyName, string JobTitle)> ExtractJobInfo(string emailContent)
    {
        try
        {
            var result = await CallGeminiApiWithRetry(emailContent);
            _logger.LogInformation("Gemini Analysis Result: {Result}", result);
            Console.WriteLine($"Gemini Analysis Result: {result}");

            if (string.IsNullOrEmpty(result)) return ("", "");

            var jsonContent = result.Replace("json\n", "").Trim();
            var jobInfo = JsonSerializer.Deserialize<JobInfo>(jsonContent);
            return (jobInfo?.BusinessName ?? "", jobInfo?.JobTitle ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting job info");
            return ("", "");
        }
    }

    private async Task<string> CallGeminiApiWithRetry(string content)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                var result = await CallGeminiApi(content);
                // 成功调用后添加短暂延迟，避免触发限制
                await Task.Delay(1000); // 1秒延迟
                return result;
            }
            catch (HttpRequestException ex) when (
                ex.Message.Contains("TooManyRequests") ||
                ex.Message.Contains("RESOURCE_EXHAUSTED"))
            {
                _logger.LogWarning("API quota exceeded, will retry after delay");
                Console.WriteLine("API quota exceeded, will retry after delay");
                throw; // 重新抛出异常，让重试策略处理
            }
        });
    }

    private async Task<string> CallGeminiApi(string content)
    {
        var geminiApiKey = _configuration["Gemini:ApiKey"];
        var geminiEndpoint = _configuration["Gemini:ApiEndpoint"];

        if (string.IsNullOrEmpty(geminiApiKey))
            throw new InvalidOperationException("Gemini API key is not configured");

        var requestContent = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = $"{EmailAnalysisPrompts.ANALYSIS_PROMPT}\n{content}"
                        }
                    }
                }
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{geminiEndpoint}?key={geminiApiKey}",
            requestContent
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Gemini API failed: Status={Status}, Error={Error}",
                response.StatusCode, errorContent);
            throw new HttpRequestException(
                $"Gemini API request failed with status code: {response.StatusCode}, Error: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<GeminiApiResponse>();
        var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        _logger.LogInformation("Raw Gemini Response: {Response}", text);
        return text ?? "";
    }

    private class JobInfo
    {
        [JsonPropertyName("BusinessName")] public string? BusinessName { get; set; }

        [JsonPropertyName("JobTitle")] public string? JobTitle { get; set; }

        [JsonPropertyName("Status")] public string? Status { get; set; }
    }

    private class GeminiApiResponse
    {
        public List<Candidate>? Candidates { get; }
    }

    private class Candidate
    {
        public Content? Content { get; }
    }

    private class Content
    {
        public List<Part>? Parts { get; }
    }

    private class Part
    {
        public string? Text { get; }
    }
}