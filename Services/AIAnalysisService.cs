using System.Text.Json;
using System.Text.Json.Serialization;
using JobTracker.Models;
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
            var jobInfo = await AnalyzeEmail(emailContent);
            return jobInfo?.Status?.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing rejection email");
            return false;
        }
    }

    public async
        Task<(string CompanyName, string JobTitle, UserJobStatus Status, List<string> KeyPhrases, string?
            SuggestedActions, string? ReasonForRejection)> ExtractJobInfo(string emailContent)
    {
        try
        {
            var jobInfo = await AnalyzeEmail(emailContent);
            var status = ParseJobStatus(jobInfo?.Status);

            _logger.LogInformation(
                "Extracted job info - Company: {Company}, Title: {Title}, Status: {Status}, KeyPhrases: {KeyPhrases}, SuggestedActions: {SuggestedActions}, ReasonForRejection: {ReasonForRejection}",
                jobInfo?.BusinessName,
                jobInfo?.JobTitle,
                status,
                jobInfo?.KeyPhrases != null ? string.Join(", ", jobInfo.KeyPhrases) : "none",
                jobInfo?.SuggestedActions ?? "none",
                jobInfo?.ReasonForRejection ?? "none");

            return (
                jobInfo?.BusinessName ?? "",
                jobInfo?.JobTitle ?? "",
                status,
                jobInfo?.KeyPhrases ?? new List<string>(),
                jobInfo?.SuggestedActions,
                jobInfo?.ReasonForRejection
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting job info");
            return ("", "", UserJobStatus.Applied, new List<string>(), null, null);
        }
    }

    private UserJobStatus ParseJobStatus(string? status)
    {
        if (string.IsNullOrEmpty(status))
        {
            _logger.LogInformation("Status is null or empty, defaulting to Applied");
            return UserJobStatus.Applied;
        }

        _logger.LogInformation("Parsing status: {Status}", status);

        var normalizedStatus = status.ToLower().Trim()
            .Replace("_", " ") // 将下划线替换为空格
            .Replace("technicalassessment", "technical assessment"); // 处理没有空格的情况

        _logger.LogInformation("Normalized status: {NormalizedStatus}", normalizedStatus);

        var result = normalizedStatus switch
        {
            "rejected" => UserJobStatus.Rejected,
            "reviewed" => UserJobStatus.Reviewed,
            "interviewing" => UserJobStatus.Interviewing,
            "technical assessment" => UserJobStatus.TechnicalAssessment,
            "offered" => UserJobStatus.Offered,
            _ => UserJobStatus.Applied
        };

        _logger.LogInformation("Parsed status result: {Result} from original status: {OriginalStatus}",
            result, status);

        return result;
    }

    private async Task<JobInfo?> AnalyzeEmail(string emailContent)
    {
        var result = await CallGeminiApiWithRetry(emailContent);
        _logger.LogInformation("Gemini Analysis Result: {Result}", result);

        if (string.IsNullOrEmpty(result))
        {
            _logger.LogWarning("Empty result from Gemini API");
            return null;
        }

        try
        {
            // 清理 markdown 代码块标记和多余的空白
            var jsonContent = result
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            _logger.LogInformation("Cleaned JSON content: {JsonContent}", jsonContent);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var jobInfo = JsonSerializer.Deserialize<JobInfo>(jsonContent, options);
            _logger.LogInformation(
                "Deserialized JobInfo - BusinessName: {BusinessName}, JobTitle: {JobTitle}, Status: {Status}, SuggestedActions: {SuggestedActions}",
                jobInfo?.BusinessName,
                jobInfo?.JobTitle,
                jobInfo?.Status,
                jobInfo?.SuggestedActions);

            return jobInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing JSON content: {Content}", result);
            throw;
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

        var responseContent = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Raw Gemini Response: {Response}", responseContent);

        var result = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent);
        _logger.LogInformation("Deserialized Response - Candidates Count: {Count}",
            result?.Candidates?.Count ?? 0);


        if (result?.Candidates == null || !result.Candidates.Any())
        {
            _logger.LogError("No candidates in Gemini response");
            throw new InvalidOperationException("No candidates in Gemini response");
        }

        var text = result.Candidates.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(text))
        {
            _logger.LogError("Empty text in Gemini response");
            throw new InvalidOperationException("Empty text in Gemini response");
        }

        return text;
    }

    private class JobInfo
    {
        [JsonPropertyName("BusinessName")] public string? BusinessName { get; set; }

        [JsonPropertyName("JobTitle")] public string? JobTitle { get; set; }

        [JsonPropertyName("Status")] public string? Status { get; set; }

        [JsonPropertyName("KeyPhrases")] public List<string> KeyPhrases { get; set; } = new();

        [JsonPropertyName("SuggestedActions")] public string? SuggestedActions { get; set; }

        [JsonPropertyName("ReasonForRejection")]
        public string? ReasonForRejection { get; set; }
    }

    private class GeminiApiResponse
    {
        [JsonPropertyName("candidates")] public List<Candidate>? Candidates { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")] public Content? Content { get; set; }
    }

    private class Content
    {
        [JsonPropertyName("parts")] public List<Part>? Parts { get; set; }
    }

    private class Part
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
    }
}