using System.Diagnostics;
using System.Text.RegularExpressions;
using Enter_Extractor_Api.Models.V2;
using Enter_Extractor_Api.Services.V2.PythonClients;

namespace Enter_Extractor_Api.Services.V2;

/// <summary>
/// Orquestrador principal do fluxo V2 de extração
/// C# delega 100% para Python (NLI, SmartExtract, Fallback)
/// 
/// Fluxo:
/// 1. FASE 1: Regex/Enum (C# local)
/// 2. FASE 2: NLI Classification (Python /nli/classify)
/// 3. FASE 2.5: Smart Extract (Python /smart-extract) [condicional]
/// 4. FASE 3: Final Optimizer (C# local - merge e heurísticas)
/// 5. FALLBACK: GPT (Python /llm/fallback) [condicional]
/// </summary>
public class ExtractionOrchestratorService
{
    private readonly IPythonNliClient _nliClient;
    private readonly IPythonSmartExtractClient _smartExtractClient;
    private readonly IPythonFallbackClient _fallbackClient;
    private readonly IPythonMetricsClient _metricsClient;
    private readonly FinalOptimizerService _finalOptimizer;
    private readonly ILogger<ExtractionOrchestratorService> _logger;
    private readonly IConfiguration _configuration;

    public ExtractionOrchestratorService(
        IPythonNliClient nliClient,
        IPythonSmartExtractClient smartExtractClient,
        IPythonFallbackClient fallbackClient,
        IPythonMetricsClient metricsClient,
        FinalOptimizerService finalOptimizer,
        ILogger<ExtractionOrchestratorService> logger,
        IConfiguration configuration)
    {
        _nliClient = nliClient;
        _smartExtractClient = smartExtractClient;
        _fallbackClient = fallbackClient;
        _metricsClient = metricsClient;
        _finalOptimizer = finalOptimizer;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ExtractionV2Response> ExtractAsync(
        ExtractionV2Request request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var globalSw = Stopwatch.StartNew();
        var options = request.Options ?? new ExtractionOptions();

        _logger.LogInformation(
            "🚀 [TraceId: {TraceId}] Iniciando extração V2 - Label: {Label}, {FieldCount} campos",
            traceId, request.Label, request.Schema.Count
        );

        var response = new ExtractionV2Response
        {
            TraceId = traceId,
            Fields = new Dictionary<string, ExtractedFieldV2>()
        };

        try
        {
            // ========== FASE 1: REGEX/ENUM (C# LOCAL) ==========
            var (phase1Results, cleanedText1) = await ExecutePhase1RegexAsync(
                request.Text,
                request.Schema,
                traceId,
                response.Phases
            );

            // ========== OBTER THRESHOLD DINÂMICO ==========
            var thresholdResponse = await _metricsClient.GetThresholdAsync(
                request.Label,
                traceId,
                cancellationToken
            );
            var threshold = thresholdResponse?.Threshold ?? options.ConfidenceThreshold;

            _logger.LogInformation(
                "📊 [TraceId: {TraceId}] Threshold dinâmico: {Threshold} ({Reason})",
                traceId, threshold, thresholdResponse?.Reason ?? "default"
            );

            // ========== FASE 2: NLI CLASSIFICATION (PYTHON) ==========
            var cleanedText2 = await ExecutePhase2NliAsync(
                cleanedText1,
                request.Label,
                request.Schema,
                options.EnableNliCaching,
                traceId,
                response.Phases,
                cancellationToken
            );

            // ========== DECISÃO: EXECUTAR FASE 2.5? ==========
            var phase1Confidence = CalculateAverageConfidence(phase1Results);
            var shouldRunSmartExtract = options.EnableSmartExtract ||
                                       phase1Confidence < threshold ||
                                       phase1Results.Count < request.Schema.Count / 2;

            Dictionary<string, ExtractedFieldV2> phase25Results = new();

            if (shouldRunSmartExtract)
            {
                _logger.LogInformation(
                    "🧠 [TraceId: {TraceId}] FASE 2.5 será executada (Phase1Conf: {Conf:F3} < {Threshold})",
                    traceId, phase1Confidence, threshold
                );

                // Identificar campos pendentes
                var pendingFields = request.Schema
                    .Where(kv => !phase1Results.ContainsKey(kv.Key) ||
                                 string.IsNullOrWhiteSpace(phase1Results[kv.Key].Value))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                if (pendingFields.Any())
                {
                    phase25Results = await ExecutePhase25SmartExtractAsync(
                        cleanedText2,
                        request.Label,
                        pendingFields,
                        threshold,
                        options.EnableFallbackLLM, // GPT fallback dentro do smart-extract
                        traceId,
                        response.Phases,
                        cancellationToken
                    );
                }
            }
            else
            {
                _logger.LogInformation(
                    "⏭️ [TraceId: {TraceId}] FASE 2.5 PULADA (Phase1Conf: {Conf:F3} >= {Threshold})",
                    traceId, phase1Confidence, threshold
                );
            }

            // ========== FASE 3: FINAL OPTIMIZER (C# LOCAL) ==========
            response.Fields = ExecutePhase3Optimizer(
                phase1Results,
                phase25Results,
                request.Schema,
                traceId,
                response.Phases
            );

            // ========== DECISÃO: EXECUTAR FALLBACK GPT? ==========
            var finalConfidence = _finalOptimizer.CalculateWeightedConfidence(response.Fields);
            var missingEssentialFields = response.Fields.Values.Count(f => string.IsNullOrWhiteSpace(f.Value));

            if (options.EnableFallbackLLM &&
                (finalConfidence < threshold || missingEssentialFields > 0))
            {
                _logger.LogInformation(
                    "🤖 [TraceId: {TraceId}] FALLBACK GPT será executado (FinalConf: {Conf:F3}, Missing: {Missing})",
                    traceId, finalConfidence, missingEssentialFields
                );

                response.Fields = await ExecuteFallbackAsync(
                    response.Fields,
                    request.Schema,
                    request.Text,
                    request.Label,
                    threshold,
                    traceId,
                    response.Phases,
                    cancellationToken
                );

                response.FallbackUsed = true;
            }
            else
            {
                _logger.LogInformation(
                    "✅ [TraceId: {TraceId}] FALLBACK NÃO NECESSÁRIO (FinalConf: {Conf:F3})",
                    traceId, finalConfidence
                );
                response.FallbackUsed = false;
            }

            // ========== FINALIZAÇÃO ==========
            response.ConfidenceAvg = _finalOptimizer.CalculateWeightedConfidence(response.Fields);
            response.ProcessingMs = globalSw.ElapsedMilliseconds;

            _logger.LogInformation(
                "✅ [TraceId: {TraceId}] Extração V2 concluída em {Duration}ms - {FieldCount}/{TotalFields} campos extraídos (conf: {Conf:F3})",
                traceId, response.ProcessingMs,
                response.Fields.Values.Count(f => !string.IsNullOrWhiteSpace(f.Value)),
                response.Fields.Count,
                response.ConfidenceAvg
            );

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "❌ [TraceId: {TraceId}] Erro na extração: {Message}\n{StackTrace}",
                traceId, ex.Message, ex.StackTrace
            );
            throw;
        }
    }

    // ========== FASE 1: REGEX/ENUM ==========

    private Task<(Dictionary<string, ExtractedFieldV2>, string)> ExecutePhase1RegexAsync(
        string text,
        Dictionary<string, string> schema,
        string traceId,
        PhasesMetrics metrics)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("🔍 [TraceId: {TraceId}] ▶️ FASE 1: Regex/Enum Extraction", traceId);

        var results = new Dictionary<string, ExtractedFieldV2>();
        var cleanedText = text;

        // Padrões estruturados
        var patterns = new Dictionary<string, (Regex Pattern, string Name)>
        {
            ["cpf"] = (new Regex(@"\d{3}\.?\d{3}\.?\d{3}-?\d{2}"), "cpf"),
            ["cnpj"] = (new Regex(@"\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}"), "cnpj"),
            ["cep"] = (new Regex(@"\d{5}-?\d{3}"), "cep"),
            ["telefone"] = (new Regex(@"\(?\d{2}\)?\s?\d{4,5}-?\d{4}"), "telefone"),
            ["email"] = (new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}"), "email"),
            ["data"] = (new Regex(@"\d{2}/\d{2}/\d{4}"), "data")
        };

        foreach (var (fieldName, fieldDescription) in schema)
        {
            foreach (var (key, (pattern, patternName)) in patterns)
            {
                if (fieldName.ToLowerInvariant().Contains(key))
                {
                    var match = pattern.Match(cleanedText);
                    if (match.Success)
                    {
                        results[fieldName] = new ExtractedFieldV2
                        {
                            Value = match.Value,
                            Confidence = 0.95f,
                            Method = $"regex_{patternName}",
                            SourcePhase = "phase1",
                            LineIndex = null
                        };

                        // Remover match do texto
                        cleanedText = cleanedText.Replace(match.Value, "");

                        _logger.LogInformation(
                            "  ✅ [TraceId: {TraceId}] Phase1: {Field} = '{Value}' (regex)",
                            traceId, fieldName, match.Value
                        );
                        break;
                    }
                }
            }
        }

        metrics.Phase1Ms = sw.ElapsedMilliseconds;
        metrics.Phase1Fields = results.Count;

        _logger.LogInformation(
            "✅ [TraceId: {TraceId}] FASE 1 concluída: {Count} campos em {Duration}ms",
            traceId, results.Count, metrics.Phase1Ms
        );

        return Task.FromResult((results, cleanedText));
    }

    // ========== FASE 2: NLI CLASSIFICATION ==========

    private async Task<string> ExecutePhase2NliAsync(
        string text,
        string label,
        Dictionary<string, string> schema,
        bool enableCaching,
        string traceId,
        PhasesMetrics metrics,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("🔍 [TraceId: {TraceId}] ▶️ FASE 2: NLI Classification", traceId);

        // Split em blocos (linhas)
        var blocks = text
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim())
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        if (blocks.Count == 0)
        {
            _logger.LogWarning("[TraceId: {TraceId}] FASE 2: Nenhum bloco para classificar", traceId);
            return text;
        }

        var nliRequest = new NliClassifyRequest
        {
            Label = label,
            Schema = schema,
            TextBlocks = blocks
        };

        var nliResponse = await _nliClient.ClassifyAsync(nliRequest, traceId, cancellationToken);

        // Remover labels detectadas
        var cleanedBlocks = blocks.ToList();
        foreach (var classifiedBlock in nliResponse.ClassifiedBlocks)
        {
            if (classifiedBlock.Label.Equals("label", StringComparison.OrdinalIgnoreCase) &&
                classifiedBlock.Confidence >= 0.7f)
            {
                cleanedBlocks.RemoveAll(b => b.Equals(classifiedBlock.Text, StringComparison.OrdinalIgnoreCase));

                _logger.LogInformation(
                    "  🗑️ [TraceId: {TraceId}] Phase2: Removeu label: '{Label}' (confidence: {Confidence:F3})",
                    traceId, classifiedBlock.Text, classifiedBlock.Confidence
                );
            }
        }

        var cleanedText = string.Join("\n", cleanedBlocks);

        metrics.Phase2Ms = sw.ElapsedMilliseconds;
        metrics.Phase2LabelsRemoved = blocks.Count - cleanedBlocks.Count;
        metrics.Phase2CacheHit = nliResponse.CacheHits > 0;

        _logger.LogInformation(
            "✅ [TraceId: {TraceId}] FASE 2 concluída: {Removed} labels removidas em {Duration}ms (cache_hits: {CacheHits}/{TotalBlocks})",
            traceId, metrics.Phase2LabelsRemoved, metrics.Phase2Ms, nliResponse.CacheHits, nliResponse.TotalBlocks
        );

        return cleanedText;
    }

    // ========== FASE 2.5: SMART EXTRACT ==========

    private async Task<Dictionary<string, ExtractedFieldV2>> ExecutePhase25SmartExtractAsync(
        string text,
        string label,
        Dictionary<string, string> schema,
        float threshold,
        bool enableGptFallback,
        string traceId,
        PhasesMetrics metrics,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "🔍 [TraceId: {TraceId}] ▶️ FASE 2.5: Smart Extract - {FieldCount} campos pendentes",
            traceId, schema.Count
        );

        var smartRequest = new SmartExtractRequest
        {
            Label = label,
            Schema = schema,
            Text = text,
            ConfidenceThreshold = threshold,
            EnableGptFallback = enableGptFallback,
            Options = new SmartExtractOptions
            {
                UseMemory = true,
                MaxLines = 200
            }
        };

        var smartResponse = await _smartExtractClient.ExtractAsync(smartRequest, traceId, cancellationToken);

        var results = new Dictionary<string, ExtractedFieldV2>();

        foreach (var (fieldName, field) in smartResponse.Fields)
        {
            if (!string.IsNullOrWhiteSpace(field.Value))
            {
                results[fieldName] = new ExtractedFieldV2
                {
                    Value = field.Value,
                    Confidence = field.Confidence,
                    Method = field.Method,
                    SourcePhase = "phase2.5",
                    LineIndex = field.LineIndex
                };

                _logger.LogInformation(
                    "  ✅ [TraceId: {TraceId}] Phase2.5: {Field} = '{Value}' (conf: {Conf:F3}, method: {Method})",
                    traceId, fieldName, field.Value, field.Confidence, field.Method
                );
            }
        }

        metrics.Phase25Ms = sw.ElapsedMilliseconds;
        metrics.Phase25Fields = results.Count;
        metrics.Phase25CacheHit = smartResponse.CacheHit;

        _logger.LogInformation(
            "✅ [TraceId: {TraceId}] FASE 2.5 concluída: {Count} campos em {Duration}ms (cache: {CacheHit}, gpt: {GptUsed})",
            traceId, results.Count, metrics.Phase25Ms, smartResponse.CacheHit, smartResponse.GptFallbackUsed
        );

        return results;
    }

    // ========== FASE 3: FINAL OPTIMIZER ==========

    private Dictionary<string, ExtractedFieldV2> ExecutePhase3Optimizer(
        Dictionary<string, ExtractedFieldV2> phase1Results,
        Dictionary<string, ExtractedFieldV2> phase25Results,
        Dictionary<string, string> schema,
        string traceId,
        PhasesMetrics metrics)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("🔍 [TraceId: {TraceId}] ▶️ FASE 3: Final Optimizer", traceId);

        var merged = _finalOptimizer.MergeResults(phase1Results, phase25Results, schema, traceId);

        metrics.Phase3Ms = sw.ElapsedMilliseconds;

        _logger.LogInformation(
            "✅ [TraceId: {TraceId}] FASE 3 concluída: {Count} campos finais em {Duration}ms",
            traceId, merged.Count, metrics.Phase3Ms
        );

        return merged;
    }

    // ========== FALLBACK GPT ==========

    private async Task<Dictionary<string, ExtractedFieldV2>> ExecuteFallbackAsync(
        Dictionary<string, ExtractedFieldV2> currentResults,
        Dictionary<string, string> schema,
        string originalText,
        string label,
        float threshold,
        string traceId,
        PhasesMetrics metrics,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("🔍 [TraceId: {TraceId}] ▶️ FALLBACK: GPT Correction", traceId);

        // Identificar campos que precisam de correção
        var fieldsNeedingFallback = currentResults
            .Where(kv => string.IsNullOrWhiteSpace(kv.Value.Value) || kv.Value.Confidence < threshold)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        if (!fieldsNeedingFallback.Any())
        {
            _logger.LogInformation("[TraceId: {TraceId}] FALLBACK: Nenhum campo precisa de correção", traceId);
            return currentResults;
        }

        var fallbackSchema = schema
            .Where(kv => fieldsNeedingFallback.ContainsKey(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var partialResults = fieldsNeedingFallback.ToDictionary(
            kv => kv.Key,
            kv => new PartialResult
            {
                Value = kv.Value.Value,
                Confidence = kv.Value.Confidence
            }
        );

        var fallbackRequest = new LlmFallbackRequest
        {
            Label = label,
            Schema = fallbackSchema,
            PartialResults = partialResults,
            Text = originalText,
            Options = new LlmFallbackOptions
            {
                Temperature = 0.2f,
                Model = "gpt-4o-mini"
            }
        };

        var fallbackResponse = await _fallbackClient.FallbackAsync(fallbackRequest, traceId, cancellationToken);

        var updated = _finalOptimizer.MergeWithFallback(currentResults, fallbackResponse.Fields, traceId);

        metrics.FallbackMs = sw.ElapsedMilliseconds;
        metrics.FallbackFields = fallbackResponse.Fields.Count;

        _logger.LogInformation(
            "✅ [TraceId: {TraceId}] FALLBACK concluído: {Count} campos corrigidos em {Duration}ms",
            traceId, fallbackResponse.Fields.Count, metrics.FallbackMs
        );

        return updated;
    }

    // ========== HELPERS ==========

    private float CalculateAverageConfidence(Dictionary<string, ExtractedFieldV2> results)
    {
        if (results.Count == 0)
            return 0.0f;

        var validFields = results.Values.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();

        if (validFields.Count == 0)
            return 0.0f;

        return validFields.Average(f => f.Confidence);
    }
}
