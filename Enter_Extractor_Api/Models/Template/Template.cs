using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.Template;

/// <summary>
/// Template de schema para extração
/// </summary>
public class Template
{
    /// <summary>
    /// ID único do template (UUID)
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// ID do usuário dono do template
    /// </summary>
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Nome do template
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descrição detalhada do template
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Schema serializado em JSON (Dictionary<string, string>)
    /// </summary>
    [JsonPropertyName("schema_json")]
    public string SchemaJson { get; set; } = string.Empty;

    /// <summary>
    /// Schema deserializado (computed property)
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, string>? Schema { get; set; }

    /// <summary>
    /// Data de criação (ISO 8601)
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data da última atualização
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Tags separadas por vírgula (para busca)
    /// </summary>
    [JsonPropertyName("tags")]
    public string Tags { get; set; } = string.Empty;

    /// <summary>
    /// Tags deserializadas (computed property)
    /// </summary>
    [JsonIgnore]
    public List<string> TagList => Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>
    /// Se está disponível para todos os usuários
    /// </summary>
    [JsonPropertyName("is_public")]
    public bool IsPublic { get; set; }

    /// <summary>
    /// Soft delete (não remove do Redis)
    /// </summary>
    [JsonPropertyName("is_deleted")]
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Quantas vezes foi usado
    /// </summary>
    [JsonPropertyName("usage_count")]
    public int UsageCount { get; set; }

    /// <summary>
    /// Taxa média de sucesso (0-1)
    /// </summary>
    [JsonPropertyName("avg_success_rate")]
    public double AvgSuccessRate { get; set; }

    /// <summary>
    /// Tempo médio de processamento em ms
    /// </summary>
    [JsonPropertyName("avg_processing_time_ms")]
    public long AvgProcessingTimeMs { get; set; }

    /// <summary>
    /// Última vez que foi usado
    /// </summary>
    [JsonPropertyName("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Versão do template (para versionamento)
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; }

    /// <summary>
    /// Quantidade de campos no schema
    /// </summary>
    [JsonPropertyName("fields_count")]
    public int FieldsCount { get; set; }
}
