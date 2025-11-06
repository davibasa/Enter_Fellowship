using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.SmartExtraction;

/// <summary>
/// Request para extração semântica via embeddings
/// </summary>
public class SemanticExtractRequest
{
    /// <summary>
    /// Dicionário com {campo: descrição detalhada}
    /// </summary>
    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Texto não estruturado do documento
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Quantidade de top matches para retornar por label (padrão: 3)
    /// </summary>
    [JsonPropertyName("top_k")]
    public int TopK { get; set; } = 3;

    /// <summary>
    /// Tamanho mínimo de tokens para considerar como candidatos (padrão: 2)
    /// </summary>
    [JsonPropertyName("min_token_length")]
    public int MinTokenLength { get; set; } = 2;

    /// <summary>
    /// Score mínimo de similaridade (0-1) para incluir um match (padrão: 0.0)
    /// </summary>
    [JsonPropertyName("similarity_threshold")]
    public float SimilarityThreshold { get; set; } = 0.0f;
}

/// <summary>
/// Match semântico individual com score de similaridade
/// </summary>
public class SemanticMatch
{
    /// <summary>
    /// Texto candidato do documento
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Score de similaridade cosine (0-1)
    /// </summary>
    [JsonPropertyName("score")]
    public float Score { get; set; }

    /// <summary>
    /// Ranking do match (1, 2, 3...)
    /// </summary>
    [JsonPropertyName("rank")]
    public int Rank { get; set; }
}

/// <summary>
/// Resultado de extração para um label específico
/// </summary>
public class LabelExtractionResult
{
    /// <summary>
    /// Nome do campo/label
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Descrição do campo
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Top K candidatos ordenados por score
    /// </summary>
    [JsonPropertyName("top_matches")]
    public List<SemanticMatch> TopMatches { get; set; } = new();

    /// <summary>
    /// Melhor candidato (maior score)
    /// </summary>
    [JsonPropertyName("best_match")]
    public string BestMatch { get; set; } = string.Empty;

    /// <summary>
    /// Score do melhor candidato
    /// </summary>
    [JsonPropertyName("best_score")]
    public float BestScore { get; set; }
}

/// <summary>
/// Response completa da extração semântica
/// </summary>
public class SemanticExtractResponse
{
    /// <summary>
    /// Resultados detalhados por label
    /// </summary>
    [JsonPropertyName("results")]
    public List<LabelExtractionResult> Results { get; set; } = new();

    /// <summary>
    /// Extração final sugerida {campo: valor}
    /// </summary>
    [JsonPropertyName("extraction_summary")]
    public Dictionary<string, string> ExtractionSummary { get; set; } = new();

    /// <summary>
    /// Tempo de processamento em milissegundos
    /// </summary>
    [JsonPropertyName("processing_time_ms")]
    public int ProcessingTimeMs { get; set; }

    /// <summary>
    /// Total de candidatos avaliados
    /// </summary>
    [JsonPropertyName("total_candidates")]
    public int TotalCandidates { get; set; }

    /// <summary>
    /// Modelo de embedding usado
    /// </summary>
    [JsonPropertyName("model_used")]
    public string ModelUsed { get; set; } = string.Empty;
}
