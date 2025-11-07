using Enter_Extractor_Api.Models.Cache;
using Enter_Extractor_Api.Models.Extractor;

namespace Enter_Extractor_Api.Services.Cache;

/// <summary>
/// Interface para cache de extrações de PDF
/// </summary>
public interface IExtractionCacheService
{
    /// <summary>
    /// Obtém uma extração do cache
    /// </summary>
    Task<ExtractorResponse?> GetCachedExtractionAsync(string label, string pdfHash, Dictionary<string, string> schema);

    /// <summary>
    /// Salva uma extração no cache
    /// </summary>
    Task<bool> SaveExtractionAsync(
        string label,
        string pdfHash,
        Dictionary<string, string> schema,
        ExtractorResponse response,
        long processingTimeMs,
        int tokensUsed = 0,
        double costUsd = 0,
        string strategiesUsed = "",
        long pdfSizeBytes = 0,
        string? extractedText = null);

    /// <summary>
    /// Calcula hash SHA-256 do PDF
    /// </summary>
    string CalculatePdfHash(byte[] pdfBytes);

    /// <summary>
    /// Calcula hash SHA-256 do schema
    /// </summary>
    string CalculateSchemaHash(Dictionary<string, string> schema);

    /// <summary>
    /// Remove uma extração do cache
    /// </summary>
    Task<bool> DeleteCachedExtractionAsync(string label, string pdfHash, Dictionary<string, string> schema);

    /// <summary>
    /// Busca todas as extrações anteriores do mesmo PDF (independente do schema)
    /// </summary>
    Task<List<CachedExtraction>> GetAllPdfExtractionsAsync(string pdfHash);

    /// <summary>
    /// Obtém valores já extraídos de um PDF (de todos os schemas anteriores)
    /// </summary>
    Task<Dictionary<string, object>> GetPreviouslyExtractedValuesAsync(string pdfHash);

    /// <summary>
    /// Obtém o texto extraído de um PDF do cache
    /// </summary>
    Task<string?> GetCachedExtractedTextAsync(string pdfHash);

    /// <summary>
    /// Remove do texto os valores já encontrados no cache
    /// </summary>
    string RemoveCachedValuesFromText(string text, Dictionary<string, object> cachedValues);

    /// <summary>
    /// Mescla valores do cache com valores recém-extraídos
    /// </summary>
    ExtractorResponse MergeResults(
        Dictionary<string, string> requestedSchema,
        Dictionary<string, object> cachedValues,
        ExtractorResponse newExtraction);
}
