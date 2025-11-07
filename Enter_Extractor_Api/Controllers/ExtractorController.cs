using Enter_Extractor_Api.Models;
using Enter_Extractor_Api.Services.SmartExtraction;
using Enter_Extractor_Api.Services.Cache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Enter_Extractor_Api.Services.Extractor;
using Enter_Extractor_Api.Models.Extractor;
using Enter_Extractor_Api.Services.Redis;
using Enter_Extractor_Api.Models.Redis;

namespace Enter_Extractor_Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExtractorController : ControllerBase
    {
        private readonly ILogger<ExtractorController> _logger;
        private readonly IExtractorService _extractorService;
        private readonly IExtractionCacheService _cacheService;
        private readonly IBatchJobService _batchJobService;
        private readonly IRedisPersistenceService _persistenceService;
        private readonly HttpClient _httpClient;
        private readonly string _pythonApiUrl;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ExtractorController(
            ILogger<ExtractorController> logger,
            IExtractorService extractorService,
            IExtractionCacheService cacheService,
            IBatchJobService batchJobService,
            IRedisPersistenceService persistenceService,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _logger = logger;
            _extractorService = extractorService;
            _cacheService = cacheService;
            _batchJobService = batchJobService;
            _persistenceService = persistenceService;
            _httpClient = httpClient;
            _pythonApiUrl = configuration["PythonApi:BaseUrl"] ?? "http://pdf-extractor:5000";
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResponse<ExtractorResponse>>> Extractor([FromBody] ExtractorRequest request)
        {
            var response = new ServiceResponse<ExtractorResponse>();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var pdfBytes = Convert.FromBase64String(request.PdfBase64);
                var pdfHash = _cacheService.CalculatePdfHash(pdfBytes);
                
                var cachedResult = await _cacheService.GetCachedExtractionAsync(
                    request.Label,
                    pdfHash,
                    request.ExtractionSchema);

                if (cachedResult != null)
                {
                    stopwatch.Stop();
                    _logger.LogInformation("✅ Cache HIT (Exact) - Returning cached result in {Ms}ms", stopwatch.ElapsedMilliseconds);

                    return Ok(new ServiceResponse<ExtractorResponse>
                    {
                        Data = cachedResult,
                        Success = true,
                        Message = "Resultado recuperado do cache (schema idêntico)",
                        Metadata = new Dictionary<string, object>
                        {
                            ["usedCache"] = true,
                            ["cacheType"] = "exact",
                            ["latencyMs"] = stopwatch.ElapsedMilliseconds,
                            ["pdfHash"] = pdfHash[..10]
                        }
                    });
                }

                var previouslyExtractedValues = await _cacheService.GetPreviouslyExtractedValuesAsync(pdfHash);

                var fieldsNeeded = request.ExtractionSchema.Keys
                    .Where(field => !previouslyExtractedValues.ContainsKey(field) ||
                                    previouslyExtractedValues[field] == null ||
                                    string.IsNullOrEmpty(previouslyExtractedValues[field].ToString()))
                    .ToList();

                if (fieldsNeeded.Count == 0)
                {
                    stopwatch.Stop();

                    var partialCacheResult = new ExtractorResponse
                    {
                        Schema = request.ExtractionSchema.Keys.ToDictionary(
                            k => k,
                            k => previouslyExtractedValues.ContainsKey(k) ? previouslyExtractedValues[k] : null
                        )
                    };

                    _ = Task.Run(async () =>
                    {
                        var cachedText = await _cacheService.GetCachedExtractedTextAsync(pdfHash);
                        await _cacheService.SaveExtractionAsync(
                            label: request.Label,
                            pdfHash: pdfHash,
                            schema: request.ExtractionSchema,
                            response: partialCacheResult,
                            processingTimeMs: stopwatch.ElapsedMilliseconds,
                            pdfSizeBytes: pdfBytes.Length,
                            extractedText: cachedText
                        );
                    });

                    return Ok(new ServiceResponse<ExtractorResponse>
                    {
                        Data = partialCacheResult,
                        Success = true,
                        Message = $"Todos os {request.ExtractionSchema.Count} campos encontrados em extrações anteriores",
                        Metadata = new Dictionary<string, object>
                        {
                            ["usedCache"] = true,
                            ["cacheType"] = "partial_complete",
                            ["fieldsFromCache"] = request.ExtractionSchema.Count,
                            ["latencyMs"] = stopwatch.ElapsedMilliseconds,
                            ["pdfHash"] = pdfHash[..10]
                        }
                    });
                }

                var extractedText = await _cacheService.GetCachedExtractedTextAsync(pdfHash);

                if (string.IsNullOrEmpty(extractedText))
                {

                    var requestPayload = new
                    {
                        pdf_base64 = request.PdfBase64
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
                    _logger.LogInformation("✅ Using cached extracted text ({CharCount} chars)", extractedText.Length);
                }

                var reducedText = _cacheService.RemoveCachedValuesFromText(extractedText, previouslyExtractedValues);

                var reducedSchema = fieldsNeeded.ToDictionary(
                    field => field,
                    field => request.ExtractionSchema[field]
                );
                
                var partialResult = await _extractorService.ExtractAsync(
                    request.Label,
                    reducedSchema,
                    reducedText)
                    .ConfigureAwait(false);

                var finalResult = _cacheService.MergeResults(
                    request.ExtractionSchema,
                    previouslyExtractedValues,
                    partialResult
                );

                stopwatch.Stop();
                var processingTimeMs = stopwatch.ElapsedMilliseconds;

                var fieldsFromCache = request.ExtractionSchema.Count - fieldsNeeded.Count;
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _cacheService.SaveExtractionAsync(
                            label: request.Label,
                            pdfHash: pdfHash,
                            schema: request.ExtractionSchema,
                            response: finalResult,
                            processingTimeMs: processingTimeMs,
                            tokensUsed: 0, 
                            costUsd: 0.0, 
                            strategiesUsed: "", 
                            pdfSizeBytes: pdfBytes.Length,
                            extractedText: extractedText  
                        );

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
                        var fieldsExtracted = finalResult.Schema.Count(kvp =>
                            kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.ToString()));

                        var historyDto = new ExtractionHistoryDto
                        {
                            UserId = request.UserId ?? "default-user",
                            PdfHash = pdfHash,
                            PdfFilename = request.PdfFilename ?? $"document_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
                            PdfSizeBytes = pdfBytes.Length,
                            Label = request.Label,
                            TemplateId = request.TemplateId,
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
                                ["extracted"] = fieldsNeeded.Count.ToString()
                            },
                            Result = finalResult.Schema.ToDictionary(
                                kvp => kvp.Key,
                                kvp => (object)(kvp.Value ?? "")
                            )!,
                            EditedManually = false,
                            Status = "completed"
                        };

                        var extractionId = await _persistenceService.SaveExtractionHistoryAsync(historyDto);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save extraction history (non-critical)");
                    }
                });

                return Ok(new ServiceResponse<ExtractorResponse>
                {
                    Data = finalResult,
                    Success = true,
                    Message = $"Extração concluída: {fieldsFromCache} campos do cache, {fieldsNeeded.Count} novos",
                    Metadata = new Dictionary<string, object>
                    {
                        ["usedCache"] = fieldsFromCache > 0,
                        ["cacheType"] = fieldsFromCache > 0 ? "partial_hybrid" : "none",
                        ["fieldsFromCache"] = fieldsFromCache,
                        ["fieldsExtracted"] = fieldsNeeded.Count,
                        ["latencyMs"] = processingTimeMs,
                        ["pdfHash"] = pdfHash[..10]
                    }
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling Python API at {Url}", _pythonApiUrl);
                return StatusCode(503, new ServiceResponse<ExtractorResponse>
                {
                    Success = false,
                    Message = $"Failed to connect to PDF extraction service: {ex.Message}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing extraction request");
                return StatusCode(500, new ServiceResponse<ExtractorResponse>
                {
                    Success = false,
                    Message = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Cria um job batch para processar múltiplos PDFs
        /// </summary>
        [HttpPost("batch")]
        public ActionResult<BatchJobResponse> CreateBatchJob([FromBody] BatchJobRequest request)
        {
            try
            {
                if (request.PdfItems == null || request.PdfItems.Count == 0)
                {
                    return BadRequest(new { message = "No PDF items provided" });
                }


                var jobId = _batchJobService.CreateJob(request);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _batchJobService.ProcessJobAsync(jobId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing batch job {JobId}", jobId);
                    }
                });

                var response = new BatchJobResponse
                {
                    JobId = jobId,
                    Status = "queued",
                    TotalItems = request.PdfItems.Count,
                    CreatedAt = DateTime.UtcNow
                };


                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating batch job");
                return StatusCode(500, new { message = $"Internal error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtém o status atual de um job batch
        /// </summary>
        [HttpGet("batch/{jobId}/status")]
        public ActionResult<BatchJobStatus> GetBatchJobStatus(string jobId)
        {
            var status = _batchJobService.GetJobStatus(jobId);

            if (status == null)
            {
                return NotFound(new { message = "Job not found" });
            }

            return Ok(status);
        }

        /// <summary>
        /// Stream SSE de progresso do job batch em tempo real
        /// </summary>
        [HttpGet("batch/{jobId}/stream")]
        public async Task StreamBatchJobProgress(string jobId, CancellationToken cancellationToken)
        {
            var status = _batchJobService.GetJobStatus(jobId);

            if (status == null)
            {
                Response.StatusCode = 404;
                await Response.WriteAsync("Job not found");
                return;
            }

            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";
            Response.Headers["X-Accel-Buffering"] = "no";

            _logger.LogInformation("📡 Starting SSE stream for job {JobId}", jobId);

            try
            {
                await foreach (var sseEvent in _batchJobService.StreamJobProgressAsync(jobId, cancellationToken))
                {

                    var eventData = JsonSerializer.Serialize(sseEvent.Data, _jsonOptions);

                    await Response.WriteAsync($"event: {sseEvent.Type}\n");
                    await Response.WriteAsync($"data: {eventData}\n\n");
                    await Response.Body.FlushAsync(cancellationToken);

                    if (sseEvent.Type == "complete" || sseEvent.Type == "error")
                    {
                        break;
                    }
                }

            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Client disconnected from SSE stream for job {JobId}", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SSE stream for job {JobId}", jobId);
            }
        }

        /// <summary>
        /// Busca todas as versões de schemas para um label
        /// </summary>
        [HttpGet("schema-versions/{label}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSchemaVersions(
            string label,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var versions = await _persistenceService.GetSchemaVersionsByLabelAsync(
                    label,
                    cancellationToken);

                if (versions.Count == 0)
                {
                    return NotFound(new
                    {
                        message = $"No schema versions found for label '{label}'"
                    });
                }

                return Ok(new
                {
                    label,
                    total = versions.Count,
                    versions = versions.Select(v => new
                    {
                        id = v.Id,
                        schema_hash = v.SchemaHash,
                        usage_count = v.UsageCount,
                        created_at = v.CreatedAt,
                        last_used_at = v.LastUsedAt,
                        avg_success_rate = v.AvgSuccessRate,
                        avg_processing_time_ms = v.AvgProcessingTimeMs,
                        is_default = v.IsDefault,
                        version_name = v.VersionName,
                        description = v.Description,
                        field_count = v.Schema.Count,
                        fields = v.Schema.Keys,
                        schema = v.Schema,
                        template_id = v.TemplateId,
                        extraction_count = v.ExtractionIds.Count
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting schema versions for label {Label}", label);
                return StatusCode(500, new
                {
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Busca uma versão específica de schema
        /// </summary>
        [HttpGet("schema-versions/by-id/{schemaId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSchemaVersion(
            string schemaId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var version = await _persistenceService.GetSchemaVersionAsync(
                    schemaId,
                    cancellationToken);

                if (version == null)
                {
                    return NotFound(new
                    {
                        message = $"Schema version '{schemaId}' not found"
                    });
                }

                return Ok(version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting schema version {SchemaId}", schemaId);
                return StatusCode(500, new
                {
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }
    }
}
