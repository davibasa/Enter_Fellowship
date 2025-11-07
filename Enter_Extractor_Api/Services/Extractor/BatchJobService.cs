using Enter_Extractor_Api.Models.Extractor;
using Enter_Extractor_Api.Services.Cache;
using Enter_Extractor_Api.Services.Redis;
using Enter_Extractor_Api.Models.Redis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Enter_Extractor_Api.Services.Extractor;

public interface IBatchJobService
{
    string CreateJob(BatchJobRequest request);
    BatchJobStatus? GetJobStatus(string jobId);
    Task ProcessJobAsync(string jobId);
    IAsyncEnumerable<SSEEvent> StreamJobProgressAsync(string jobId, CancellationToken cancellationToken);
}

public class BatchJobService : IBatchJobService
{
    private readonly ConcurrentDictionary<string, BatchJobStatus> _jobs = new();
    private readonly ConcurrentDictionary<string, BatchJobRequest> _jobRequests = new();
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<BatchJobService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _pythonApiUrl;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BatchJobService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<BatchJobService> logger,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _httpClient = httpClient;
        _pythonApiUrl = configuration["PythonApi:BaseUrl"] ?? "http://pdf-extractor:5000";
    }

    public string CreateJob(BatchJobRequest request)
    {
        var jobId = Guid.NewGuid().ToString();
        var status = new BatchJobStatus
        {
            JobId = jobId,
            Status = "queued",
            TotalItems = request.PdfItems.Count,
            ProcessedItems = 0,
            SuccessCount = 0,
            ErrorCount = 0,
            CreatedAt = DateTime.UtcNow,
            Results = request.PdfItems.Select(item => new BatchItemResult
            {
                FileId = item.FileId,
                Status = "pending",
                ValidationData = item.ValidationData
            }).ToList()
        };

        _jobs[jobId] = status;
        _jobRequests[jobId] = request;

        return jobId;
    }

    public BatchJobStatus? GetJobStatus(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var status) ? status : null;
    }

    public async Task ProcessJobAsync(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var status) || !_jobRequests.TryGetValue(jobId, out var request))
        {
            return;
        }

        status.Status = "processing";
        status.StartedAt = DateTime.UtcNow;

        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < request.PdfItems.Count; i++)
        {
            var pdfItem = request.PdfItems[i];
            var result = status.Results.FirstOrDefault(r => r.FileId == pdfItem.FileId);
            if (result == null) continue;

            result.Status = "processing";
            var itemStopwatch = Stopwatch.StartNew();

            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var extractorService = scope.ServiceProvider.GetRequiredService<IExtractorService>();
                    var cacheService = scope.ServiceProvider.GetRequiredService<IExtractionCacheService>();
                    var labelDetectionService = scope.ServiceProvider.GetRequiredService<Services.LabelDetection.ILabelDetectionService>();

                    var pdfBytes = Convert.FromBase64String(pdfItem.PdfBase64);
                    var pdfHash = cacheService.CalculatePdfHash(pdfBytes);

                    var schemaHash = !string.IsNullOrEmpty(pdfItem.SchemaHash)
                        ? pdfItem.SchemaHash
                        : cacheService.CalculateSchemaHash(pdfItem.ExtractionSchema!);

                    // 🔥 FIRE-AND-FORGET: Detectar labels em background (não bloqueia)
                    if (pdfItem.ExtractionSchema != null)
                    {
                        _ = DetectAndCacheLabelsAsync(
                            request.Label,
                            pdfHash,
                            pdfItem.ExtractionSchema,
                            schemaHash,
                            pdfBytes,
                            labelDetectionService,
                            scope.ServiceProvider);
                    }

                    var cachedResult = await cacheService.GetCachedExtractionAsync(
                        request.Label,
                        pdfHash,
                        pdfItem.ExtractionSchema!);

                    if (cachedResult != null)
                    {
                        itemStopwatch.Stop();

                        var filteredResult = FilterSchemaFields(cachedResult, pdfItem.ExtractionSchema.Keys);

                        result.Status = "success";
                        result.Data = filteredResult;
                        result.ProcessingTimeMs = itemStopwatch.ElapsedMilliseconds;
                        result.ProcessedAt = DateTime.UtcNow;
                        result.UsedCache = true;
                        result.CacheType = "exact";
                        result.SchemaHash = schemaHash;

                        status.SuccessCount++;
                        status.ProcessedItems++;

                        _ = Task.Run(async () =>
                        {
                            try
                            {

                                using var historyScope = _serviceScopeFactory.CreateScope();
                                var persistence = historyScope.ServiceProvider.GetRequiredService<IRedisPersistenceService>();


                                var schemaVersionId = await persistence.SaveOrUpdateSchemaVersionAsync(
                                    request.Label,
                                    pdfItem.ExtractionSchema,
                                    request.TemplateId,
                                    schemaHash);

                                var fieldsExtracted = filteredResult.Schema.Count(kvp =>
                                    kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.ToString()));

                                var historyDto = new ExtractionHistoryDto
                                {
                                    UserId = request.UserId ?? "default-user",
                                    PdfHash = pdfHash,
                                    PdfFilename = pdfItem.PdfFilename ?? $"{request.Label}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
                                    PdfSizeBytes = pdfBytes.Length,
                                    Label = request.Label,
                                    TemplateId = request.TemplateId,
                                    SchemaVersionId = schemaVersionId,
                                    SchemaHash = schemaHash,
                                    Schema = pdfItem.ExtractionSchema,
                                    ExtractedAt = DateTime.UtcNow,
                                    ProcessingTimeMs = (int)itemStopwatch.ElapsedMilliseconds,
                                    TokensUsed = 0,
                                    CostUsd = 0.0m,
                                    FieldsTotal = filteredResult.Schema.Count,
                                    FieldsExtracted = fieldsExtracted,
                                    SuccessRate = filteredResult.Schema.Count > 0
                                        ? (float)fieldsExtracted / filteredResult.Schema.Count
                                        : 0f,
                                    Strategies = new Dictionary<string, string>
                                    {
                                        ["cache"] = filteredResult.Schema.Count.ToString(),
                                        ["extracted"] = "0",
                                        ["cacheType"] = "exact"
                                    },
                                    Result = filteredResult.Schema.ToDictionary(
                                        kvp => kvp.Key,
                                        kvp => (object)(kvp.Value ?? "")
                                    )!,
                                    EditedManually = false,
                                    Status = "completed"
                                };

                                var extractionId = await persistence.SaveExtractionHistoryAsync(historyDto);

                                await persistence.UpdateSchemaVersionStatsAsync(
                                    schemaVersionId,
                                    historyDto.SuccessRate * 100,
                                    historyDto.ProcessingTimeMs,
                                    extractionId);

                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to save extraction history for cached PDF {FileId} (non-critical)",
                                    pdfItem.FileId);
                            }
                        });

                        continue;
                    }

                    var previouslyExtractedValues = await cacheService.GetPreviouslyExtractedValuesAsync(pdfHash);

                    var fieldsNeeded = pdfItem.ExtractionSchema.Keys
                        .Where(field => !previouslyExtractedValues.ContainsKey(field) ||
                                        previouslyExtractedValues[field] == null ||
                                        string.IsNullOrEmpty(previouslyExtractedValues[field].ToString()))
                        .ToList();

                    if (fieldsNeeded.Count == 0)
                    {
                        itemStopwatch.Stop();

                        var partialCacheResult = new ExtractorResponse
                        {
                            Schema = pdfItem.ExtractionSchema.Keys.ToDictionary(
                                k => k,
                                k => previouslyExtractedValues.ContainsKey(k) ? previouslyExtractedValues[k] : null
                            )
                        };

                        result.Status = "success";
                        result.Data = partialCacheResult;
                        result.ProcessingTimeMs = itemStopwatch.ElapsedMilliseconds;
                        result.ProcessedAt = DateTime.UtcNow;
                        result.UsedCache = true;
                        result.CacheType = "partial_complete";
                        result.SchemaHash = schemaHash;

                        status.SuccessCount++;

                        _ = Task.Run(async () =>
                        {
                            var cachedText = await cacheService.GetCachedExtractedTextAsync(pdfHash);
                            await cacheService.SaveExtractionAsync(
                                label: request.Label,
                                pdfHash: pdfHash,
                                schema: pdfItem.ExtractionSchema,
                                response: partialCacheResult,
                                processingTimeMs: itemStopwatch.ElapsedMilliseconds,
                                pdfSizeBytes: pdfBytes.Length,
                                extractedText: cachedText
                            );
                        });

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using var historyScope = _serviceScopeFactory.CreateScope();
                                var persistence = historyScope.ServiceProvider.GetRequiredService<IRedisPersistenceService>();

                                var schemaVersionId = await persistence.SaveOrUpdateSchemaVersionAsync(
                                    request.Label,
                                    pdfItem.ExtractionSchema,
                                    request.TemplateId,
                                    schemaHash);

                                var fieldsExtracted = partialCacheResult.Schema.Count(kvp =>
                                    kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.ToString()));

                                var historyDto = new ExtractionHistoryDto
                                {
                                    UserId = request.UserId ?? "default-user",
                                    PdfHash = pdfHash,
                                    PdfFilename = pdfItem.PdfFilename ?? $"{request.Label}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
                                    PdfSizeBytes = pdfBytes.Length,
                                    Label = request.Label,
                                    TemplateId = request.TemplateId,
                                    SchemaVersionId = schemaVersionId,
                                    SchemaHash = schemaHash,
                                    Schema = pdfItem.ExtractionSchema,
                                    ExtractedAt = DateTime.UtcNow,
                                    ProcessingTimeMs = (int)itemStopwatch.ElapsedMilliseconds,
                                    TokensUsed = 0,
                                    CostUsd = 0.0m,
                                    FieldsTotal = partialCacheResult.Schema.Count,
                                    FieldsExtracted = fieldsExtracted,
                                    SuccessRate = partialCacheResult.Schema.Count > 0
                                        ? (float)fieldsExtracted / partialCacheResult.Schema.Count
                                        : 0f,
                                    Strategies = new Dictionary<string, string>
                                    {
                                        ["cache"] = partialCacheResult.Schema.Count.ToString(),
                                        ["extracted"] = "0",
                                        ["cacheType"] = "partial_complete"
                                    },
                                    Result = partialCacheResult.Schema.ToDictionary(
                                        kvp => kvp.Key,
                                        kvp => (object)(kvp.Value ?? "")
                                    )!,
                                    EditedManually = false,
                                    Status = "completed"
                                };

                                var extractionId = await persistence.SaveExtractionHistoryAsync(historyDto);

                                await persistence.UpdateSchemaVersionStatsAsync(
                                    schemaVersionId,
                                    historyDto.SuccessRate * 100,
                                    historyDto.ProcessingTimeMs,
                                    extractionId);

                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to save extraction history for partially cached PDF {FileId} (non-critical)",
                                    pdfItem.FileId);
                            }
                        });

                        status.ProcessedItems++;
                        continue;
                    }

                    var extractedText = await cacheService.GetCachedExtractedTextAsync(pdfHash);

                    if (string.IsNullOrEmpty(extractedText))
                    {

                        var requestPayload = new
                        {
                            pdf_base64 = pdfItem.PdfBase64
                        };

                        var jsonContent = JsonSerializer.Serialize(requestPayload, _jsonOptions);
                        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_pythonApiUrl}/extract-text")
                        {
                            Content = httpContent
                        };

                        var httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead)
                            .ConfigureAwait(false);

                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            var errorContent = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            _logger.LogError("Failed to extract text from PDF. API returned {StatusCode}: {Error}",
                                httpResponse.StatusCode, errorContent);
                            throw new InvalidOperationException($"Failed to extract text from PDF. API returned {httpResponse.StatusCode}");
                        }

                        var responseContent = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var extractionResult = JsonSerializer.Deserialize<PdfExtractionResponse>(responseContent, _jsonOptions);

                        if (extractionResult == null || !extractionResult.Success)
                        {
                            throw new InvalidOperationException("Failed to parse extraction result from Python API");
                        }

                        extractedText = extractionResult.Text ?? string.Empty;
                    }
                    else
                    {
                        _logger.LogInformation("Using cached extracted text ({CharCount} chars)", extractedText.Length);
                    }

                    var reducedText = cacheService.RemoveCachedValuesFromText(extractedText, previouslyExtractedValues);

                    var reducedSchema = fieldsNeeded.ToDictionary(
                        field => field,
                        field => pdfItem.ExtractionSchema[field]
                    );

                    var partialResult = await extractorService.ExtractAsync(
                        request.Label,
                        pdfItem.ExtractionSchema,
                        reducedText)
                        .ConfigureAwait(false);

                    var finalResult = cacheService.MergeResults(
                        pdfItem.ExtractionSchema,
                        previouslyExtractedValues,
                        partialResult
                    );

                    itemStopwatch.Stop();
                    var processingTimeMs = itemStopwatch.ElapsedMilliseconds;

                    var fieldsFromCache = pdfItem.ExtractionSchema.Count - fieldsNeeded.Count;

                    result.Status = "success";
                    result.Data = finalResult;
                    result.ProcessingTimeMs = processingTimeMs;
                    result.ProcessedAt = DateTime.UtcNow;
                    result.UsedCache = fieldsFromCache > 0;
                    result.CacheType = fieldsFromCache > 0 ? "partial_hybrid" : "none";
                    result.SchemaHash = schemaHash;

                    status.SuccessCount++;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await cacheService.SaveExtractionAsync(
                                label: request.Label,
                                pdfHash: pdfHash,
                                schema: pdfItem.ExtractionSchema,
                                response: finalResult,
                                processingTimeMs: processingTimeMs,
                                tokensUsed: 0,
                                costUsd: 0.0,
                                strategiesUsed: "",
                                pdfSizeBytes: pdfBytes.Length,
                                extractedText: extractedText
                            );

                            _logger.LogDebug("Extraction cached successfully for {Label} (with text: {CharCount} chars)",
                                request.Label, extractedText.Length);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to cache extraction (non-critical)");
                        }
                    });

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var historyScope = _serviceScopeFactory.CreateScope();
                            var persistence = historyScope.ServiceProvider.GetRequiredService<IRedisPersistenceService>();

                            var schemaVersionId = await persistence.SaveOrUpdateSchemaVersionAsync(
                                request.Label,
                                pdfItem.ExtractionSchema,
                                request.TemplateId,
                                schemaHash);

                            var fieldsExtracted = finalResult.Schema.Count(kvp =>
                                kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.ToString()));

                            var historyDto = new ExtractionHistoryDto
                            {
                                UserId = request.UserId ?? "default-user",
                                PdfHash = pdfHash,
                                PdfFilename = pdfItem.PdfFilename ?? $"{request.Label}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
                                PdfSizeBytes = pdfBytes.Length,
                                Label = request.Label,
                                TemplateId = request.TemplateId,
                                SchemaVersionId = schemaVersionId,
                                SchemaHash = schemaHash,
                                Schema = pdfItem.ExtractionSchema,
                                ExtractedAt = DateTime.UtcNow,
                                ProcessingTimeMs = (int)processingTimeMs,
                                TokensUsed = 0,
                                CostUsd = 0.0m,
                                FieldsTotal = finalResult.Schema.Count,
                                FieldsExtracted = fieldsExtracted,
                                SuccessRate = finalResult.Schema.Count > 0
                                    ? (float)fieldsExtracted / finalResult.Schema.Count
                                    : 0f,
                                Strategies = new Dictionary<string, string>
                                {
                                    ["cache"] = fieldsFromCache.ToString(),
                                    ["extracted"] = fieldsNeeded.Count.ToString(),
                                    ["cacheType"] = result.CacheType ?? "none"
                                },
                                Result = finalResult.Schema.ToDictionary(
                                    kvp => kvp.Key,
                                    kvp => (object)(kvp.Value ?? "")
                                )!,
                                EditedManually = false,
                                Status = "completed"
                            };

                            var extractionId = await persistence.SaveExtractionHistoryAsync(historyDto);

                            await persistence.UpdateSchemaVersionStatsAsync(
                                schemaVersionId,
                                historyDto.SuccessRate * 100,
                                historyDto.ProcessingTimeMs,
                                extractionId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to save extraction history for PDF {FileId} (non-critical)",
                                pdfItem.FileId);
                        }
                    });
                }
            }
            catch (HttpRequestException ex)
            {
                itemStopwatch.Stop();
                _logger.LogError(ex, "HTTP error calling Python API at {Url} for PDF {FileId}", _pythonApiUrl, pdfItem.FileId);

                result.Status = "error";
                result.ErrorMessage = $"Failed to connect to PDF extraction service: {ex.Message}";
                result.ProcessingTimeMs = itemStopwatch.ElapsedMilliseconds;
                result.ProcessedAt = DateTime.UtcNow;

                status.ErrorCount++;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var errorScope = _serviceScopeFactory.CreateScope();
                        var persistence = errorScope.ServiceProvider.GetRequiredService<IRedisPersistenceService>();
                        var cacheService = errorScope.ServiceProvider.GetRequiredService<IExtractionCacheService>();

                        var pdfBytesError = Convert.FromBase64String(pdfItem.PdfBase64);
                        var pdfHashError = cacheService.CalculatePdfHash(pdfBytesError);

                        var errorHistoryDto = new ExtractionHistoryDto
                        {
                            UserId = request.UserId ?? "default-user",
                            PdfHash = pdfHashError,
                            PdfFilename = pdfItem.PdfFilename ?? $"{request.Label}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
                            PdfSizeBytes = pdfBytesError.Length,
                            Label = request.Label,
                            ExtractedAt = DateTime.UtcNow,
                            ProcessingTimeMs = (int)itemStopwatch.ElapsedMilliseconds,
                            FieldsTotal = pdfItem.ExtractionSchema.Count,
                            FieldsExtracted = 0,
                            SuccessRate = 0f,
                            Status = "failed",
                            Result = new Dictionary<string, object>
                            {
                                ["error"] = result.ErrorMessage ?? "Unknown error"
                            }
                        };

                        await persistence.SaveExtractionHistoryAsync(errorHistoryDto);
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogWarning(saveEx, "Failed to save error history for PDF {FileId}", pdfItem.FileId);
                    }
                });
            }
            catch (Exception ex)
            {
                itemStopwatch.Stop();

                result.Status = "error";
                result.ErrorMessage = ex.Message;
                result.ProcessingTimeMs = itemStopwatch.ElapsedMilliseconds;
                result.ProcessedAt = DateTime.UtcNow;

                status.ErrorCount++;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var errorScope = _serviceScopeFactory.CreateScope();
                        var persistence = errorScope.ServiceProvider.GetRequiredService<IRedisPersistenceService>();
                        var cacheService = errorScope.ServiceProvider.GetRequiredService<IExtractionCacheService>();

                        var pdfBytesError = Convert.FromBase64String(pdfItem.PdfBase64);
                        var pdfHashError = cacheService.CalculatePdfHash(pdfBytesError);
                        var schemaHashError = !string.IsNullOrEmpty(pdfItem.SchemaHash)
                            ? pdfItem.SchemaHash
                            : cacheService.CalculateSchemaHash(pdfItem.ExtractionSchema);

                        var errorHistoryDto = new ExtractionHistoryDto
                        {
                            UserId = request.UserId ?? "default-user",
                            PdfHash = pdfHashError,
                            PdfFilename = pdfItem.PdfFilename ?? $"{request.Label}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
                            PdfSizeBytes = pdfBytesError.Length,
                            Label = request.Label,
                            SchemaHash = schemaHashError,
                            Schema = pdfItem.ExtractionSchema,
                            ExtractedAt = DateTime.UtcNow,
                            ProcessingTimeMs = (int)itemStopwatch.ElapsedMilliseconds,
                            FieldsTotal = pdfItem.ExtractionSchema.Count,
                            FieldsExtracted = 0,
                            SuccessRate = 0f,
                            Status = "failed",
                            Result = new Dictionary<string, object>
                            {
                                ["error"] = result.ErrorMessage ?? "Unknown error"
                            }
                        };

                        await persistence.SaveExtractionHistoryAsync(errorHistoryDto);
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogWarning(saveEx, "Failed to save error history for PDF {FileId}", pdfItem.FileId);
                    }
                });
            }

            status.ProcessedItems++;
        }

        stopwatch.Stop();
        status.Status = "completed";
        status.CompletedAt = DateTime.UtcNow;

    }

    public async IAsyncEnumerable<SSEEvent> StreamJobProgressAsync(
        string jobId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(jobId, out var status))
        {
            yield return new SSEEvent
            {
                Type = "error",
                Data = new { message = "Job not found" }
            };
            yield break;
        }

        var lastProcessedCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (status.ProcessedItems > lastProcessedCount || status.Status == "completed")
            {
                yield return new SSEEvent
                {
                    Type = "progress",
                    Data = new
                    {
                        jobId,
                        status = status.Status,
                        processed = status.ProcessedItems,
                        total = status.TotalItems,
                        successCount = status.SuccessCount,
                        errorCount = status.ErrorCount
                    }
                };

                for (int i = lastProcessedCount; i < status.ProcessedItems; i++)
                {
                    var result = status.Results[i];
                    yield return new SSEEvent
                    {
                        Type = "result",
                        Data = new
                        {
                            fileId = result.FileId,
                            status = result.Status,
                            data = result.Data,
                            error = result.ErrorMessage,
                            processingTimeMs = result.ProcessingTimeMs,
                            usedCache = result.UsedCache,
                            cacheType = result.CacheType,
                            validationData = result.ValidationData
                        }
                    };
                }

                lastProcessedCount = status.ProcessedItems;
            }

            if (status.Status == "completed")
            {
                yield return new SSEEvent
                {
                    Type = "complete",
                    Data = new
                    {
                        jobId,
                        totalItems = status.TotalItems,
                        successCount = status.SuccessCount,
                        errorCount = status.ErrorCount,
                        completedAt = status.CompletedAt
                    }
                };

                yield break;
            }

            await Task.Delay(500, cancellationToken);
        }

    }

    /// <summary>
    /// Filtra o resultado da extração para conter apenas os campos solicitados
    /// </summary>
    private ExtractorResponse FilterSchemaFields(ExtractorResponse response, IEnumerable<string> requestedFields)
    {
        var filteredSchema = new Dictionary<string, object?>();

        foreach (var field in requestedFields)
        {
            filteredSchema[field] = response.Schema.ContainsKey(field)
                ? response.Schema[field]
                : null;
        }

        return new ExtractorResponse
        {
            Schema = filteredSchema
        };
    }

    /// <summary>
    /// 🔥 FIRE-AND-FORGET: Detecta labels em background e salva no cache Redis
    /// Não bloqueia o fluxo principal de extração
    /// </summary>
    private async Task DetectAndCacheLabelsAsync(
        string label,
        string pdfHash,
        Dictionary<string, string?> schema,
        string schemaHash,
        byte[] pdfBytes,
        Services.LabelDetection.ILabelDetectionService labelDetectionService,
        IServiceProvider serviceProvider)
    {
        try
        {
            // Executar em background (sem await no caller)
            await Task.Run(async () =>
            {
                try
                {
                    _logger.LogDebug(
                        "🔍 [Background] Iniciando detecção de labels | Label: {Label} | PdfHash: {PdfHash} | SchemaHash: {SchemaHash}",
                        label,
                        pdfHash[..8],
                        schemaHash[..8]);

                    // 1. Extrair texto do PDF
                    var pdfExtractor = serviceProvider.GetRequiredService<IPdfTextExtractor>();
                    var extractedText = await pdfExtractor.ExtractTextAsync(pdfBytes);

                    if (string.IsNullOrWhiteSpace(extractedText))
                    {
                        _logger.LogWarning(
                            "⚠️ [Background] Texto vazio extraído do PDF | PdfHash: {PdfHash}",
                            pdfHash[..8]);
                        return;
                    }

                    // 2. Converter schema (remover nullables)
                    var cleanSchema = schema
                        .Where(kvp => kvp.Value != null)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

                    // 3. Detectar labels (usa cache se disponível)
                    var detectedLabels = await labelDetectionService.DetectLabelsAsync(
                        label: label,
                        pdfHash: pdfHash,
                        schema: cleanSchema,
                        schemaHash: schemaHash,
                        text: extractedText,
                        topK: 3,
                        minTokenLength: 3,
                        similarityThreshold: 0.5f,
                        cancellationToken: CancellationToken.None);

                    _logger.LogInformation(
                        "✅ [Background] Labels detectadas e salvas no cache | Label: {Label} | Detected: {Count} labels | Model: {Model}",
                        label,
                        detectedLabels.DetectedLabels.Count,
                        detectedLabels.ModelUsed);
                }
                catch (Exception ex)
                {
                    // Log erro mas não propaga (fire-and-forget)
                    _logger.LogError(ex,
                        "❌ [Background] Erro ao detectar labels | Label: {Label} | PdfHash: {PdfHash}",
                        label,
                        pdfHash[..8]);
                }
            });
        }
        catch (Exception ex)
        {
            // Captura erro na criação da task (não deve acontecer)
            _logger.LogError(ex,
                "❌ [Background] Erro ao iniciar detecção de labels | Label: {Label}",
                label);
        }
    }
}
