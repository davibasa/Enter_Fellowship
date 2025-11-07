namespace Enter_Extractor_Api.Services.Cache;

/// <summary>
/// Interface genérica para operações de cache Redis
/// </summary>
public interface IRedisCacheService
{
    /// <summary>
    /// Obtém um valor do cache
    /// </summary>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// Define um valor no cache com TTL
    /// </summary>
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;

    /// <summary>
    /// Remove uma chave do cache
    /// </summary>
    Task<bool> DeleteAsync(string key);

    /// <summary>
    /// Verifica se uma chave existe
    /// </summary>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Define todos os campos de um hash
    /// </summary>
    Task<bool> HashSetAllAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;

    /// <summary>
    /// Obtém todos os campos de um hash
    /// </summary>
    Task<T?> HashGetAllAsync<T>(string key) where T : class, new();

    /// <summary>
    /// Incrementa um valor numérico
    /// </summary>
    Task<long> StringIncrementAsync(string key, long value = 1);

    /// <summary>
    /// Incrementa um valor double
    /// </summary>
    Task<double> StringIncrementAsync(string key, double value);

    /// <summary>
    /// Obtém um valor string
    /// </summary>
    Task<string?> StringGetAsync(string key);

    /// <summary>
    /// Define um valor string
    /// </summary>
    Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null);

    /// <summary>
    /// Define expiração de uma chave
    /// </summary>
    Task<bool> ExpireAsync(string key, TimeSpan expiry);

    /// <summary>
    /// Verifica se o Redis está disponível
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Busca chaves que correspondem a um padrão (usando SCAN)
    /// </summary>
    Task<List<string>> ScanKeysAsync(string pattern, int count = 100);

    // ⭐ FASE 2: Operações com Sets (Templates por usuário, tags)
    /// <summary>
    /// Adiciona um membro a um set
    /// </summary>
    Task<bool> SetAddAsync(string key, string member);

    /// <summary>
    /// Remove um membro de um set
    /// </summary>
    Task<bool> SetRemoveAsync(string key, string member);

    /// <summary>
    /// Obtém todos os membros de um set
    /// </summary>
    Task<List<string>> SetMembersAsync(string key);

    /// <summary>
    /// Verifica se um membro existe no set
    /// </summary>
    Task<bool> SetContainsAsync(string key, string member);

    // ⭐ FASE 2: Operações com Sorted Sets (Rankings, linha do tempo)
    /// <summary>
    /// Adiciona um membro a um sorted set com score
    /// </summary>
    Task<bool> SortedSetAddAsync(string key, string member, double score);

    /// <summary>
    /// Remove um membro de um sorted set
    /// </summary>
    Task<bool> SortedSetRemoveAsync(string key, string member);

    /// <summary>
    /// Incrementa o score de um membro
    /// </summary>
    Task<double> SortedSetIncrementAsync(string key, string member, double value);

    /// <summary>
    /// Obtém membros por range de score (ordenado por score)
    /// </summary>
    Task<List<string>> SortedSetRangeByScoreAsync(string key, double min, double max, int take = -1, bool descending = false);

    // ⭐ FASE 2: Operações Hash adicionais
    /// <summary>
    /// Define um único campo em um hash
    /// </summary>
    Task<bool> HashSetAsync(string key, string field, string value);

    /// <summary>
    /// Incrementa um campo numérico em um hash
    /// </summary>
    Task<long> HashIncrementAsync(string key, string field, long value);

    /// <summary>
    /// Obtém estatísticas do Redis
    /// </summary>
    Task<Models.Redis.RedisCacheStats> GetStatsAsync();
}
