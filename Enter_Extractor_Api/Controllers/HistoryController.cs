using Microsoft.AspNetCore.Mvc;
using Enter_Extractor_Api.Services.Redis;
using Enter_Extractor_Api.Models.Redis;

namespace Enter_Extractor_Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HistoryController : ControllerBase
{
    private readonly IRedisPersistenceService _persistence;
    private readonly ILogger<HistoryController> _logger;

    public HistoryController(
        IRedisPersistenceService persistence,
        ILogger<HistoryController> logger)
    {
        _persistence = persistence;
        _logger = logger;
    }

    /// <summary>
    /// Buscar histórico de extração por ID
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="extractionId">ID da extração</param>
    [HttpGet("{userId}/{extractionId}")]
    [ProducesResponseType(typeof(ExtractionHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(string userId, string extractionId)
    {
        try
        {
            var history = await _persistence.GetExtractionHistoryAsync(userId, extractionId);

            if (history == null)
            {
                return NotFound(new { error = "History not found" });
            }

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history {ExtractionId} for user {UserId}",
                extractionId, userId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Listar histórico do usuário (paginado)
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="page">Número da página (0-based)</param>
    /// <param name="pageSize">Itens por página (default: 20)</param>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(List<ExtractionHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserHistory(
        string userId,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var history = await _persistence.GetUserHistoryAsync(userId, page, pageSize);
            return Ok(new
            {
                page,
                pageSize,
                count = history.Count,
                items = history
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history for user {UserId}", userId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Salvar histórico de extração
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> SaveHistory([FromBody] ExtractionHistoryDto history)
    {
        try
        {
            var extractionId = await _persistence.SaveExtractionHistoryAsync(history);
            return CreatedAtAction(
                nameof(GetHistory),
                new { userId = history.UserId, extractionId },
                new { id = extractionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving history for user {UserId}", history.UserId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
