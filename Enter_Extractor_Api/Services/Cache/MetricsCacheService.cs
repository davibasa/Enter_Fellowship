using Enter_Extractor_Api.Models.Cache;
using Microsoft.Extensions.Options;

namespace Enter_Extractor_Api.Services.Cache;

/// <summary>
/// Serviço de métricas de cache
/// </summary>
public class MetricsCacheService : IMetricsCacheService
{
    private readonly IRedisCacheService _redis;
    private readonly ILogger<MetricsCacheService> _logger;
    private readonly RedisConfiguration _config;

    public MetricsCacheService(
        IRedisCacheService redis,
        ILogger<MetricsCacheService> logger,
        IOptions<RedisConfiguration> config)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config.Value;
    }

    public async Task IncrementCacheHitAsync(long savedTimeMs = 3200, double savedCostUsd = 0.021)
    {
        try
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            await _redis.StringIncrementAsync($"stats:cache:hits:{today}", 1);
            await _redis.StringIncrementAsync($"stats:cache:total_savings_ms:{today}", savedTimeMs);
            await _redis.StringIncrementAsync($"stats:cache:total_savings_usd:{today}", savedCostUsd);

            // Atualizar hit rate
            await UpdateHitRateAsync(today);

            // Configurar TTL
            var ttl = TimeSpan.FromSeconds(_config.Cache.MetricsTTLSeconds);
            await _redis.ExpireAsync($"stats:cache:hits:{today}", ttl);
            await _redis.ExpireAsync($"stats:cache:total_savings_ms:{today}", ttl);
            await _redis.ExpireAsync($"stats:cache:total_savings_usd:{today}", ttl);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing cache hit");
        }
    }

    public async Task IncrementCacheMissAsync()
    {
        try
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            await _redis.StringIncrementAsync($"stats:cache:misses:{today}", 1);

            await UpdateHitRateAsync(today);

            var ttl = TimeSpan.FromSeconds(_config.Cache.MetricsTTLSeconds);
            await _redis.ExpireAsync($"stats:cache:misses:{today}", ttl);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing cache miss");
        }
    }

    public async Task<CacheMetrics> GetDailyMetricsAsync(DateTime date)
    {
        try
        {
            var dateStr = date.ToString("yyyy-MM-dd");

            var hitsStr = await _redis.StringGetAsync($"stats:cache:hits:{dateStr}");
            var missesStr = await _redis.StringGetAsync($"stats:cache:misses:{dateStr}");
            var hitRateStr = await _redis.StringGetAsync($"stats:cache:hit_rate:{dateStr}");
            var savingsMsStr = await _redis.StringGetAsync($"stats:cache:total_savings_ms:{dateStr}");
            var savingsUsdStr = await _redis.StringGetAsync($"stats:cache:total_savings_usd:{dateStr}");

            var hits = long.TryParse(hitsStr, out var h) ? h : 0;
            var misses = long.TryParse(missesStr, out var m) ? m : 0;
            var hitRate = double.TryParse(hitRateStr, out var hr) ? hr : 0;
            var savingsMs = long.TryParse(savingsMsStr, out var sms) ? sms : 0;
            var savingsUsd = double.TryParse(savingsUsdStr, out var susd) ? susd : 0;

            return new CacheMetrics
            {
                Date = date,
                Hits = hits,
                Misses = misses,
                HitRate = hitRate,
                TotalSavingsMs = savingsMs,
                TotalSavingsUsd = savingsUsd
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving daily metrics for {Date}", date);
            return new CacheMetrics { Date = date };
        }
    }

    public async Task<List<CacheMetrics>> GetMetricsSummaryAsync(int days = 7)
    {
        var metrics = new List<CacheMetrics>();

        for (int i = 0; i < days; i++)
        {
            var date = DateTime.UtcNow.AddDays(-i).Date;
            var dailyMetrics = await GetDailyMetricsAsync(date);
            metrics.Add(dailyMetrics);
        }

        return metrics;
    }

    private async Task UpdateHitRateAsync(string dateStr)
    {
        try
        {
            var hitsStr = await _redis.StringGetAsync($"stats:cache:hits:{dateStr}");
            var missesStr = await _redis.StringGetAsync($"stats:cache:misses:{dateStr}");

            var hits = long.TryParse(hitsStr, out var h) ? h : 0;
            var misses = long.TryParse(missesStr, out var m) ? m : 0;
            var total = hits + misses;

            if (total > 0)
            {
                var hitRate = (double)hits / total;
                await _redis.StringSetAsync($"stats:cache:hit_rate:{dateStr}", hitRate.ToString("F4"));

                var ttl = TimeSpan.FromSeconds(_config.Cache.MetricsTTLSeconds);
                await _redis.ExpireAsync($"stats:cache:hit_rate:{dateStr}", ttl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating hit rate for {Date}", dateStr);
        }
    }
}
