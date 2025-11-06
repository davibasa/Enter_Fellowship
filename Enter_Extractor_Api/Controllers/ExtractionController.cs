using Enter_Extractor_Api.Models;
using Enter_Extractor_Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Enter_Extractor_Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExtractionController : ControllerBase
    {
        private readonly IExtractionService _extractionService;
        private readonly ILogger<ExtractionController> _logger;

        public ExtractionController(
            IExtractionService extractionService,
            ILogger<ExtractionController> logger)
        {
            _extractionService = extractionService;
            _logger = logger;
        }

        [HttpPost("extract")]
        public async Task<IActionResult> Extract([FromBody] ExtractionRequest request)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Starting extraction for label: {Label}", request.Label);

                if (string.IsNullOrEmpty(request.Label))
                    return BadRequest(new { error = "Label is required" });

                if (request.ExtractionSchema == null || !request.ExtractionSchema.Any())
                    return BadRequest(new { error = "Extraction schema is required" });

                if (string.IsNullOrEmpty(request.PdfBase64))
                    return BadRequest(new { error = "PDF data is required" });

                var result = await _extractionService.ExtractAsync(
                    request.Label,
                    request.ExtractionSchema,
                    request.PdfBase64
                );

                stopwatch.Stop();
                _logger.LogInformation(
                    "Extraction completed in {ElapsedMs}ms for label: {Label}",
                    stopwatch.ElapsedMilliseconds,
                    request.Label
                );

                return Ok(new ExtractionResponse
                {
                    Data = result.Data,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    UsedCache = result.UsedCache,
                    UsedHeuristics = result.UsedHeuristics,
                    TokensUsed = result.TokensUsed
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error during extraction for label: {Label}", request.Label);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("extract-batch")]
        public async Task<IActionResult> ExtractBatch([FromBody] List<ExtractionRequest> requests)
        {
            var stopwatch = Stopwatch.StartNew();
            var results = new List<ExtractionResponse>();

            try
            {
                _logger.LogInformation("Starting batch extraction for {Count} documents", requests.Count);

                // Process first document immediately to meet <10s requirement
                if (requests.Any())
                {
                    var firstResult = await _extractionService.ExtractAsync(
                        requests[0].Label,
                        requests[0].ExtractionSchema,
                        requests[0].PdfBase64
                    );

                    results.Add(new ExtractionResponse
                    {
                        Data = firstResult.Data,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        UsedCache = firstResult.UsedCache,
                        UsedHeuristics = firstResult.UsedHeuristics,
                        TokensUsed = firstResult.TokensUsed
                    });

                    // Process remaining documents
                    for (int i = 1; i < requests.Count; i++)
                    {
                        var itemStopwatch = Stopwatch.StartNew();
                        var result = await _extractionService.ExtractAsync(
                            requests[i].Label,
                            requests[i].ExtractionSchema,
                            requests[i].PdfBase64
                        );
                        itemStopwatch.Stop();

                        results.Add(new ExtractionResponse
                        {
                            Data = result.Data,
                            ProcessingTimeMs = itemStopwatch.ElapsedMilliseconds,
                            UsedCache = result.UsedCache,
                            UsedHeuristics = result.UsedHeuristics,
                            TokensUsed = result.TokensUsed
                        });
                    }
                }

                stopwatch.Stop();
                _logger.LogInformation(
                    "Batch extraction completed in {ElapsedMs}ms for {Count} documents",
                    stopwatch.ElapsedMilliseconds,
                    requests.Count
                );

                return Ok(new
                {
                    results,
                    totalProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    averageTimeMs = results.Any() ? results.Average(r => r.ProcessingTimeMs) : 0,
                    totalTokensUsed = results.Sum(r => r.TokensUsed)
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error during batch extraction");
                return StatusCode(500, new { error = ex.Message, results });
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }
}
