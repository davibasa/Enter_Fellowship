namespace Enter_Extractor_Api.Models.V2;

public class ExtractionV2Config
{
    public string PythonApiUrl { get; set; } = "http://localhost:5001";
    public FeatureFlagsConfig FeatureFlags { get; set; } = new();
    public TimeoutsConfig Timeouts { get; set; } = new();
    public RetryConfig Retry { get; set; } = new();
    public CircuitBreakerConfig CircuitBreaker { get; set; } = new();
}

public class FeatureFlagsConfig
{
    public bool EnableSmartExtract { get; set; } = true;
    public bool EnableFallbackLLM { get; set; } = false;
    public bool EnableNliCaching { get; set; } = true;
}

public class TimeoutsConfig
{
    public int NliSeconds { get; set; } = 10;
    public int SmartExtractSeconds { get; set; } = 20;
    public int FallbackSeconds { get; set; } = 30;
    public int MetricsSeconds { get; set; } = 2;
}

public class RetryConfig
{
    public int MaxAttempts { get; set; } = 3;
    public int BackoffMs { get; set; } = 200;
}

public class CircuitBreakerConfig
{
    public int FailureThreshold { get; set; } = 5;
    public int DurationSeconds { get; set; } = 30;
}