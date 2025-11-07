using Enter_Extractor_Api.Models.Cache;
using Enter_Extractor_Api.Services.Cache;
using Microsoft.AspNetCore.Mvc;

namespace Enter_Extractor_Api.Controllers;

/// <summary>
/// Controller para métricas de cache Redis
/// </summary>
[Route("api/cache")]
[ApiController]
public class CacheMetricsController : ControllerBase
{
    private readonly IMetricsCacheService _metricsService;
    private readonly ILogger<CacheMetricsController> _logger;

    public CacheMetricsController(
        IMetricsCacheService metricsService,
        ILogger<CacheMetricsController> logger)
    {
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Obtém métricas de cache para um dia específico
    /// </summary>
    /// <param name="date">Data no formato yyyy-MM-dd (default: hoje)</param>
    [HttpGet("metrics/{date?}")]
    [ProducesResponseType(typeof(CacheMetrics), 200)]
    public async Task<ActionResult<CacheMetrics>> GetDailyMetrics(string? date = null)
    {
        try
        {
            DateTime targetDate;

            if (string.IsNullOrEmpty(date))
            {
                targetDate = DateTime.UtcNow.Date;
            }
            else if (!DateTime.TryParse(date, out targetDate))
            {
                return BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd" });
            }

            var metrics = await _metricsService.GetDailyMetricsAsync(targetDate);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics for date {Date}", date);
            return StatusCode(500, new { error = "Internal error retrieving metrics" });
        }
    }

    /// <summary>
    /// Obtém resumo de métricas dos últimos N dias
    /// </summary>
    /// <param name="days">Quantidade de dias (default: 7)</param>
    [HttpGet("metrics/summary")]
    [ProducesResponseType(typeof(List<CacheMetrics>), 200)]
    public async Task<ActionResult<List<CacheMetrics>>> GetMetricsSummary([FromQuery] int days = 7)
    {
        try
        {
            if (days < 1 || days > 90)
            {
                return BadRequest(new { error = "Days must be between 1 and 90" });
            }

            var metrics = await _metricsService.GetMetricsSummaryAsync(days);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics summary for {Days} days", days);
            return StatusCode(500, new { error = "Internal error retrieving metrics summary" });
        }
    }

    /// <summary>
    /// Obtém estatísticas agregadas dos últimos N dias
    /// </summary>
    /// <param name="days">Quantidade de dias (default: 7)</param>
    [HttpGet("metrics/aggregate")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult> GetAggregateMetrics([FromQuery] int days = 7)
    {
        try
        {
            if (days < 1 || days > 90)
            {
                return BadRequest(new { error = "Days must be between 1 and 90" });
            }

            var metrics = await _metricsService.GetMetricsSummaryAsync(days);

            var aggregate = new
            {
                period = $"Last {days} days",
                total_hits = metrics.Sum(m => m.Hits),
                total_misses = metrics.Sum(m => m.Misses),
                total_requests = metrics.Sum(m => m.TotalRequests),
                avg_hit_rate = metrics.Any() ? metrics.Average(m => m.HitRate) : 0,
                total_time_saved_ms = metrics.Sum(m => m.TotalSavingsMs),
                total_cost_saved_usd = metrics.Sum(m => m.TotalSavingsUsd),
                daily_breakdown = metrics.Select(m => new
                {
                    date = m.Date.ToString("yyyy-MM-dd"),
                    hits = m.Hits,
                    misses = m.Misses,
                    hit_rate = m.HitRate,
                    time_saved = m.TimeSavedHuman,
                    cost_saved = $"${m.TotalSavingsUsd:F2}"
                })
            };

            return Ok(aggregate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving aggregate metrics for {Days} days", days);
            return StatusCode(500, new { error = "Internal error retrieving aggregate metrics" });
        }
    }
}
