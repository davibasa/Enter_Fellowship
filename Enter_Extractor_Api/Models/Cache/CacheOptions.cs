namespace Enter_Extractor_Api.Models.Cache;

/// <summary>
/// Configurações do Redis Cache
/// </summary>
public class CacheOptions
{
    public int DefaultTTLSeconds { get; set; } = 604800; // 7 dias
    public int EmbeddingTTLSeconds { get; set; } = 2592000; // 30 dias
    public int MetricsTTLSeconds { get; set; } = 7776000; // 90 dias
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 100;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
    public bool EnableCompression { get; set; } = false;
    public int MaxCachedResultSizeKB { get; set; } = 500;
}

/// <summary>
/// Configurações de hashing do PDF
/// </summary>
public class PdfHashingOptions
{
    public string Algorithm { get; set; } = "SHA256";
    public int TruncateLength { get; set; } = 40;
}

/// <summary>
/// Configuração completa do Redis
/// </summary>
public class RedisConfiguration
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public CacheOptions Cache { get; set; } = new();
    public PdfHashingOptions PdfHashing { get; set; } = new();
}
