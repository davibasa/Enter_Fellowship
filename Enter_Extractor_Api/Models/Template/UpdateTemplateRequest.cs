using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.Template;

/// <summary>
/// Request para atualizar template existente
/// </summary>
public class UpdateTemplateRequest
{
    /// <summary>
    /// Nome do template (opcional)
    /// </summary>
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Nome deve ter entre 3 e 100 caracteres")]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Descrição do template (opcional)
    /// </summary>
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Descrição deve ter entre 10 e 500 caracteres")]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Schema de extração (opcional)
    /// </summary>
    [JsonPropertyName("schema")]
    public Dictionary<string, string>? Schema { get; set; }

    /// <summary>
    /// Tags para busca (opcional)
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Se está disponível para todos os usuários (opcional)
    /// </summary>
    [JsonPropertyName("is_public")]
    public bool? IsPublic { get; set; }
}
