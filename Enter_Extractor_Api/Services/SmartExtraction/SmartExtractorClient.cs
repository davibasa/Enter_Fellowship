using System.Text;
using System.Text.Json;
using Enter_Extractor_Api.Models.SmartExtraction;

namespace Enter_Extractor_Api.Services.SmartExtraction;

/// <summary>
/// Interface para o cliente do Smart Extraction Python API
/// </summary>
public interface ISmartExtractorClient
{
    /// <summary>
    /// Extrai campos usando pipeline completo: Cache → NER → Embeddings → GPT (condicional)
    /// </summary>
    Task<EnhancedSmartExtractionResponse> SmartExtractAsync(
        EnhancedSmartExtractionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// FASE 2.5 - Extração inteligente com NER + Embeddings + Cache Redis
    /// Usado após FASE 2 (remoção de labels) quando confiança < threshold
    /// </summary>
    Task<Phase25SmartExtractResponse> SmartExtractPhase25Async(
        string? label,
        string text,
        Dictionary<string, string> schema,
        float confidenceThreshold = 0.7f,
        bool enableGptFallback = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se o serviço Python está saudável
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cliente HTTP para o Smart Extraction Python API
/// Implementa pipeline: Cache → NER → Embeddings → GPT (condicional)
/// </summary>
public class SmartExtractorClient : ISmartExtractorClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmartExtractorClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SmartExtractorClient(
        HttpClient httpClient,
        ILogger<SmartExtractorClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public async Task<EnhancedSmartExtractionResponse> SmartExtractAsync(
        EnhancedSmartExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Chamando Smart Extraction Python API para {FieldCount} campos",
                request.Schema.Count);

            // Serializar request
            var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Chamar API Python
            var response = await _httpClient.PostAsync(
                "/smart-extract",
                content,
                cancellationToken);

            // Validar resposta
            response.EnsureSuccessStatusCode();

            // Deserializar response
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<EnhancedSmartExtractionResponse>(
                responseBody,
                _jsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException("Resposta vazia do Smart Extraction");
            }

            _logger.LogInformation(
                "Smart Extraction concluído: {ExtractedFields} campos extraídos, " +
                "Cache Hit: {CacheHit}, Métodos: {Methods}, GPT Usado: {GptUsed}",
                result.ExtractedFields.Count,
                result.CacheHit,
                string.Join(", ", result.MethodsUsed),
                result.GptFallbackUsed);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro HTTP ao chamar Smart Extraction Python API");
            throw new InvalidOperationException(
                "Falha na comunicação com Smart Extraction Python API", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Erro ao deserializar resposta do Smart Extraction");
            throw new InvalidOperationException(
                "Resposta inválida do Smart Extraction Python API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no Smart Extraction");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Phase25SmartExtractResponse> SmartExtractPhase25Async(
        string? label,
        string text,
        Dictionary<string, string> schema,
        float confidenceThreshold = 0.7f,
        bool enableGptFallback = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🧠 FASE 2.5 - Chamando Python API /smart-extract...");
            _logger.LogInformation("📝 Label: {Label}", label ?? "N/A");
            _logger.LogInformation("📦 Schema: {Count} campos", schema.Count);
            _logger.LogInformation("📄 Texto: {Length} caracteres", text.Length);
            _logger.LogInformation("🤖 GPT Fallback: {Enabled}", enableGptFallback ? "HABILITADO" : "DESABILITADO");

            var payload = new
            {
                label = label,
                text = text,
                schema = schema,
                confidence_threshold = confidenceThreshold,
                enable_gpt_fallback = enableGptFallback
            };

            var jsonContent = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/smart-extract", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<Phase25SmartExtractResponse>(responseContent, _jsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException("Resposta do Python API é nula");
            }

            _logger.LogInformation("✅ Python API respondeu:");
            _logger.LogInformation("  📊 Campos extraídos: {Count}", result.Fields.Count);
            _logger.LogInformation("  🎯 Confiança média: {Confidence:F3}", result.ConfidenceAvg);
            _logger.LogInformation("  ⏱️ Tempo: {Time}ms", result.ProcessingTimeMs);
            _logger.LogInformation("  💾 Cache hit: {CacheHit}", result.CacheHit);
            _logger.LogInformation("  🤖 GPT usado: {GptUsed}", result.GptFallbackUsed ? "SIM" : "NÃO");

            if (result.MethodsUsed.Any())
            {
                _logger.LogInformation("  🔧 Métodos usados: {Methods}",
                    string.Join(", ", result.MethodsUsed.Select(m => $"{m.Key}={m.Value}")));
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de comunicação com Python API /smart-extract");
            throw new InvalidOperationException(
                "Falha ao comunicar com Python API /smart-extract", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Erro ao deserializar resposta de /smart-extract");
            throw new InvalidOperationException(
                "Resposta inválida do endpoint /smart-extract", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado em /smart-extract");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check do Smart Extraction falhou");
            return false;
        }
    }
}
