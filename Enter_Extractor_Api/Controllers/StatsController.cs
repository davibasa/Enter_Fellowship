using Microsoft.AspNetCore.Mvc;
using Enter_Extractor_Api.Services.Redis;
using Enter_Extractor_Api.Services.Cache;
using Enter_Extractor_Api.Models.Redis;

namespace Enter_Extractor_Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatsController : ControllerBase
{
    private readonly IRedisPersistenceService _persistence;
    private readonly IRedisCacheService _cache;
    private readonly ILogger<StatsController> _logger;

    public StatsController(
        IRedisPersistenceService persistence,
        IRedisCacheService cache,
        ILogger<StatsController> logger)
    {
        _persistence = persistence;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Estatísticas globais de um dia específico
    /// </summary>
    /// <param name="date">Data no formato yyyy-MM-dd</param>
    [HttpGet("global/{date}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGlobalStats(string date)
    {
        try
        {
            var extractions = await _persistence.GetGlobalStatAsync("extractions", date);
            var tokensUsed = await _persistence.GetGlobalStatAsync("tokens_used", date);
            var costUsd = await _persistence.GetGlobalStatAsync("cost_usd_cents", date);

            return Ok(new
            {
                date,
                extractions,
                tokensUsed,
                costUsd = costUsd / 100.0 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting global stats for date {Date}", date);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Estatísticas globais de múltiplos dias
    /// </summary>
    [HttpPost("global/range")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGlobalStatsRange([FromBody] List<string> dates)
    {
        try
        {
            var extractionsStats = await _persistence.GetGlobalStatsAsync("extractions", dates);
            var tokensStats = await _persistence.GetGlobalStatsAsync("tokens_used", dates);
            var costStats = await _persistence.GetGlobalStatsAsync("cost_usd_cents", dates);

            var result = dates.Select(date => new
            {
                date,
                extractions = extractionsStats.GetValueOrDefault(date, 0),
                tokensUsed = tokensStats.GetValueOrDefault(date, 0),
                costUsd = costStats.GetValueOrDefault(date, 0) / 100.0
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting global stats range");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Estatísticas do cache
    /// </summary>
    [HttpGet("cache")]
    [ProducesResponseType(typeof(RedisCacheStats), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCacheStats()
    {
        try
        {
            var stats = await _cache.GetStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache stats");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Top padrões aprendidos para um label e campo
    /// </summary>
    /// <param name="label">Label do documento</param>
    /// <param name="fieldName">Nome do campo</param>
    /// <param name="topN">Número de top padrões (default: 5)</param>
    [HttpGet("patterns/{label}/{fieldName}")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopPatterns(
        string label,
        string fieldName,
        [FromQuery] int topN = 5)
    {
        try
        {
            var patterns = await _persistence.GetTopPatternsAsync(label, fieldName, topN);
            var result = patterns.Select(p => new
            {
                value = p.value,
                frequency = p.frequency
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top patterns for {Label}.{Field}", label, fieldName);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Verificar frequência de um padrão específico
    /// </summary>
    [HttpGet("patterns/{label}/{fieldName}/{value}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatternFrequency(
        string label,
        string fieldName,
        string value)
    {
        try
        {
            var frequency = await _persistence.GetPatternFrequencyAsync(label, fieldName, value);
            return Ok(new
            {
                label,
                fieldName,
                value,
                frequency = frequency ?? 0,
                isCommon = frequency.HasValue && frequency.Value > 5
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pattern frequency for {Label}.{Field}={Value}",
                label, fieldName, value);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
