
using Enter_Extractor_Api.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Enter_Extractor_Api.Services;

public interface IExtractionService
{
    Task<ExtractionResult> ExtractAsync(
        string label,
        Dictionary<string, string> schema,
        string pdfBase64);
}

public class ExtractionService : IExtractionService
{
    private readonly ICacheService _cache;
    private readonly ITemplateStore _templateStore;
    private readonly IPdfTextExtractor _pdfExtractor;
    private readonly IHeuristicExtractor _heuristicExtractor;
    private readonly ILLMService _llmService;
    private readonly ILogger<ExtractionService> _logger;

    public ExtractionService(
        ICacheService cache,
        ITemplateStore templateStore,
        IPdfTextExtractor pdfExtractor,
        IHeuristicExtractor heuristicExtractor,
        ILLMService llmService,
        ILogger<ExtractionService> logger)
    {
        _cache = cache;
        _templateStore = templateStore;
        _pdfExtractor = pdfExtractor;
        _heuristicExtractor = heuristicExtractor;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ExtractionResult> ExtractAsync(
        string label,
        Dictionary<string, string> schema,
        string pdfBase64)
    {
        // Step 1: Check cache
        var cacheKey = GenerateCacheKey(pdfBase64, schema);
        var cachedResult = _cache.Get<Dictionary<string, object?>>(cacheKey);

        if (cachedResult != null)
        {
            _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
            return new ExtractionResult
            {
                Data = cachedResult,
                UsedCache = true,
                UsedHeuristics = false,
                TokensUsed = 0,
                Confidence = 1.0
            };
        }

        // Step 2: Extract PDF text
        var pdfBytes = Convert.FromBase64String(pdfBase64);
        var extractedText = await _pdfExtractor.ExtractTextAsync(pdfBytes);

        _logger.LogInformation("Extracted {CharCount} characters from PDF", extractedText.Length);

        // Step 3: Try heuristic extraction if template exists
        if (_templateStore.HasTemplate(label))
        {
            var heuristicResult = _heuristicExtractor.TryExtract(label, schema, extractedText);

            if (heuristicResult.Confidence >= 0.9)
            {
                _logger.LogInformation(
                    "Heuristic extraction successful with confidence: {Confidence}",
                    heuristicResult.Confidence
                );

                _cache.Set(cacheKey, heuristicResult.Data, TimeSpan.FromHours(24));

                return new ExtractionResult
                {
                    Data = heuristicResult.Data,
                    UsedCache = false,
                    UsedHeuristics = true,
                    TokensUsed = 0,
                    Confidence = heuristicResult.Confidence
                };
            }

            _logger.LogInformation(
                "Heuristic extraction confidence too low: {Confidence}, falling back to LLM",
                heuristicResult.Confidence
            );
        }

        // Step 4: LLM fallback
        var (llmResult, tokensUsed) = await _llmService.ExtractAsync(extractedText, schema);

        _logger.LogInformation("LLM extraction completed, tokens used: {TokensUsed}", tokensUsed);

        // Step 5: Learn from result for future optimizations
        _templateStore.LearnPattern(label, schema, extractedText, llmResult);

        // Step 6: Cache the result
        _cache.Set(cacheKey, llmResult, TimeSpan.FromHours(24));

        return new ExtractionResult
        {
            Data = llmResult,
            UsedCache = false,
            UsedHeuristics = false,
            TokensUsed = tokensUsed,
            Confidence = 1.0
        };
    }

    private string GenerateCacheKey(string pdfBase64, Dictionary<string, string> schema)
    {
        var combined = $"{pdfBase64}_{JsonSerializer.Serialize(schema)}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hashBytes);
    }
}
