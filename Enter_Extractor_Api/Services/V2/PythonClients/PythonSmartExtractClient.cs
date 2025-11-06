using System.Text;
using System.Text.Json;
using Enter_Extractor_Api.Models.V2;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace Enter_Extractor_Api.Services.V2.PythonClients;

public interface IPythonSmartExtractClient
{
    Task<SmartExtractResponse> ExtractAsync(
        SmartExtractRequest request,
        string traceId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Cliente HTTP para /smart-extract (Python)
/// Timeout: 20s
/// </summary>
public class PythonSmartExtractClient : IPythonSmartExtractClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PythonSmartExtractClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IAsyncPolicy<HttpResponseMessage> _combinedPolicy;

    public PythonSmartExtractClient(
        HttpClient httpClient,
        ILogger<PythonSmartExtractClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        // Retry policy
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "⚠️ [SmartExtractClient] Retry {RetryCount} após {Delay}ms",
                        retryCount, timespan.TotalMilliseconds
                    );
                }
            );

        // Circuit Breaker
        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration) =>
                {
                    _logger.LogError("🔴 [SmartExtractClient] Circuit Breaker ABERTO por {Duration}s", duration.TotalSeconds);
                },
                onReset: () => _logger.LogInformation("🟢 [SmartExtractClient] Circuit Breaker FECHADO"),
                onHalfOpen: () => _logger.LogInformation("🟡 [SmartExtractClient] Circuit Breaker HALF-OPEN")
            );

        // Timeout: 20s para SmartExtract
        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(20),
            TimeoutStrategy.Pessimistic,
            onTimeoutAsync: (context, timespan, task) =>
            {
                _logger.LogError("⏱️ [SmartExtractClient] Timeout após {Timeout}s", timespan.TotalSeconds);
                return Task.CompletedTask;
            }
        );

        _combinedPolicy = Policy.WrapAsync(timeoutPolicy, retryPolicy, circuitBreakerPolicy);
    }

    public async Task<SmartExtractResponse> ExtractAsync(
        SmartExtractRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation(
                "📡 [TraceId: {TraceId}] [SmartExtractClient] Chamando /smart-extract - {FieldCount} campos, {TextLength} chars",
                traceId, request.Schema.Count, request.Text.Length
            );

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            content.Headers.Add("X-Trace-ID", traceId);

            var response = await _combinedPolicy.ExecuteAsync(async () =>
            {
                var httpResponse = await _httpClient.PostAsync("/smart-extract", content, cancellationToken);
                httpResponse.EnsureSuccessStatusCode();
                return httpResponse;
            });

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<SmartExtractResponse>(responseBody, _jsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException("SmartExtract response é nula");
            }

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation(
                "✅ [TraceId: {TraceId}] [SmartExtractClient] Resposta em {Duration}ms - {FieldCount} campos, conf_avg: {ConfAvg:F3}, cache_hit: {CacheHit}, gpt_used: {GptUsed}",
                traceId, duration, result.Fields.Count, result.ConfidenceAvg, result.CacheHit, result.GptFallbackUsed
            );

            return result;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError("[TraceId: {TraceId}] [SmartExtractClient] Circuit Breaker aberto: {Message}", traceId, ex.Message);

            return new SmartExtractResponse
            {
                Fields = new Dictionary<string, SmartExtractField>(),
                ConfidenceAvg = 0.0f,
                ProcessingTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                CacheHit = false,
                GptFallbackUsed = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("[TraceId: {TraceId}] [SmartExtractClient] Erro: {Message}", traceId, ex.Message);
            throw;
        }
    }
}
