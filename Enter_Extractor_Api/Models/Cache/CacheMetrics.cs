using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.Cache;

/// <summary>
/// Métricas de cache para um dia específico
/// </summary>
public class CacheMetrics
{
    /// <summary>
    /// Data das métricas
    /// </summary>
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    /// <summary>
    /// Quantidade de cache hits
    /// </summary>
    [JsonPropertyName("cache_hits")]
    public long Hits { get; set; }

    /// <summary>
    /// Quantidade de cache misses
    /// </summary>
    [JsonPropertyName("cache_misses")]
    public long Misses { get; set; }

    /// <summary>
    /// Taxa de acerto (hits / total)
    /// </summary>
    [JsonPropertyName("hit_rate")]
    public double HitRate { get; set; }

    /// <summary>
    /// Total de requisições (hits + misses)
    /// </summary>
    [JsonPropertyName("total_requests")]
    public long TotalRequests => Hits + Misses;

    /// <summary>
    /// Tempo economizado em millisegundos
    /// </summary>
    [JsonPropertyName("time_saved_ms")]
    public long TotalSavingsMs { get; set; }

    /// <summary>
    /// Tempo economizado em formato legível
    /// </summary>
    [JsonPropertyName("time_saved_human")]
    public string TimeSavedHuman => FormatMilliseconds(TotalSavingsMs);

    /// <summary>
    /// Custo economizado em USD
    /// </summary>
    [JsonPropertyName("cost_saved_usd")]
    public double TotalSavingsUsd { get; set; }

    /// <summary>
    /// Latência média de cache hit (estimada)
    /// </summary>
    [JsonPropertyName("avg_cache_latency_ms")]
    public int AvgCacheLatencyMs { get; set; } = 45;

    /// <summary>
    /// Latência média de cache miss (estimada)
    /// </summary>
    [JsonPropertyName("avg_miss_latency_ms")]
    public int AvgMissLatencyMs { get; set; } = 3200;

    private static string FormatMilliseconds(long ms)
    {
        var timeSpan = TimeSpan.FromMilliseconds(ms);

        if (timeSpan.TotalDays >= 1)
            return $"{timeSpan.TotalDays:F1} dias";
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.TotalHours:F1} horas";
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.TotalMinutes:F1} minutos";

        return $"{timeSpan.TotalSeconds:F1} segundos";
    }
}
