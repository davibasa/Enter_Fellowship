using Enter_Extractor_Api.Models.SmartExtraction;
using Enter_Extractor_Api.Services.SmartExtraction;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Enter_Extractor_Api.Controllers
{
    [Route("api/extraction")]
    [ApiController]
    public class SmartExtractionController : ControllerBase
    {
        private readonly IPythonExtractorClient _pythonClient;
        private readonly ILogger<SmartExtractionController> _logger;

        public SmartExtractionController(
            IPythonExtractorClient pythonClient,
            ILogger<SmartExtractionController> logger)
        {
            _pythonClient = pythonClient;
            _logger = logger;
        }

        /// <summary>
        /// Smart Extraction - Extração inteligente com ordem sequencial e enum automático
        /// Fase 1: Sem validação ML (heurísticas + ordem)
        /// </summary>
        /// <param name="request">Texto extraído + Schema ordenado</param>
        /// <returns>Campos extraídos com confiança e método usado</returns>
        [HttpPost("smart-extract")]
        [ProducesResponseType(typeof(SmartExtractionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SmartExtract([FromBody] SmartExtractionRequest request)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation(
                    "🚀 Iniciando Smart Extraction [FASE 2 - Zero-Shot NLI]. Label: {Label}, Campos: {Count}",
                    request.Label ?? "N/A",
                    request.Schema.Count
                );

                // Validações
                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    return BadRequest(new { error = "Text is required" });
                }

                if (request.Schema == null || !request.Schema.Any())
                {
                    return BadRequest(new { error = "Schema is required and must contain at least one field" });
                }

                // ⭐ FASE 2: Extrair campos sequencialmente com Zero-Shot NLI
                //result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                //stopwatch.Stop();

                //_logger.LogInformation(
                //    "✅ Smart Extraction concluída. Tempo: {Time}ms, Campos encontrados: {Found}/{Total}, Confiança: {Confidence:P1}",
                //    result.ProcessingTimeMs,
                //    result.FieldsFound,
                //    result.FieldsTotal,
                //    result.TotalConfidence
                //);

                //return Ok(result);
                return Ok();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Erro durante Smart Extraction");
                return StatusCode(500, new { error = ex.Message, stack_trace = ex.StackTrace });
            }
        }

        /// <summary>
        /// 🚀 Enhanced Smart Extraction - Pipeline Python completo (Cache → NER → Embeddings → GPT)
        /// Novo fluxo otimizado com cache Redis, NER spaCy, embeddings e fallback GPT condicional
        /// </summary>
        /// <param name="request">Texto + Schema + Label (opcional para cache)</param>
        /// <returns>Campos extraídos com metadados completos (cache, métodos, confiança, custos)</returns>
        [HttpPost("enhanced-smart-extract")]
        [ProducesResponseType(typeof(SmartExtractionPythonResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EnhancedSmartExtract([FromBody] SmartExtractionRequest request)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation(
                    "🚀 Iniciando Enhanced Smart Extraction [Python Pipeline]. " +
                    "Label: {Label}, Campos: {Count}, Texto: {TextLength} chars",
                    request.Label ?? "N/A",
                    request.Schema.Count,
                    request.Text.Length
                );

                // Validações
                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    return BadRequest(new { error = "Text is required" });
                }

                if (request.Schema == null || !request.Schema.Any())
                {
                    return BadRequest(new { error = "Schema is required and must contain at least one field" });
                }

                // Chamar Python API diretamente
                var result = await _pythonClient.SmartExtractAsync(
                    request.Label,
                    request.Text,
                    request.Schema,
                    confidenceThreshold: 0.7f
                );

                stopwatch.Stop();

                _logger.LogInformation(
                    "✅ Enhanced Smart Extraction concluída em {Time}ms. " +
                    "Cache: {Cache}, Confiança: {Conf:F2}, GPT: {Gpt}, Métodos: {Methods}",
                    stopwatch.ElapsedMilliseconds,
                    result.CacheHit ? "HIT" : "MISS",
                    result.AvgConfidence,
                    result.GptFallbackUsed,
                    string.Join(", ", result.MethodsUsed.Select(kvp => $"{kvp.Key}={kvp.Value}"))
                );

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "❌ Erro de comunicação com Python API após {Ms}ms", stopwatch.ElapsedMilliseconds);
                return StatusCode(503, new
                {
                    error = "Python API indisponível",
                    details = ex.Message,
                    elapsed_ms = stopwatch.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "❌ Erro no Enhanced Smart Extraction após {Ms}ms", stopwatch.ElapsedMilliseconds);
                return StatusCode(500, new
                {
                    error = ex.Message,
                    elapsed_ms = stopwatch.ElapsedMilliseconds
                });
            }
        }

        /// <summary>
        /// Health check do Enhanced Smart Extraction (verifica Python API + Redis)
        /// </summary>
        [HttpGet("enhanced-smart-extract/health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> EnhancedHealthCheck()
        {
            try
            {
                var isHealthy = await _pythonClient.IsHealthyAsync();

                if (isHealthy)
                {
                    return Ok(new
                    {
                        status = "healthy",
                        service = "Enhanced Smart Extraction (Python Pipeline)",
                        message = "Pipeline Cache → NER → Embeddings → GPT está operacional",
                        timestamp = DateTime.UtcNow
                    });
                }

                return StatusCode(503, new
                {
                    status = "unhealthy",
                    service = "Enhanced Smart Extraction",
                    message = "Python API ou Redis indisponíveis",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro no health check do Enhanced Smart Extraction");
                return StatusCode(503, new
                {
                    status = "unhealthy",
                    service = "Enhanced Smart Extraction",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Health check do Smart Extraction
        /// </summary>
        [HttpGet("smart-extract/health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                status = "healthy",
                service = "Smart Extraction",
                version = "1.0.0-phase1",
                features = new
                {
                    enum_detection = true,
                    sequential_extraction = true,
                    regex_patterns = true,
                    ml_validation = false // Fase 1: ainda não implementado
                },
                available_patterns = Services.SmartExtraction.RegexPatternBank.Patterns.Keys
            });
        }

        /// <summary>
        /// Testa detecção de enum em uma descrição
        /// </summary>
        [HttpPost("smart-extract/test-enum")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult TestEnumDetection([FromBody] TestEnumRequest request)
        {
            try
            {
                var enumParser = new Services.SmartExtraction.EnumParser(
                    _logger as ILogger<Services.SmartExtraction.EnumParser>
                );

                var enumValues = enumParser.ExtractEnumValues(request.Description);

                return Ok(new
                {
                    description = request.Description,
                    enums_detected = enumValues,
                    count = enumValues.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao testar detecção de enum");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Lista todos os padrões regex disponíveis
        /// </summary>
        [HttpGet("smart-extract/patterns")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult ListPatterns()
        {
            var patterns = Services.SmartExtraction.RegexPatternBank.Patterns
                .Select(kvp => new
                {
                    name = kvp.Key,
                    pattern = kvp.Value,
                    metadata = Services.SmartExtraction.RegexPatternBank.Metadata.ContainsKey(kvp.Key)
                        ? new
                        {
                            description = Services.SmartExtraction.RegexPatternBank.Metadata[kvp.Key].Description,
                            keywords = Services.SmartExtraction.RegexPatternBank.Metadata[kvp.Key].Keywords,
                            examples = Services.SmartExtraction.RegexPatternBank.Metadata[kvp.Key].Examples
                        }
                        : null
                })
                .ToList();

            return Ok(new
            {
                total_patterns = patterns.Count,
                patterns = patterns
            });
        }
    }

    /// <summary>
    /// Request para teste de detecção de enum
    /// </summary>
    public class TestEnumRequest
    {
        public string Description { get; set; } = string.Empty;
    }
}
