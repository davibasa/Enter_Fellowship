using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.History;

/// <summary>
/// Item de histórico de extração
/// </summary>
public class ExtractionHistoryItem
{
    /// <summary>
    /// ID único da extração
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// ID do usuário
    /// </summary>
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Hash SHA-256 do PDF
    /// </summary>
    [JsonPropertyName("pdf_hash")]
    public string PdfHash { get; set; } = string.Empty;

    /// <summary>
    /// Nome original do arquivo
    /// </summary>
    [JsonPropertyName("pdf_filename")]
    public string PdfFilename { get; set; } = string.Empty;

    /// <summary>
    /// Tamanho do PDF em bytes
    /// </summary>
    [JsonPropertyName("pdf_size_bytes")]
    public long PdfSizeBytes { get; set; }

    /// <summary>
    /// Label do documento
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// ID do template usado (se aplicável)
    /// </summary>
    [JsonPropertyName("template_id")]
    public string? TemplateId { get; set; }

    /// <summary>
    /// Nome do template (cache)
    /// </summary>
    [JsonPropertyName("template_name")]
    public string? TemplateName { get; set; }

    /// <summary>
    /// Timestamp da extração
    /// </summary>
    [JsonPropertyName("extracted_at")]
    public DateTime ExtractedAt { get; set; }

    /// <summary>
    /// Tempo de processamento em ms
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
    /// Total de campos no schema
    /// </summary>
    [JsonPropertyName("fields_total")]
    public int FieldsTotal { get; set; }

    /// <summary>
    /// Campos extraídos com sucesso
    /// </summary>
    [JsonPropertyName("fields_extracted")]
    public int FieldsExtracted { get; set; }

    /// <summary>
    /// Campos que falharam
    /// </summary>
    [JsonPropertyName("fields_failed")]
    public int FieldsFailed { get; set; }

    /// <summary>
    /// Taxa de sucesso (0-1)
    /// </summary>
    [JsonPropertyName("success_rate")]
    public double SuccessRate { get; set; }

    /// <summary>
    /// Se usou cache
    /// </summary>
    [JsonPropertyName("used_cache")]
    public bool UsedCache { get; set; }

    /// <summary>
    /// Resumo das estratégias usadas (JSON)
    /// </summary>
    [JsonPropertyName("strategies_json")]
    public string StrategiesJson { get; set; } = string.Empty;

    /// <summary>
    /// Resultado completo serializado (JSON)
    /// </summary>
    [JsonPropertyName("result_json")]
    public string ResultJson { get; set; } = string.Empty;

    /// <summary>
    /// Se usuário editou resultado manualmente
    /// </summary>
    [JsonPropertyName("edited_manually")]
    public bool EditedManually { get; set; }

    /// <summary>
    /// IP do cliente (opcional, LGPD)
    /// </summary>
    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent (opcional)
    /// </summary>
    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Versão do formato
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;
}
