using System.Text;
using System.Text.Json;
using Enter_Extractor_Api.Models.V2;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace Enter_Extractor_Api.Services.V2.PythonClients;

public interface IPythonFallbackClient
{
    Task<LlmFallbackResponse> FallbackAsync(
        LlmFallbackRequest request,
        string traceId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Cliente HTTP para /llm/fallback (Python)
/// Timeout: 30s (GPT pode demorar)
/// </summary>
public class PythonFallbackClient : IPythonFallbackClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PythonFallbackClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IAsyncPolicy<HttpResponseMessage> _combinedPolicy;

    public PythonFallbackClient(
        HttpClient httpClient,
        ILogger<PythonFallbackClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: 2, // Menos retries para fallback (já é custoso)
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(500 * Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("⚠️ [FallbackClient] Retry {RetryCount} após {Delay}ms", retryCount, timespan.TotalMilliseconds);
                }
            );

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(60),
                onBreak: (outcome, duration) =>
                {
                    _logger.LogError("🔴 [FallbackClient] Circuit Breaker ABERTO por {Duration}s", duration.TotalSeconds);
                },
                onReset: () => _logger.LogInformation("🟢 [FallbackClient] Circuit Breaker FECHADO"),
                onHalfOpen: () => _logger.LogInformation("🟡 [FallbackClient] Circuit Breaker HALF-OPEN")
            );

        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(30), // 30s para GPT
            TimeoutStrategy.Pessimistic,
            onTimeoutAsync: (context, timespan, task) =>
            {
                _logger.LogError("⏱️ [FallbackClient] Timeout após {Timeout}s", timespan.TotalSeconds);
                return Task.CompletedTask;
            }
        );

        _combinedPolicy = Policy.WrapAsync(timeoutPolicy, retryPolicy, circuitBreakerPolicy);
    }

    public async Task<LlmFallbackResponse> FallbackAsync(
        LlmFallbackRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation(
                "📡 [TraceId: {TraceId}] [FallbackClient] Chamando /llm/fallback - {FieldCount} campos",
                traceId, request.Schema.Count
            );

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            content.Headers.Add("X-Trace-ID", traceId);

            var response = await _combinedPolicy.ExecuteAsync(async () =>
            {
                var httpResponse = await _httpClient.PostAsync("/llm/fallback", content, cancellationToken);
                httpResponse.EnsureSuccessStatusCode();
                return httpResponse;
            });

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<LlmFallbackResponse>(responseBody, _jsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException("Fallback response é nula");
            }

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation(
                "✅ [TraceId: {TraceId}] [FallbackClient] Resposta em {Duration}ms - {FieldCount} campos corrigidos",
                traceId, duration, result.Fields.Count
            );

            return result;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError("[TraceId: {TraceId}] [FallbackClient] Circuit Breaker aberto: {Message}", traceId, ex.Message);
            return new LlmFallbackResponse
            {
                Fields = new Dictionary<string, LlmFallbackField>(),
                ProcessingMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                CacheHit = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("[TraceId: {TraceId}] [FallbackClient] Erro: {Message}", traceId, ex.Message);
            throw;
        }
    }
}

// ========== Metrics Client ==========

public interface IPythonMetricsClient
{
    Task<ThresholdMetricsResponse> GetThresholdAsync(
        string label,
        string traceId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Cliente HTTP para /metrics/threshold/{label} (Python)
/// Timeout: 1.5s (rápido)
/// </summary>
public class PythonMetricsClient : IPythonMetricsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PythonMetricsClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly float _defaultThreshold = 0.7f;

    public PythonMetricsClient(
        HttpClient httpClient,
        ILogger<PythonMetricsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<ThresholdMetricsResponse> GetThresholdAsync(
        string label,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "📡 [TraceId: {TraceId}] [MetricsClient] Consultando threshold para label: {Label}",
                traceId, label
            );

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(1500)); // Timeout curto: 1.5s

            var request = new HttpRequestMessage(HttpMethod.Get, $"/metrics/threshold/{label}");
            request.Headers.Add("X-Trace-ID", traceId);

            var response = await _httpClient.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
                var result = JsonSerializer.Deserialize<ThresholdMetricsResponse>(responseBody, _jsonOptions);

                if (result != null)
                {
                    _logger.LogInformation(
                        "✅ [TraceId: {TraceId}] [MetricsClient] Threshold: {Threshold} - {Reason}",
                        traceId, result.Threshold, result.Reason
                    );
                    return result;
                }
            }

            // Fallback para threshold padrão
            _logger.LogWarning(
                "⚠️ [TraceId: {TraceId}] [MetricsClient] Usando threshold padrão: {Threshold}",
                traceId, _defaultThreshold
            );

            return new ThresholdMetricsResponse
            {
                Label = label,
                Threshold = _defaultThreshold,
                Reason = "default (metrics endpoint unavailable)"
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "⏱️ [TraceId: {TraceId}] [MetricsClient] Timeout - usando threshold padrão: {Threshold}",
                traceId, _defaultThreshold
            );

            return new ThresholdMetricsResponse
            {
                Label = label,
                Threshold = _defaultThreshold,
                Reason = "default (timeout)"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "⚠️ [TraceId: {TraceId}] [MetricsClient] Erro: {Message} - usando threshold padrão: {Threshold}",
                traceId, ex.Message, _defaultThreshold
            );

            return new ThresholdMetricsResponse
            {
                Label = label,
                Threshold = _defaultThreshold,
                Reason = "default (error)"
            };
        }
    }
}
