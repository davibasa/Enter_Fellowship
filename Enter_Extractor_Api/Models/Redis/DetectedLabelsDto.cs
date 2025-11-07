using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.Redis;

/// <summary>
/// DTO para armazenar labels detectadas por documento no Redis
/// Cache Key: detected_labels:{label}:{pdfHash}
/// TTL: 30 dias
/// </summary>
public class DetectedLabelsDto
{
    /// <summary>
    /// Hash do PDF (SHA256)
    /// </summary>
    [JsonPropertyName("pdf_hash")]
    public string PdfHash { get; set; } = string.Empty;

    /// <summary>
    /// Label do documento (ex: "Carteira OAB", "CPF")
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Schema usado na detecção
    /// </summary>
    [JsonPropertyName("schema")]
    public Dictionary<string, string> Schema { get; set; } = new();

    /// <summary>
    /// Hash do schema (para versionamento)
    /// </summary>
    [JsonPropertyName("schema_hash")]
    public string SchemaHash { get; set; } = string.Empty;

    /// <summary>
    /// Textos detectados como labels (candidatos do documento)
    /// </summary>
    [JsonPropertyName("detected_labels")]
    public List<DetectedLabelMatch> DetectedLabels { get; set; } = new();

    /// <summary>
    /// Total de candidatos analisados
    /// </summary>
    [JsonPropertyName("total_candidates")]
    public int TotalCandidates { get; set; }

    /// <summary>
    /// Modelo usado (ex: "paraphrase-multilingual-mpnet-base-v2")
    /// </summary>
    [JsonPropertyName("model_used")]
    public string ModelUsed { get; set; } = string.Empty;

    /// <summary>
    /// Tempo de processamento em ms
    /// </summary>
    [JsonPropertyName("processing_time_ms")]
    public int ProcessingTimeMs { get; set; }

    /// <summary>
    /// Data/hora da detecção
    /// </summary>
    [JsonPropertyName("detected_at")]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Match de label detectada no texto
/// </summary>
public class DetectedLabelMatch
{
    /// <summary>
    /// Texto candidato do documento (ex: "Nome Completo:")
    /// </summary>
    [JsonPropertyName("candidate_text")]
    public string CandidateText { get; set; } = string.Empty;

    /// <summary>
    /// Label do schema que foi detectada (ex: "nome")
    /// </summary>
    [JsonPropertyName("matched_label")]
    public string MatchedLabel { get; set; } = string.Empty;

    /// <summary>
    /// Score de similaridade (0-1)
    /// </summary>
    [JsonPropertyName("score")]
    public float Score { get; set; }

    /// <summary>
    /// Ranking do match (1 = melhor)
    /// </summary>
    [JsonPropertyName("rank")]
    public int Rank { get; set; }
}
