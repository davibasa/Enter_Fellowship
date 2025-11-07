using Enter_Extractor_Api.Models.Cache;

namespace Enter_Extractor_Api.Services.Cache;

/// <summary>
/// Interface para métricas de cache
/// </summary>
public interface IMetricsCacheService
{
    /// <summary>
    /// Incrementa contador de cache hit
    /// </summary>
    Task IncrementCacheHitAsync(long savedTimeMs = 3200, double savedCostUsd = 0.021);

    /// <summary>
    /// Incrementa contador de cache miss
    /// </summary>
    Task IncrementCacheMissAsync();

    /// <summary>
    /// Obtém métricas de um dia específico
    /// </summary>
    Task<CacheMetrics> GetDailyMetricsAsync(DateTime date);

    /// <summary>
    /// Obtém resumo dos últimos N dias
    /// </summary>
    Task<List<CacheMetrics>> GetMetricsSummaryAsync(int days = 7);
}
