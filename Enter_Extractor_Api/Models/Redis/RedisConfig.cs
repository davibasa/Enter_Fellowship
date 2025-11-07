namespace Enter_Extractor_Api.Models.Redis;

public class RedisConfig
{
    public RedisCacheConfig Cache { get; set; } = new();
    public RedisStorageConfig Storage { get; set; } = new();
}

public class RedisCacheConfig
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "EnterExtract:Cache:";
    public int Database { get; set; } = 0;
    public int DefaultExpirationMinutes { get; set; } = 10080; // 7 dias
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 200;
}

public class RedisStorageConfig
{
    public string ConnectionString { get; set; } = "localhost:6380";
    public string InstanceName { get; set; } = "EnterExtract:Storage:";
    public int Database { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 200;
}
