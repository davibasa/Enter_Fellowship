using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.Template;

/// <summary>
/// Request para criar novo template
/// </summary>
public class CreateTemplateRequest
{
    /// <summary>
    /// Nome do template
    /// </summary>
    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Nome deve ter entre 3 e 100 caracteres")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descrição do template
    /// </summary>
    [Required(ErrorMessage = "Descrição é obrigatória")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Descrição deve ter entre 10 e 500 caracteres")]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Schema de extração (campo -> descrição)
    /// </summary>
    [Required(ErrorMessage = "Schema é obrigatório")]
    [JsonPropertyName("schema")]
    public Dictionary<string, string> Schema { get; set; } = new();

    /// <summary>
    /// Tags para busca
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Se está disponível para todos os usuários
    /// </summary>
    [JsonPropertyName("is_public")]
    public bool IsPublic { get; set; } = false;
}
