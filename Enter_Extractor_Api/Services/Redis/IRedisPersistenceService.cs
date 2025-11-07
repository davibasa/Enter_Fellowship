using Enter_Extractor_Api.Models.Redis;

namespace Enter_Extractor_Api.Services.Redis;

public interface IRedisPersistenceService
{
    // ============================================================================
    // TEMPLATES
    // ============================================================================

    Task<string> CreateTemplateAsync(TemplateDto template, CancellationToken cancellationToken = default);
    Task<TemplateDto?> GetTemplateAsync(string userId, string templateId, CancellationToken cancellationToken = default);
    Task<List<TemplateDto>> GetUserTemplatesAsync(string userId, int page = 0, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<bool> UpdateTemplateAsync(TemplateDto template, CancellationToken cancellationToken = default);
    Task<bool> DeleteTemplateAsync(string userId, string templateId, CancellationToken cancellationToken = default);
    Task<bool> IncrementTemplateUsageAsync(string userId, string templateId, CancellationToken cancellationToken = default);

    // ============================================================================
    // HISTÓRICO
    // ============================================================================

    Task<string> SaveExtractionHistoryAsync(ExtractionHistoryDto history, CancellationToken cancellationToken = default);
    Task<ExtractionHistoryDto?> GetExtractionHistoryAsync(string userId, string extractionId, CancellationToken cancellationToken = default);
    Task<List<ExtractionHistoryDto>> GetUserHistoryAsync(string userId, int page = 0, int pageSize = 20, CancellationToken cancellationToken = default);

    // ============================================================================
    // ESTATÍSTICAS
    // ============================================================================

    Task IncrementGlobalStatAsync(string metric, string date, long value = 1, CancellationToken cancellationToken = default);
    Task<long> GetGlobalStatAsync(string metric, string date, CancellationToken cancellationToken = default);
    Task<Dictionary<string, long>> GetGlobalStatsAsync(string metric, List<string> dates, CancellationToken cancellationToken = default);

    // ============================================================================
    // PADRÕES APRENDIDOS
    // ============================================================================

    Task SavePatternAsync(string label, string fieldName, string value, CancellationToken cancellationToken = default);
    Task<List<(string value, double frequency)>> GetTopPatternsAsync(string label, string fieldName, int topN = 5, CancellationToken cancellationToken = default);
    Task<double?> GetPatternFrequencyAsync(string label, string fieldName, string value, CancellationToken cancellationToken = default);

    // ============================================================================
    // VERSÕES DE SCHEMAS
    // ============================================================================

    Task<string> SaveOrUpdateSchemaVersionAsync(string label, Dictionary<string, string> schema, string? templateId = null, string? schemaHash = null, CancellationToken cancellationToken = default);
    Task<SchemaVersionDto?> GetSchemaVersionAsync(string schemaId, CancellationToken cancellationToken = default);
    Task<List<SchemaVersionDto>> GetSchemaVersionsByLabelAsync(string label, CancellationToken cancellationToken = default);
    Task<SchemaVersionDto?> GetSchemaVersionByHashAsync(string label, string schemaHash, CancellationToken cancellationToken = default);
    Task<bool> UpdateSchemaVersionStatsAsync(string schemaId, float successRate, int processingTimeMs, string extractionId, CancellationToken cancellationToken = default);

    // ============================================================================
    // LABELS DETECTADAS (RoBERTa/Embeddings)
    // ============================================================================

    /// <summary>
    /// Salva labels detectadas no documento para cache
    /// Key: detected_labels:{label}:{pdfHash}:{schemaHash}
    /// TTL: 30 dias
    /// </summary>
    Task SaveDetectedLabelsAsync(DetectedLabelsDto detectedLabels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca labels detectadas do cache
    /// </summary>
    Task<DetectedLabelsDto?> GetDetectedLabelsAsync(string label, string pdfHash, string schemaHash, CancellationToken cancellationToken = default);
}
