using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Enter_Extractor_Api.Models.SmartExtraction;

namespace Enter_Extractor_Api.Services.SmartExtraction;

/// <summary>
/// Interface para comunicação com Python FastAPI (NLI + Smart Extraction)
/// </summary>
public interface IPythonExtractorClient
{
    /// <summary>
    /// Classifica blocos de texto via Zero-Shot NLI para detectar labels vs valores
    /// </summary>
    Task<NliClassificationResponse> ClassifyNliAsync(
        string? label,
        Dictionary<string, string> schema,
        IEnumerable<string> textBlocks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extração inteligente com pipeline completo: Cache → NER → Embeddings → GPT
    /// </summary>
    Task<SmartExtractionPythonResponse> SmartExtractAsync(
        string? label,
        string text,
        Dictionary<string, string> schema,
        float confidenceThreshold = 0.6f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Health check do serviço Python
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    Task<SmartExtractResponse> EmbeddingsPythonAsync(
        string? label,
        string text,
        Dictionary<string, string> schema,
        float confidenceThreshold = 0.7f,
        bool enableGptFallback = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extração semântica pura com embeddings (sem NER, sem GPT)
    /// Retorna top K matches para cada label baseado em similaridade cosine
    /// </summary>
    Task<SemanticExtractResponse> SemanticExtractAsync(
        Dictionary<string, string> labels,
        string text,
        int topK = 3,
        int minTokenLength = 2,
        float similarityThreshold = 0.0f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detecção de labels no texto (LÓGICA INVERTIDA)
    /// Identifica quais labels do schema existem no documento
    /// Útil para FASE 2 (remoção de labels do documento)
    /// </summary>
    Task<SemanticLabelDetectResponse> SemanticLabelDetectAsync(
        Dictionary<string, string> labels,
        string text,
        int topK = 3,
        int minTokenLength = 3,
        float similarityThreshold = 0.5f,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cliente HTTP para comunicação com Python FastAPI
/// Implementa os endpoints: /nli/classify e /smart-extract
/// </summary>
public class PythonExtractorClient : IPythonExtractorClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PythonExtractorClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public PythonExtractorClient(
        HttpClient httpClient,
        ILogger<PythonExtractorClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<SmartExtractResponse> EmbeddingsPythonAsync(
        string? label,
        string text,
        Dictionary<string, string> schema,
        float confidenceThreshold = 0.7f,
        bool enableGptFallback = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
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
            var result = JsonSerializer.Deserialize<SmartExtractResponse>(responseContent, _jsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException("Resposta do Python API é nula");
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

    public async Task<NliClassificationResponse> ClassifyNliAsync(
        string? label,
        Dictionary<string, string> schema,
        IEnumerable<string> textBlocks,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blocksList = textBlocks.ToList();
            _logger.LogInformation(
                "📤 Chamando /nli/classify | Label: {Label} | Schema: {SchemaCount} campos | Blocos: {BlockCount}",
                label ?? "N/A",
                schema.Count,
                blocksList.Count);

            var requestPayload = new
            {
                label,
                schema,
                text_blocks = blocksList
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/nli/classify", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<NliClassificationResponse>(responseBody, _jsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException("Resposta vazia do endpoint /nli/classify");
            }

            _logger.LogInformation(
                "📥 /nli/classify concluído | Labels detectadas: {LabelsCount}/{TotalBlocks} | Cache hits: {CacheHits} | Tempo: {Time}ms",
                result.LabelsDetected.Count,
                result.TotalBlocks,
                result.CacheHits,
                result.ProcessingTimeMs);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Erro HTTP ao chamar /nli/classify");
            throw new InvalidOperationException("Falha na comunicação com Python API (/nli/classify)", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ Erro ao deserializar resposta do /nli/classify");
            throw new InvalidOperationException("Resposta inválida do Python API", ex);
        }
    }

    /// <inheritdoc />
    public async Task<SmartExtractionPythonResponse> SmartExtractAsync(
        string? label,
        string text,
        Dictionary<string, string> schema,
        float confidenceThreshold = 0.6f,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "📤 Chamando /smart-extract | Label: {Label} | Schema: {SchemaCount} campos | Texto: {TextLength} chars | Threshold: {Threshold}",
                label ?? "N/A",
                schema.Count,
                text.Length,
                confidenceThreshold);

            var requestPayload = new
            {
                text,
                schema,
                label,
                confidence_threshold = confidenceThreshold
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/smart-extract", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<SmartExtractionPythonResponse>(responseBody, _jsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException("Resposta vazia do endpoint /smart-extract");
            }

            _logger.LogInformation(
                "📥 /smart-extract concluído | Cache Hit: {CacheHit} | Confiança: {Confidence:F2} | " +
                "Tempo: {Time}ms | Métodos: {Methods} | GPT: {Gpt}",
                result.CacheHit,
                result.AvgConfidence,
                result.ProcessingTimeMs,
                string.Join(", ", result.MethodsUsed.Select(kvp => $"{kvp.Key}={kvp.Value}")),
                result.GptFallbackUsed);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Erro HTTP ao chamar /smart-extract");
            throw new InvalidOperationException("Falha na comunicação com Python API (/smart-extract)", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ Erro ao deserializar resposta do /smart-extract");
            throw new InvalidOperationException("Resposta inválida do Python API", ex);
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
            _logger.LogWarning(ex, "⚠️ Health check do Python API falhou");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<SemanticExtractResponse> SemanticExtractAsync(
        Dictionary<string, string> labels,
        string text,
        int topK = 3,
        int minTokenLength = 2,
        float similarityThreshold = 0.0f,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "📤 Chamando /semantic-extract | Labels: {LabelCount} | Texto: {TextLength} chars | TopK: {TopK} | MinToken: {MinToken} | Threshold: {Threshold}",
                labels.Count,
                text.Length,
                topK,
                minTokenLength,
                similarityThreshold);

            var requestPayload = new SemanticExtractRequest
            {
                Labels = labels,
                Text = text,
                TopK = topK,
                MinTokenLength = minTokenLength,
                SimilarityThreshold = similarityThreshold
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/semantic-extract", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<SemanticExtractResponse>(responseBody, _jsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException("Resposta vazia do endpoint /semantic-extract");
            }

            _logger.LogInformation(
                "📥 /semantic-extract concluído | Candidatos: {Candidates} | Tempo: {Time}ms | Modelo: {Model} | Campos: {FieldCount}",
                result.TotalCandidates,
                result.ProcessingTimeMs,
                result.ModelUsed,
                result.Results.Count);

            // Log de confiança por campo
            foreach (var fieldResult in result.Results)
            {
                var emoji = fieldResult.BestScore >= 0.7 ? "🟢" : fieldResult.BestScore >= 0.5 ? "🟡" : "🔴";
                _logger.LogDebug(
                    "   {Emoji} {Label}: '{Value}' (score: {Score:F3})",
                    emoji,
                    fieldResult.Label,
                    fieldResult.BestMatch,
                    fieldResult.BestScore);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Erro HTTP ao chamar /semantic-extract");
            throw new InvalidOperationException("Falha na comunicação com Python API (/semantic-extract)", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ Erro ao deserializar resposta do /semantic-extract");
            throw new InvalidOperationException("Resposta inválida do Python API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro inesperado em /semantic-extract");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SemanticLabelDetectResponse> SemanticLabelDetectAsync(
        Dictionary<string, string> labels,
        string text,
        int topK = 3,
        int minTokenLength = 3,
        float similarityThreshold = 0.5f,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "📤 Chamando /semantic-label-detect | Labels: {LabelCount} | Texto: {TextLength} chars | TopK: {TopK} | MinToken: {MinToken} | Threshold: {Threshold}",
                labels.Count,
                text.Length,
                topK,
                minTokenLength,
                similarityThreshold);

            var requestPayload = new SemanticLabelDetectRequest
            {
                Labels = labels,
                Text = text,
                TopK = topK,
                MinTokenLength = minTokenLength,
                SimilarityThreshold = similarityThreshold
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/semantic-label-detect", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<SemanticLabelDetectResponse>(responseBody, _jsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException("Resposta vazia do endpoint /semantic-label-detect");
            }

            _logger.LogInformation(
                "📥 /semantic-label-detect concluído | Labels detectadas: {DetectedCount} | Candidatos: {Candidates} | Tempo: {Time}ms | Modelo: {Model}",
                result.DetectedLabels.Count,
                result.TotalCandidates,
                result.ProcessingTimeMs,
                result.ModelUsed);

            // Log das labels detectadas
            // foreach (var detected in result.DetectedLabels)
            // {
            //     var emoji = detected.Score >= 0.7 ? "🟢" : detected.Score >= 0.5 ? "🟡" : "🔴";
            //     _logger.LogDebug(
            //         "   {Emoji} '{Candidate}' → Label '{Label}' (score: {Score:F3})",
            //         emoji,
            //         detected.CandidateText,
            //         detected.MatchedLabel,
            //         detected.Score);
            // }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Erro HTTP ao chamar /semantic-label-detect");
            throw new InvalidOperationException("Falha na comunicação com Python API (/semantic-label-detect)", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ Erro ao deserializar resposta do /semantic-label-detect");
            throw new InvalidOperationException("Resposta inválida do Python API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro inesperado em /semantic-label-detect");
            throw;
        }
    }
}

// ============================================================================
// MODELOS PARA COMUNICAÇÃO COM PYTHON API
// ============================================================================

/// <summary>
/// Response do endpoint /nli/classify (classificação Zero-Shot de labels vs valores)
/// </summary>
public class NliClassificationResponse
{
    [JsonPropertyName("labels_detected")]
    public List<string> LabelsDetected { get; set; } = new();

    [JsonPropertyName("classified_blocks")]
    public List<ClassifiedBlock> ClassifiedBlocks { get; set; } = new();

    [JsonPropertyName("processing_time_ms")]
    public int ProcessingTimeMs { get; set; }

    [JsonPropertyName("cache_hits")]
    public int CacheHits { get; set; }

    [JsonPropertyName("total_blocks")]
    public int TotalBlocks { get; set; }
}

/// <summary>
/// Bloco de texto classificado como "label" ou "valor"
/// </summary>
public class ClassifiedBlock
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty; // "label" ou "valor"

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }
}

/// <summary>
/// Response do endpoint /smart-extract (Python side)
/// </summary>
public class SmartExtractionPythonResponse
{
    [JsonPropertyName("fields")]
    public Dictionary<string, PythonFieldResult> Fields { get; set; } = new();

    [JsonPropertyName("avg_confidence")]
    public float AvgConfidence { get; set; }

    [JsonPropertyName("cache_hit")]
    public bool CacheHit { get; set; }

    [JsonPropertyName("processing_time_ms")]
    public int ProcessingTimeMs { get; set; }

    [JsonPropertyName("methods_used")]
    public Dictionary<string, int> MethodsUsed { get; set; } = new();

    [JsonPropertyName("gpt_fallback_used")]
    public bool GptFallbackUsed { get; set; }
}

/// <summary>
/// Resultado de campo extraído pelo Python
/// </summary>
public class PythonFieldResult
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("line_index")]
    public int LineIndex { get; set; } = -1;
}

public class SmartExtractResponse
{
    /// <summary>
    /// Campos extraídos com seus valores
    /// Formato: { "campo": "valor" } ou { "campo": null }
    /// </summary>
    [JsonPropertyName("fields")]
    public Dictionary<string, string?> Fields { get; set; } = new Dictionary<string, string?>();
}

// ============================================================================
// MODELOS PARA /semantic-label-detect (Detecção de Labels)
// ============================================================================

/// <summary>
/// Request para /semantic-label-detect (detecção de labels no texto)
/// </summary>
public class SemanticLabelDetectRequest
{
    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = new();

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("top_k")]
    public int TopK { get; set; } = 3;

    [JsonPropertyName("min_token_length")]
    public int MinTokenLength { get; set; } = 3;

    [JsonPropertyName("similarity_threshold")]
    public float SimilarityThreshold { get; set; } = 0.5f;
}

/// <summary>
/// Match de candidato → label detectada
/// </summary>
public class CandidateLabelMatch
{
    [JsonPropertyName("candidate_text")]
    public string CandidateText { get; set; } = string.Empty;

    [JsonPropertyName("matched_label")]
    public string MatchedLabel { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public float Score { get; set; }

    [JsonPropertyName("rank")]
    public int Rank { get; set; }
}

/// <summary>
/// Response do /semantic-label-detect (labels detectadas no documento)
/// </summary>
public class SemanticLabelDetectResponse
{
    [JsonPropertyName("detected_labels")]
    public List<CandidateLabelMatch> DetectedLabels { get; set; } = new();

    [JsonPropertyName("labels_summary")]
    public Dictionary<string, string> LabelsSummary { get; set; } = new();

    [JsonPropertyName("processing_time_ms")]
    public int ProcessingTimeMs { get; set; }

    [JsonPropertyName("total_candidates")]
    public int TotalCandidates { get; set; }

    [JsonPropertyName("model_used")]
    public string ModelUsed { get; set; } = string.Empty;
}