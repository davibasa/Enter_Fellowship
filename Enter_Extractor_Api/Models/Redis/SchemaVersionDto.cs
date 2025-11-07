using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.Redis;

/// <summary>
/// Representa uma versão de schema salva no Redis
/// </summary>
public class SchemaVersionDto
{
    /// <summary>
    /// ID único da versão do schema
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Label/tipo de documento
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// ID do template (se vier de um template)
    /// </summary>
    [JsonPropertyName("template_id")]
    public string? TemplateId { get; set; }

    /// <summary>
    /// Schema completo {campo: descrição}
    /// </summary>
    [JsonPropertyName("schema")]
    public Dictionary<string, string> Schema { get; set; } = new();

    /// <summary>
    /// Hash único do schema (para identificar versões idênticas)
    /// </summary>
    [JsonPropertyName("schema_hash")]
    public string SchemaHash { get; set; } = string.Empty;

    /// <summary>
    /// Quantidade de vezes que este schema foi usado
    /// </summary>
    [JsonPropertyName("usage_count")]
    public int UsageCount { get; set; } = 0;

    /// <summary>
    /// Data de criação (primeira vez usado)
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data da última utilização
    /// </summary>
    [JsonPropertyName("last_used_at")]
    public DateTime LastUsedAt { get; set; }

    /// <summary>
    /// Média de taxa de sucesso das extrações com este schema
    /// </summary>
    [JsonPropertyName("avg_success_rate")]
    public float AvgSuccessRate { get; set; }

    /// <summary>
    /// Tempo médio de processamento (ms)
    /// </summary>
    [JsonPropertyName("avg_processing_time_ms")]
    public int AvgProcessingTimeMs { get; set; }

    /// <summary>
    /// Lista de IDs de extrações que usaram este schema
    /// </summary>
    [JsonPropertyName("extraction_ids")]
    public List<string> ExtractionIds { get; set; } = new();

    /// <summary>
    /// Se este é o schema padrão para o label
    /// </summary>
    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }

    /// <summary>
    /// Nome amigável da versão (opcional)
    /// </summary>
    [JsonPropertyName("version_name")]
    public string? VersionName { get; set; }

    /// <summary>
    /// Descrição da versão (opcional)
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
