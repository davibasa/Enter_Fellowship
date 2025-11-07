using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.Cache;

/// <summary>
/// Resultado de extração armazenado em cache
/// </summary>
public class CachedExtraction
{
    /// <summary>
    /// Resultado completo serializado (ExtractorResponse em JSON)
    /// </summary>
    [JsonPropertyName("result_json")]
    public string ResultJson { get; set; } = string.Empty;

    /// <summary>
    /// Label do documento (ex: carteira_oab, extrato_bancario)
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Hash SHA-256 do PDF
    /// </summary>
    [JsonPropertyName("pdf_hash")]
    public string PdfHash { get; set; } = string.Empty;

    /// <summary>
    /// Texto extraído do PDF (armazenado uma vez por PDF)
    /// </summary>
    [JsonPropertyName("extracted_text")]
    public string? ExtractedText { get; set; }

    /// <summary>
    /// Tamanho do PDF em bytes
    /// </summary>
    [JsonPropertyName("pdf_size_bytes")]
    public long PdfSizeBytes { get; set; }

    /// <summary>
    /// Timestamp ISO 8601 da extração
    /// </summary>
    [JsonPropertyName("extracted_at")]
    public DateTime ExtractedAt { get; set; }

    /// <summary>
    /// Tempo total de processamento em millisegundos
    /// </summary>
    [JsonPropertyName("processing_time_ms")]
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Tokens GPT consumidos
    /// </summary>
    [JsonPropertyName("tokens_used")]
    public int TokensUsed { get; set; }

    /// <summary>
    /// Custo estimado em USD
    /// </summary>
    [JsonPropertyName("cost_usd")]
    public double CostUsd { get; set; }

    /// <summary>
    /// Quantidade total de campos no schema
    /// </summary>
    [JsonPropertyName("fields_total")]
    public int FieldsTotal { get; set; }

    /// <summary>
    /// Quantidade de campos extraídos com sucesso
    /// </summary>
    [JsonPropertyName("fields_extracted")]
    public int FieldsExtracted { get; set; }

    /// <summary>
    /// Taxa de sucesso (fields_extracted / fields_total)
    /// </summary>
    [JsonPropertyName("success_rate")]
    public double SuccessRate { get; set; }

    /// <summary>
    /// Resumo das estratégias usadas (formato: TIPO:COUNT)
    /// </summary>
    [JsonPropertyName("strategies_used")]
    public string StrategiesUsed { get; set; } = string.Empty;

    /// <summary>
    /// Versão do formato de cache (para migrações futuras)
    /// </summary>
    [JsonPropertyName("cache_version")]
    public string CacheVersion { get; set; } = "1.0";
}
