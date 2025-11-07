using Enter_Extractor_Api.Models.History;

namespace Enter_Extractor_Api.Services.History;

/// <summary>
/// Interface para gerenciamento de histórico de extrações
/// </summary>
public interface IHistoryService
{
    /// <summary>
    /// Salvar extração no histórico
    /// </summary>
    Task<string> SaveAsync(string userId, ExtractionHistoryItem item);

    /// <summary>
    /// Obter extração específica
    /// </summary>
    Task<ExtractionHistoryItem?> GetByIdAsync(string userId, string extractionId);

    /// <summary>
    /// Listar histórico do usuário
    /// </summary>
    Task<(List<ExtractionHistoryItem> Items, int Total)> ListAsync(
        string userId,
        int page = 1,
        int limit = 20,
        string? label = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// Deletar extração do histórico
    /// </summary>
    Task<bool> DeleteAsync(string userId, string extractionId);
}
