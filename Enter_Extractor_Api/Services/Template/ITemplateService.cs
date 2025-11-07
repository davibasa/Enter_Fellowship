using Enter_Extractor_Api.Models.Template;

namespace Enter_Extractor_Api.Services.Template;

/// <summary>
/// Interface para gerenciamento de templates de schema
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Criar novo template
    /// </summary>
    Task<Models.Template.Template> CreateAsync(string userId, CreateTemplateRequest request);

    /// <summary>
    /// Obter template por ID
    /// </summary>
    Task<Models.Template.Template?> GetByIdAsync(string userId, string templateId);

    /// <summary>
    /// Listar templates do usuário
    /// </summary>
    Task<(List<Models.Template.Template> Templates, int Total)> ListUserTemplatesAsync(
        string userId,
        int page = 1,
        int limit = 20,
        string? searchQuery = null,
        List<string>? tags = null);

    /// <summary>
    /// Listar templates públicos (ordenados por popularidade)
    /// </summary>
    Task<List<Models.Template.Template>> ListPublicTemplatesAsync(int limit = 10);

    /// <summary>
    /// Atualizar template
    /// </summary>
    Task<Models.Template.Template> UpdateAsync(string userId, string templateId, UpdateTemplateRequest request);

    /// <summary>
    /// Deletar template (soft delete)
    /// </summary>
    Task<bool> DeleteAsync(string userId, string templateId);

    /// <summary>
    /// Incrementar contador de uso do template
    /// </summary>
    Task IncrementUsageAsync(string userId, string templateId, double successRate, long processingTimeMs);

    /// <summary>
    /// Buscar templates por nome ou tag
    /// </summary>
    Task<List<Models.Template.Template>> SearchAsync(string userId, string query);

    /// <summary>
    /// Clonar template público
    /// </summary>
    Task<Models.Template.Template> ClonePublicTemplateAsync(string userId, string templateId);
}
