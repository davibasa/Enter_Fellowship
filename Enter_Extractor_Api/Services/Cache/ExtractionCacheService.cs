using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Enter_Extractor_Api.Models.Cache;
using Microsoft.Extensions.Options;
using Enter_Extractor_Api.Models.Extractor;

namespace Enter_Extractor_Api.Services.Cache;

/// <summary>
/// Serviço de cache para extrações de PDF
/// </summary>
public class ExtractionCacheService : IExtractionCacheService
{
    private readonly IRedisCacheService _redis;
    private readonly ILogger<ExtractionCacheService> _logger;
    private readonly RedisConfiguration _config;
    private readonly IMetricsCacheService _metrics;

    public ExtractionCacheService(
        IRedisCacheService redis,
        ILogger<ExtractionCacheService> logger,
        IOptions<RedisConfiguration> config,
        IMetricsCacheService metrics)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config.Value;
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public async Task<ExtractorResponse?> GetCachedExtractionAsync(string label, string pdfHash, Dictionary<string, string> schema)
    {
        try
        {
            var schemaHash = CalculateSchemaHash(schema);
            var cacheKey = BuildCacheKey(label, pdfHash, schemaHash);
            _logger.LogDebug("Checking cache for key: {CacheKey}", cacheKey);

            var cached = await _redis.HashGetAllAsync<CachedExtraction>(cacheKey);

            if (cached == null)
            {
                _logger.LogDebug("Cache MISS for {Label} (PDF: {PdfHash}, Schema: {SchemaHash})",
                    label, pdfHash[..10], schemaHash[..10]);
                await _metrics.IncrementCacheMissAsync();
                return null;
            }

            _logger.LogInformation("Cache HIT for {Label} (PDF: {PdfHash}, Schema: {SchemaHash}, extracted at {ExtractedAt})",
                label, pdfHash[..10], schemaHash[..10], cached.ExtractedAt);

            var response = JsonSerializer.Deserialize<ExtractorResponse>(cached.ResultJson);

            // Incrementar métricas
            await _metrics.IncrementCacheHitAsync(
                savedTimeMs: cached.ProcessingTimeMs,
                savedCostUsd: cached.CostUsd
            );

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cached extraction for {Label}", label);
            return null;
        }
    }

    public async Task<bool> SaveExtractionAsync(
        string label,
        string pdfHash,
        Dictionary<string, string> schema,
        ExtractorResponse response,
        long processingTimeMs,
        int tokensUsed = 0,
        double costUsd = 0,
        string strategiesUsed = "",
        long pdfSizeBytes = 0,
        string? extractedText = null)
    {
        try
        {
            var schemaHash = CalculateSchemaHash(schema);
            var cacheKey = BuildCacheKey(label, pdfHash, schemaHash);

            var fieldsTotal = response.Schema?.Count ?? 0;
            var fieldsExtracted = response.Schema?.Count(kvp =>
                kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.ToString())) ?? 0;
            var successRate = fieldsTotal > 0 ? (double)fieldsExtracted / fieldsTotal : 0;

            var cached = new CachedExtraction
            {
                ResultJson = JsonSerializer.Serialize(response),
                Label = label,
                PdfHash = pdfHash,
                ExtractedText = extractedText,
                PdfSizeBytes = pdfSizeBytes,
                ExtractedAt = DateTime.UtcNow,
                ProcessingTimeMs = processingTimeMs,
                TokensUsed = tokensUsed,
                CostUsd = costUsd,
                FieldsTotal = fieldsTotal,
                FieldsExtracted = fieldsExtracted,
                SuccessRate = successRate,
                StrategiesUsed = strategiesUsed,
                CacheVersion = "1.0"
            };

            var ttl = TimeSpan.FromSeconds(_config.Cache.DefaultTTLSeconds);
            var success = await _redis.HashSetAllAsync(cacheKey, cached, ttl);

            if (success)
            {
                _logger.LogInformation(
                    "Cached extraction for {Label} (PDF: {PdfHash}, Schema: {SchemaHash}, size: {Size}KB, fields: {Extracted}/{Total})",
                    label, pdfHash[..10], schemaHash[..10], pdfSizeBytes / 1024, fieldsExtracted, fieldsTotal);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving cached extraction for {Label}", label);
            return false;
        }
    }

    public string CalculatePdfHash(byte[] pdfBytes)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(pdfBytes);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var truncateLength = _config.PdfHashing.TruncateLength;
        return hash.Length > truncateLength ? hash[..truncateLength] : hash;
    }

    public string CalculateSchemaHash(Dictionary<string, string> schema)
    {
        // Criar representação determinística do schema (ordenada por chave)
        var schemaJson = JsonSerializer.Serialize(
            schema.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        );

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(schemaJson));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var truncateLength = _config.PdfHashing.TruncateLength;
        return hash.Length > truncateLength ? hash[..truncateLength] : hash;
    }

    public async Task<bool> DeleteCachedExtractionAsync(string label, string pdfHash, Dictionary<string, string> schema)
    {
        try
        {
            var schemaHash = CalculateSchemaHash(schema);
            var cacheKey = BuildCacheKey(label, pdfHash, schemaHash);
            var success = await _redis.DeleteAsync(cacheKey);

            if (success)
            {
                _logger.LogInformation("Deleted cached extraction for {Label} (PDF: {PdfHash}, Schema: {SchemaHash})",
                    label, pdfHash[..10], schemaHash[..10]);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting cached extraction for {Label}", label);
            return false;
        }
    }

    private static string BuildCacheKey(string label, string pdfHash, string schemaHash)
    {
        return $"extraction:{label}:{pdfHash}:{schemaHash}";
    }

    public async Task<List<CachedExtraction>> GetAllPdfExtractionsAsync(string pdfHash)
    {
        try
        {
            var pattern = $"extraction:*:{pdfHash}:*";
            _logger.LogInformation("Scanning Redis for pattern: {Pattern}", pattern);

            var results = new List<CachedExtraction>();

            // Buscar todas as chaves que correspondem ao padrão usando SCAN
            var keys = await _redis.ScanKeysAsync(pattern, count: 1000);

            _logger.LogInformation("Found {Count} cache keys matching pattern", keys.Count);

            if (keys.Count > 0)
            {
                _logger.LogDebug("  Keys found: {Keys}", string.Join(", ", keys));
            }

            // Buscar dados de cada chave
            foreach (var key in keys)
            {
                try
                {
                    var cached = await _redis.HashGetAllAsync<CachedExtraction>(key);
                    if (cached != null)
                    {
                        results.Add(cached);
                        _logger.LogDebug("Retrieved extraction from key: {Key}", key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error retrieving cached extraction from key {Key}", key);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all extractions for PDF {PdfHash}", pdfHash);
            return new List<CachedExtraction>();
        }
    }

    public async Task<Dictionary<string, object>> GetPreviouslyExtractedValuesAsync(string pdfHash)
    {
        try
        {

            var allExtractions = await GetAllPdfExtractionsAsync(pdfHash);


            var mergedValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (var extraction in allExtractions)
            {
                try
                {
                    var response = JsonSerializer.Deserialize<ExtractorResponse>(extraction.ResultJson);
                    if (response?.Schema != null)
                    {

                        foreach (var kvp in response.Schema)
                        {
                            // Adicionar apenas se ainda não existe ou se o valor atual não é nulo
                            if (kvp.Value != null &&
                                (!mergedValues.ContainsKey(kvp.Key) || mergedValues[kvp.Key] == null))
                            {
                                mergedValues[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing cached extraction");
                }
            }

            return mergedValues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting previously extracted values for PDF {PdfHash}", pdfHash);
            return new Dictionary<string, object>();
        }
    }

    public ExtractorResponse MergeResults(
        Dictionary<string, string> requestedSchema,
        Dictionary<string, object> cachedValues,
        ExtractorResponse newExtraction)
    {
        try
        {
            var mergedSchema = new Dictionary<string, object?>();
            var fieldsFromCache = 0;
            var fieldsFromExtraction = 0;

            foreach (var field in requestedSchema.Keys)
            {
                if (newExtraction.Schema?.TryGetValue(field, out var newValue) == true &&
                    newValue != null && !string.IsNullOrEmpty(newValue.ToString()))
                {
                    mergedSchema[field] = newValue;
                    fieldsFromExtraction++;
                }
                else if (cachedValues.TryGetValue(field, out var cachedValue) &&
                         cachedValue != null && !string.IsNullOrEmpty(cachedValue.ToString()))
                {
                    mergedSchema[field] = cachedValue;
                    fieldsFromCache++;
                }
                else
                {
                    mergedSchema[field] = null;
                }
            }

            return new ExtractorResponse
            {
                Schema = mergedSchema
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error merging results");
            return newExtraction;
        }
    }

    public async Task<string?> GetCachedExtractedTextAsync(string pdfHash)
    {
        try
        {
            var extractions = await GetAllPdfExtractionsAsync(pdfHash);

            if (extractions.Count == 0)
            {
                return null;
            }

            var firstExtraction = extractions[0];
            if (!string.IsNullOrEmpty(firstExtraction.ExtractedText))
            {
                return firstExtraction.ExtractedText;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cached text for PDF {PdfHash}", pdfHash[..10]);
            return null;
        }
    }

    public string RemoveCachedValuesFromText(string text, Dictionary<string, object> cachedValues)
    {
        try
        {
            if (cachedValues.Count == 0)
            {
                return text;
            }
            var lines = text.Split('\n', StringSplitOptions.None);
            var remainingLines = new List<string>();
            var removedLinesCount = 0;

            foreach (var line in lines)
            {
                bool lineContainsExtractedValue = false;

                if (string.IsNullOrWhiteSpace(line) || line.Trim().Length < 2)
                {
                    remainingLines.Add(line);
                    continue;
                }

                var normalizedLine = NormalizeText(line);

                foreach (var kvp in cachedValues)
                {
                    var value = kvp.Value?.ToString();
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    var normalizedValue = NormalizeText(value);

                    if (normalizedValue.Length < 2)
                        continue;

                    if (normalizedLine.Contains(normalizedValue, StringComparison.OrdinalIgnoreCase))
                    {
                        lineContainsExtractedValue = true;
                        removedLinesCount++;
                        break;
                    }
                }

                if (!lineContainsExtractedValue)
                {
                    remainingLines.Add(line);
                }
            }

            var reducedText = string.Join('\n', remainingLines);

            while (reducedText.Contains("\n\n\n"))
                reducedText = reducedText.Replace("\n\n\n", "\n\n");

            reducedText = reducedText.Trim();

            var reductionPercent = text.Length > 0
                ? ((text.Length - reducedText.Length) * 100.0 / text.Length)
                : 0;

            return reducedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cached values from text");
            return text; // Retornar texto original em caso de erro
        }
    }

    /// <summary>
    /// Normaliza texto removendo acentos, pontuação e convertendo para minúsculas
    /// </summary>
    private string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var result = sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant()
            .Trim();

        result = new string(result.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());

        while (result.Contains("  "))
            result = result.Replace("  ", " ");

        return result;
    }
}
