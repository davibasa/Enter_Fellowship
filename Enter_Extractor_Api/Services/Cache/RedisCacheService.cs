using StackExchange.Redis;
using System.Text.Json;
using Enter_Extractor_Api.Models.Cache;
using Microsoft.Extensions.Options;

namespace Enter_Extractor_Api.Services.Cache;

/// <summary>
/// Implementação do serviço de cache Redis com retry e circuit breaker
/// </summary>
public class RedisCacheService : IRedisCacheService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly CacheOptions _cacheOptions;
    private int _circuitBreakerFailures = 0;
    private DateTime? _circuitBreakerOpenedAt = null;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger,
        IOptions<RedisConfiguration> config)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _db = _redis.GetDatabase();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheOptions = config.Value.Cache;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return null;

            var value = await RetryAsync(async () => await _db.StringGetAsync(key));

            if (!value.HasValue)
                return null;

            var result = JsonSerializer.Deserialize<T>(value!);
            ResetCircuitBreaker();
            return result;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error getting key {key}");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing cached value for key {Key}", key);
            await DeleteAsync(key); // Remove dados corrompidos
            return null;
        }
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return false;

            var json = JsonSerializer.Serialize(value);
            var ttl = expiry ?? TimeSpan.FromSeconds(_cacheOptions.DefaultTTLSeconds);

            var success = await RetryAsync(async () => await _db.StringSetAsync(key, json, ttl));
            ResetCircuitBreaker();
            return success;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error setting key {key}");
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string key)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return false;

            var success = await RetryAsync(async () => await _db.KeyDeleteAsync(key));
            ResetCircuitBreaker();
            return success;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error deleting key {key}");
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return false;

            var exists = await RetryAsync(async () => await _db.KeyExistsAsync(key));
            ResetCircuitBreaker();
            return exists;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error checking existence of key {key}");
            return false;
        }
    }

    public async Task<bool> HashSetAllAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return false;

            var json = JsonSerializer.Serialize(value);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (dict == null)
                return false;

            var entries = dict.Select(kvp => new HashEntry(kvp.Key, JsonSerializer.Serialize(kvp.Value))).ToArray();

            await RetryAsync(async () =>
            {
                await _db.HashSetAsync(key, entries);
                return true;
            });

            if (expiry.HasValue)
            {
                await _db.KeyExpireAsync(key, expiry.Value);
            }

            ResetCircuitBreaker();
            return true;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error setting hash {key}");
            return false;
        }
    }

    public async Task<T?> HashGetAllAsync<T>(string key) where T : class, new()
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return null;

            var entries = await RetryAsync(async () => await _db.HashGetAllAsync(key));

            if (entries == null || entries.Length == 0)
                return null;

            var dict = entries.ToDictionary(
                e => e.Name.ToString(),
                e => JsonSerializer.Deserialize<object>(e.Value!)
            );

            var json = JsonSerializer.Serialize(dict);
            var result = JsonSerializer.Deserialize<T>(json);

            ResetCircuitBreaker();
            return result;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error getting hash {key}");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing hash for key {Key}", key);
            await DeleteAsync(key);
            return null;
        }
    }

    public async Task<long> StringIncrementAsync(string key, long value = 1)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return 0;

            var result = await RetryAsync(async () => await _db.StringIncrementAsync(key, value));
            ResetCircuitBreaker();
            return result;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error incrementing key {key}");
            return 0;
        }
    }

    public async Task<double> StringIncrementAsync(string key, double value)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return 0;

            var result = await RetryAsync(async () => await _db.StringIncrementAsync(key, value));
            ResetCircuitBreaker();
            return result;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error incrementing key {key}");
            return 0;
        }
    }

    public async Task<string?> StringGetAsync(string key)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return null;

            var value = await RetryAsync(async () => await _db.StringGetAsync(key));
            ResetCircuitBreaker();
            return value.HasValue ? value.ToString() : null;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error getting string key {key}");
            return null;
        }
    }

    public async Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return false;

            var success = await RetryAsync(async () => await _db.StringSetAsync(key, value, expiry));
            ResetCircuitBreaker();
            return success;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error setting string key {key}");
            return false;
        }
    }

    public async Task<bool> ExpireAsync(string key, TimeSpan expiry)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return false;

            var success = await RetryAsync(async () => await _db.KeyExpireAsync(key, expiry));
            ResetCircuitBreaker();
            return success;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error setting expiry for key {key}");
            return false;
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            return _redis.IsConnected && await _db.PingAsync() != TimeSpan.Zero;
        }
        catch
        {
            return false;
        }
    }

    // --- Circuit Breaker Logic ---

    private async Task<bool> CheckCircuitBreakerAsync()
    {
        if (_circuitBreakerOpenedAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - _circuitBreakerOpenedAt.Value;
            var breakerDuration = TimeSpan.FromSeconds(_cacheOptions.CircuitBreakerDurationSeconds);

            if (elapsed < breakerDuration)
            {
                return false;
            }

            _circuitBreakerOpenedAt = null;
            _circuitBreakerFailures = 0;
        }

        return await Task.FromResult(true);
    }

    private void ResetCircuitBreaker()
    {
        if (_circuitBreakerFailures > 0)
        {
            _circuitBreakerFailures = 0;
        }
    }

    private void LogRedisError(Exception ex, string message)
    {
        _circuitBreakerFailures++;

        if (_circuitBreakerFailures >= _cacheOptions.CircuitBreakerThreshold)
        {
            _circuitBreakerOpenedAt = DateTime.UtcNow;
        }
    }

    // --- Retry Logic ---

    private async Task<T> RetryAsync<T>(Func<Task<T>> operation)
    {
        var attempt = 0;

        while (attempt < _cacheOptions.MaxRetries)
        {
            try
            {
                return await operation();
            }
            catch (RedisException ex)
            {
                attempt++;

                if (attempt >= _cacheOptions.MaxRetries)
                {
                    throw;
                }

                var delay = _cacheOptions.RetryDelayMs * attempt;
                _logger.LogDebug(ex, "Redis operation failed (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                    attempt, _cacheOptions.MaxRetries, delay);

                await Task.Delay(delay);
            }
        }

        throw new InvalidOperationException("Retry logic failed unexpectedly");
    }

    public async Task<List<string>> ScanKeysAsync(string pattern, int count = 100)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return new List<string>();

            var keys = new List<string>();
            var server = _redis.GetServer(_redis.GetEndPoints().First());

            await foreach (var key in server.KeysAsync(pattern: pattern, pageSize: count))
            {
                keys.Add(key.ToString());
            }

            ResetCircuitBreaker();
            return keys;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error scanning keys with pattern {pattern}");
            return new List<string>();
        }
    }

    // ⭐ FASE 2: Implementação de Sets
    public async Task<bool> SetAddAsync(string key, string member)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return false;

            var result = await RetryAsync(async () => await _db.SetAddAsync(key, member));
            ResetCircuitBreaker();
            return result;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error adding member to set {key}");
            return false;
        }
    }

    public async Task<bool> SetRemoveAsync(string key, string member)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return false;

            var result = await RetryAsync(async () => await _db.SetRemoveAsync(key, member));
            ResetCircuitBreaker();
            return result;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error removing member from set {key}");
            return false;
        }
    }

    public async Task<List<string>> SetMembersAsync(string key)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return new List<string>();

            var members = await RetryAsync(async () => await _db.SetMembersAsync(key));
            ResetCircuitBreaker();
            return members.Select(m => m.ToString()).ToList();
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error getting set members from {key}");
            return new List<string>();
        }
    }

    public async Task<bool> SetContainsAsync(string key, string member)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return false;

            var result = await RetryAsync(async () => await _db.SetContainsAsync(key, member));
            ResetCircuitBreaker();
            return result;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error checking set membership in {key}");
            return false;
        }
    }

    // ⭐ FASE 2: Implementação de Sorted Sets
    public async Task<bool> SortedSetAddAsync(string key, string member, double score)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return false;

            var result = await RetryAsync(async () => await _db.SortedSetAddAsync(key, member, score));
            ResetCircuitBreaker();
            return result;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error adding member to sorted set {key}");
            return false;
        }
    }

    public async Task<bool> SortedSetRemoveAsync(string key, string member)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return false;

            var result = await RetryAsync(async () => await _db.SortedSetRemoveAsync(key, member));
            ResetCircuitBreaker();
            return result;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error removing member from sorted set {key}");
            return false;
        }
    }

    public async Task<double> SortedSetIncrementAsync(string key, string member, double value)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return 0;

            var result = await RetryAsync(async () => await _db.SortedSetIncrementAsync(key, member, value));
            ResetCircuitBreaker();
            return result;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error incrementing sorted set member in {key}");
            return 0;
        }
    }

    public async Task<List<string>> SortedSetRangeByScoreAsync(string key, double min, double max, int take = -1, bool descending = false)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return new List<string>();

            var order = descending ? Order.Descending : Order.Ascending;
            var members = await RetryAsync(async () =>
                await _db.SortedSetRangeByScoreAsync(key, min, max, take: take, order: order));

            ResetCircuitBreaker();
            return members.Select(m => m.ToString()).ToList();
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error getting sorted set range from {key}");
            return new List<string>();
        }
    }

    // ⭐ FASE 2: Operações Hash adicionais
    public async Task<bool> HashSetAsync(string key, string field, string value)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return false;

            await RetryAsync(async () => await _db.HashSetAsync(key, field, value));
            ResetCircuitBreaker();
            return true;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error setting hash field in {key}");
            return false;
        }
    }

    public async Task<long> HashIncrementAsync(string key, string field, long value)
    {
        try
        {
            if (!await CheckCircuitBreakerAsync())
                return 0;

            var result = await RetryAsync(async () => await _db.HashIncrementAsync(key, field, value));
            ResetCircuitBreaker();
            return result;
        }
        catch (RedisException ex)
        {
            LogRedisError(ex, $"Error incrementing hash field in {key}");
            return 0;
        }
    }

    // ⭐ FASE 3: Estatísticas do Redis
    public async Task<Models.Redis.RedisCacheStats> GetStatsAsync()
    {
        try
        {
            var endpoints = _redis.GetEndPoints();
            var server = _redis.GetServer(endpoints.First());
            var info = await server.InfoAsync();

            var stats = new Models.Redis.RedisCacheStats();

            // Extrair informações do INFO
            foreach (var group in info)
            {
                foreach (var item in group)
                {
                    switch (item.Key)
                    {
                        case "used_memory":
                            stats.UsedMemoryBytes = long.TryParse(item.Value, out var mem) ? mem : 0;
                            break;
                        case "used_memory_human":
                            stats.UsedMemoryHuman = item.Value;
                            break;
                        case "connected_clients":
                            stats.ConnectedClients = int.TryParse(item.Value, out var clients) ? clients : 0;
                            break;
                        case "total_commands_processed":
                            stats.TotalCommandsProcessed = long.TryParse(item.Value, out var cmds) ? cmds : 0;
                            break;
                        case "uptime_in_seconds":
                            stats.Uptime = int.TryParse(item.Value, out var uptime)
                                ? TimeSpan.FromSeconds(uptime)
                                : TimeSpan.Zero;
                            break;
                        case "keyspace_hits":
                            stats.KeyspaceHits = long.TryParse(item.Value, out var hits) ? hits : 0;
                            break;
                        case "keyspace_misses":
                            stats.KeyspaceMisses = long.TryParse(item.Value, out var misses) ? misses : 0;
                            break;
                    }
                }
            }

            var total = stats.KeyspaceHits + stats.KeyspaceMisses;
            stats.HitRate = total > 0 ? (double)stats.KeyspaceHits / total * 100 : 0;
            
            stats.TotalKeys = await server.DatabaseSizeAsync(_db.Database);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Redis stats");
            return new Models.Redis.RedisCacheStats
            {
                UsedMemoryHuman = "Error"
            };
        }
    }

    public void Dispose()
    {
        // ConnectionMultiplexer é singleton, não dispose aqui
    }
}
