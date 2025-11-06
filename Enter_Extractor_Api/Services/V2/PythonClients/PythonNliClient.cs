using System.Net;
using System.Text;
using System.Text.Json;
using Enter_Extractor_Api.Models.V2;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace Enter_Extractor_Api.Services.V2.PythonClients;

public interface IPythonNliClient
{
    Task<NliClassifyResponse> ClassifyAsync(
        NliClassifyRequest request,
        string traceId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Cliente HTTP para /nli/classify (Python)
/// Implementa Polly: Retry + Circuit Breaker + Timeout
/// </summary>
public class PythonNliClient : IPythonNliClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PythonNliClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;
    private readonly IAsyncPolicy<HttpResponseMessage> _timeoutPolicy;
    private readonly IAsyncPolicy<HttpResponseMessage> _combinedPolicy;

    public PythonNliClient(
        HttpClient httpClient,
        ILogger<PythonNliClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        // Retry: 3 tentativas com backoff exponencial
        _retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "⚠️ [NliClient] Retry {RetryCount} após {Delay}ms - Reason: {Reason}",
                        retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()
                    );
                }
            );

        // Circuit Breaker: abre após 5 falhas em 1 minuto
        _circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(300),
                onBreak: (outcome, duration) =>
                {
                    _logger.LogError(
                        "🔴 [NliClient] Circuit Breaker ABERTO por {Duration}s - Reason: {Reason}",
                        duration.TotalSeconds, outcome.Exception?.Message ?? "HTTP Error"
                    );
                },
                onReset: () => _logger.LogInformation("🟢 [NliClient] Circuit Breaker FECHADO"),
                onHalfOpen: () => _logger.LogInformation("🟡 [NliClient] Circuit Breaker HALF-OPEN")
            );

        // Timeout: 10s para NLI
        _timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(300),
            TimeoutStrategy.Pessimistic,
            onTimeoutAsync: (context, timespan, task) =>
            {
                _logger.LogError("⏱️ [NliClient] Timeout após {Timeout}s", timespan.TotalSeconds);
                return Task.CompletedTask;
            }
        );

        // Combina todas as políticas: Timeout → Retry → Circuit Breaker
        _combinedPolicy = Policy.WrapAsync(_timeoutPolicy, _retryPolicy, _circuitBreakerPolicy);
    }

    public async Task<NliClassifyResponse> ClassifyAsync(
        NliClassifyRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation(
                "📡 [TraceId: {TraceId}] [NliClient] Chamando /nli/classify - {BlockCount} blocos",
                traceId, request.TextBlocks.Count
            );

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Adiciona headers de trace
            content.Headers.Add("X-Trace-ID", traceId);
            content.Headers.Add("X-Request-ID", Guid.NewGuid().ToString("N")[..12]);

            // Executa com políticas Polly
            var response = await _combinedPolicy.ExecuteAsync(async () =>
            {
                var httpResponse = await _httpClient.PostAsync("/nli/classify", content, cancellationToken);
                httpResponse.EnsureSuccessStatusCode();
                return httpResponse;
            });

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<NliClassifyResponse>(responseBody, _jsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException("NLI response é nula");
            }

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation(
                "✅ [TraceId: {TraceId}] [NliClient] Resposta recebida em {Duration}ms - {LabelCount} labels detectadas, cache_hits: {CacheHits}",
                traceId, duration, result.LabelsDetected.Count, result.CacheHits
            );

            return result;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(
                "🔴 [TraceId: {TraceId}] [NliClient] Circuit Breaker aberto: {Message}",
                traceId, ex.Message
            );

            // Retorna resposta vazia em caso de circuit breaker aberto
            return new NliClassifyResponse
            {
                ClassifiedBlocks = new List<ClassifiedBlock>(),
                LabelsDetected = new List<string>(),
                ProcessingTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                CacheHits = 0,
                TotalBlocks = 0
            };
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(
                "⏱️ [TraceId: {TraceId}] [NliClient] Timeout: {Message}",
                traceId, ex.Message
            );
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                "❌ [TraceId: {TraceId}] [NliClient] HTTP Error: {Message}",
                traceId, ex.Message
            );
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "❌ [TraceId: {TraceId}] [NliClient] Erro inesperado: {Message}",
                traceId, ex.Message
            );
            throw;
        }
    }
}
