using Enter_Extractor_Api.Models.V2;
using Enter_Extractor_Api.Services.V2;
using Microsoft.AspNetCore.Mvc;

namespace Enter_Extractor_Api.Controllers.V2;

/// <summary>
/// Controller V2 - Novo fluxo orquestrado
/// C# delega 100% para Python (NLI, SmartExtract, Fallback)
/// </summary>
[ApiController]
[Route("api/v2/extraction")]
[Produces("application/json")]
public class ExtractionV2Controller : ControllerBase
{
    private readonly ExtractionOrchestratorService _orchestrator;
    private readonly ILogger<ExtractionV2Controller> _logger;

    public ExtractionV2Controller(
        ExtractionOrchestratorService orchestrator,
        ILogger<ExtractionV2Controller> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Extrai campos estruturados de texto usando o pipeline V2 completo
    /// </summary>
    /// <param name="request">Request com label, schema e texto</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Campos extraídos com métricas detalhadas</returns>
    /// <response code="200">Extração concluída com sucesso</response>
    /// <response code="400">Request inválido</response>
    /// <response code="500">Erro interno no processamento</response>
    [HttpPost("extract")]
    [ProducesResponseType(typeof(ExtractionV2Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExtractionV2Response>> Extract(
        [FromBody] ExtractionV2Request request,
        CancellationToken cancellationToken = default)
    {
        // Obter trace_id do HttpContext (definido pelo TraceMiddleware)
        var traceId = HttpContext.Items["TraceId"]?.ToString()
                      ?? Guid.NewGuid().ToString("N")[..16];

        try
        {
            // Validações
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                _logger.LogWarning("[TraceId: {TraceId}] Request inválido: texto vazio", traceId);
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "O campo 'text' é obrigatório e não pode estar vazio",
                    Status = StatusCodes.Status400BadRequest,
                    Instance = HttpContext.Request.Path
                });
            }

            if (request.Schema == null || request.Schema.Count == 0)
            {
                _logger.LogWarning("[TraceId: {TraceId}] Request inválido: schema vazio", traceId);
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "O campo 'schema' é obrigatório e deve conter ao menos um campo",
                    Status = StatusCodes.Status400BadRequest,
                    Instance = HttpContext.Request.Path
                });
            }

            // Executar extração orquestrada
            var response = await _orchestrator.ExtractAsync(request, traceId, cancellationToken);

            return Ok(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                "[TraceId: {TraceId}] Erro de comunicação com Python API: {Message}",
                traceId, ex.Message
            );

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Service Unavailable",
                Detail = $"Não foi possível comunicar com o serviço Python: {ex.Message}",
                Status = StatusCodes.Status503ServiceUnavailable,
                Instance = HttpContext.Request.Path
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[TraceId: {TraceId}] Request cancelado", traceId);

            return StatusCode(StatusCodes.Status499ClientClosedRequest, new ProblemDetails
            {
                Title = "Request Cancelled",
                Detail = "A requisição foi cancelada pelo cliente",
                Status = 499,
                Instance = HttpContext.Request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "[TraceId: {TraceId}] Erro interno: {Message}\n{StackTrace}",
                traceId, ex.Message, ex.StackTrace
            );

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "Ocorreu um erro inesperado durante o processamento",
                Status = StatusCodes.Status500InternalServerError,
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// Health check do pipeline V2
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            version = "2.0.0",
            timestamp = DateTime.UtcNow
        });
    }
}
