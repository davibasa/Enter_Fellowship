using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Enter_Extractor_Api.Models.Redis;

namespace Enter_Extractor_Api.Services.Redis;

public class RedisPersistenceService : IRedisPersistenceService, IDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly string _instanceName;
    private readonly ILogger<RedisPersistenceService> _logger;

    public RedisPersistenceService(
        IOptions<RedisConfig> config,
        ILogger<RedisPersistenceService> logger)
    {
        _logger = logger;
        var storageConfig = config.Value.Storage;

        // Configurar options do Redis
        var options = ConfigurationOptions.Parse(storageConfig.ConnectionString);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = storageConfig.MaxRetries;
        options.ConnectTimeout = 5000;
        options.SyncTimeout = 5000;

        // Conectar
        _redis = ConnectionMultiplexer.Connect(options);
        _db = _redis.GetDatabase(storageConfig.Database);
        _instanceName = storageConfig.InstanceName;

        _logger.LogInformation("Redis Persistence Service connected: {Endpoint}",
            _redis.GetEndPoints().FirstOrDefault());
    }

    private string BuildKey(string key) => $"{_instanceName}{key}";

    // ============================================================================
    // TEMPLATES
    // ============================================================================

    public async Task<string> CreateTemplateAsync(TemplateDto template,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var templateId = template.Id ?? Guid.NewGuid().ToString();
            var key = BuildKey($"template:{template.UserId}:{templateId}");

            var fields = new Dictionary<string, string>
            {
                ["id"] = templateId,
                ["user_id"] = template.UserId,
                ["name"] = template.Name,
                ["description"] = template.Description ?? string.Empty,
                ["schema_json"] = JsonSerializer.Serialize(template.Schema),
                ["validation_schema_json"] = template.ValidationSchema != null
                    ? JsonSerializer.Serialize(template.ValidationSchema)
                    : string.Empty,
                ["created_at"] = DateTime.UtcNow.ToString("O"),
                ["updated_at"] = DateTime.UtcNow.ToString("O"),
                ["usage_count"] = "0",
                ["avg_success_rate"] = "0",
                ["avg_processing_time_ms"] = "0",
                ["tags"] = template.Tags ?? string.Empty,
                ["is_public"] = template.IsPublic.ToString(),
                ["category"] = template.Category ?? string.Empty
            };

            // Salvar template (sem TTL - persistente)
            await _db.HashSetAsync(key, fields.Select(kvp =>
                new HashEntry(kvp.Key, kvp.Value)).ToArray());

            // Adicionar nos índices
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await _db.SortedSetAddAsync(
                BuildKey($"templates:by_user:{template.UserId}"),
                templateId,
                timestamp);

            // Índice por tags
            if (!string.IsNullOrEmpty(template.Tags))
            {
                var tags = template.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var tag in tags)
                {
                    await _db.SetAddAsync(
                        BuildKey($"templates:by_tag:{tag.Trim().ToLower()}"),
                        templateId);
                }
            }

            // Se público, adicionar no ranking
            if (template.IsPublic)
            {
                await _db.SortedSetAddAsync(
                    BuildKey("templates:public"),
                    templateId,
                    0); // score inicial = 0
            }

            _logger.LogInformation("Template created: {TemplateId} ({Name}) for user {UserId}",
                templateId, template.Name, template.UserId);

            return templateId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template for user {UserId}", template.UserId);
            throw;
        }
    }

    public async Task<TemplateDto?> GetTemplateAsync(string userId, string templateId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"template:{userId}:{templateId}");
            var fields = await _db.HashGetAllAsync(key);

            if (fields.Length == 0)
            {
                return null;
            }

            var dict = fields.ToDictionary(
                x => x.Name.ToString(),
                x => x.Value.ToString());

            return new TemplateDto
            {
                Id = dict.GetValueOrDefault("id"),
                UserId = dict.GetValueOrDefault("user_id") ?? string.Empty,
                Name = dict.GetValueOrDefault("name") ?? string.Empty,
                Description = dict.GetValueOrDefault("description"),
                Schema = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    dict.GetValueOrDefault("schema_json") ?? "{}") ?? new(),
                ValidationSchema = !string.IsNullOrEmpty(dict.GetValueOrDefault("validation_schema_json"))
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(dict["validation_schema_json"])
                    : null,
                CreatedAt = DateTime.TryParse(dict.GetValueOrDefault("created_at"), out var ca) ? ca : null,
                UpdatedAt = DateTime.TryParse(dict.GetValueOrDefault("updated_at"), out var ua) ? ua : null,
                UsageCount = int.TryParse(dict.GetValueOrDefault("usage_count"), out var uc) ? uc : 0,
                AvgSuccessRate = float.TryParse(dict.GetValueOrDefault("avg_success_rate"), out var asr) ? asr : 0,
                AvgProcessingTimeMs = int.TryParse(dict.GetValueOrDefault("avg_processing_time_ms"), out var apt) ? apt : 0,
                Tags = dict.GetValueOrDefault("tags"),
                IsPublic = bool.TryParse(dict.GetValueOrDefault("is_public"), out var ip) && ip,
                Category = dict.GetValueOrDefault("category")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template {TemplateId} for user {UserId}", templateId, userId);
            return null;
        }
    }

    public async Task<List<TemplateDto>> GetUserTemplatesAsync(string userId,
        int page = 0, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"templates:by_user:{userId}");
            var start = page * pageSize;
            var stop = start + pageSize - 1;

            // Buscar IDs ordenados por timestamp (mais recentes primeiro)
            var templateIds = await _db.SortedSetRangeByScoreAsync(
                key,
                start: double.NegativeInfinity,
                stop: double.PositiveInfinity,
                skip: start,
                take: pageSize,
                order: Order.Descending);

            var templates = new List<TemplateDto>();

            foreach (var templateId in templateIds)
            {
                var template = await GetTemplateAsync(userId, templateId.ToString(), cancellationToken);
                if (template != null)
                {
                    templates.Add(template);
                }
            }

            return templates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting templates for user {UserId}", userId);
            return new List<TemplateDto>();
        }
    }

    public async Task<bool> UpdateTemplateAsync(TemplateDto template,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(template.Id))
            {
                _logger.LogWarning("Cannot update template without ID");
                return false;
            }

            var key = BuildKey($"template:{template.UserId}:{template.Id}");

            // Verificar se existe
            var exists = await _db.KeyExistsAsync(key);
            if (!exists)
            {
                _logger.LogWarning("Template {TemplateId} not found for user {UserId}",
                    template.Id, template.UserId);
                return false;
            }

            // Atualizar campos
            var updates = new List<HashEntry>
            {
                new("name", template.Name),
                new("description", template.Description ?? string.Empty),
                new("schema_json", JsonSerializer.Serialize(template.Schema)),
                new("updated_at", DateTime.UtcNow.ToString("O")),
                new("tags", template.Tags ?? string.Empty),
                new("is_public", template.IsPublic.ToString()),
                new("category", template.Category ?? string.Empty)
            };

            await _db.HashSetAsync(key, updates.ToArray());

            _logger.LogInformation("Template updated: {TemplateId} for user {UserId}",
                template.Id, template.UserId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template {TemplateId}", template.Id);
            return false;
        }
    }

    public async Task<bool> DeleteTemplateAsync(string userId, string templateId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"template:{userId}:{templateId}");

            // Buscar template para pegar tags
            var template = await GetTemplateAsync(userId, templateId, cancellationToken);
            if (template == null)
            {
                return false;
            }

            // Remover do hash principal
            await _db.KeyDeleteAsync(key);

            // Remover dos índices
            await _db.SortedSetRemoveAsync(
                BuildKey($"templates:by_user:{userId}"),
                templateId);

            // Remover das tags
            if (!string.IsNullOrEmpty(template.Tags))
            {
                var tags = template.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var tag in tags)
                {
                    await _db.SetRemoveAsync(
                        BuildKey($"templates:by_tag:{tag.Trim().ToLower()}"),
                        templateId);
                }
            }

            // Remover do público
            if (template.IsPublic)
            {
                await _db.SortedSetRemoveAsync(
                    BuildKey("templates:public"),
                    templateId);
            }

            _logger.LogInformation("Template deleted: {TemplateId} for user {UserId}",
                templateId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template {TemplateId}", templateId);
            return false;
        }
    }

    public async Task<bool> IncrementTemplateUsageAsync(string userId, string templateId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"template:{userId}:{templateId}");

            await _db.HashIncrementAsync(key, "usage_count", 1);

            var isPublic = await _db.HashGetAsync(key, "is_public");
            if (isPublic == "True")
            {
                await _db.SortedSetIncrementAsync(
                    BuildKey("templates:public"),
                    templateId,
                    1);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing usage for template {TemplateId}", templateId);
            return false;
        }
    }

    // ============================================================================
    // HISTÓRICO
    // ============================================================================

    public async Task<string> SaveExtractionHistoryAsync(ExtractionHistoryDto history,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var extractionId = history.Id ?? Guid.NewGuid().ToString();
            var key = BuildKey($"history:{history.UserId}:{extractionId}");

            var fields = new Dictionary<string, string>
            {
                ["id"] = extractionId,
                ["user_id"] = history.UserId,
                ["pdf_hash"] = history.PdfHash,
                ["pdf_filename"] = history.PdfFilename,
                ["pdf_size_bytes"] = history.PdfSizeBytes.ToString(),
                ["label"] = history.Label,
                ["template_id"] = history.TemplateId ?? string.Empty,
                ["schema_hash"] = history.SchemaHash ?? string.Empty,
                ["extracted_at"] = (history.ExtractedAt ?? DateTime.UtcNow).ToString("O"),
                ["processing_time_ms"] = history.ProcessingTimeMs.ToString(),
                ["tokens_used"] = history.TokensUsed.ToString(),
                ["cost_usd"] = history.CostUsd.ToString("F6"),
                ["fields_total"] = history.FieldsTotal.ToString(),
                ["fields_extracted"] = history.FieldsExtracted.ToString(),
                ["success_rate"] = history.SuccessRate.ToString("F4"),
                ["strategies_json"] = history.Strategies != null
                    ? JsonSerializer.Serialize(history.Strategies)
                    : "{}",
                ["result_json"] = history.Result != null
                    ? JsonSerializer.Serialize(history.Result)
                    : "{}",
                ["edited_manually"] = history.EditedManually.ToString(),
                ["status"] = history.Status
            };

            // Salvar com TTL de 90 dias
            await _db.HashSetAsync(key, fields.Select(kvp =>
                new HashEntry(kvp.Key, kvp.Value)).ToArray());
            await _db.KeyExpireAsync(key, TimeSpan.FromDays(90));

            // Adicionar nos índices
            var timestamp = (history.ExtractedAt ?? DateTime.UtcNow).ToUniversalTime()
                .Subtract(DateTime.UnixEpoch).TotalSeconds;

            await _db.SortedSetAddAsync(
                BuildKey($"history:by_user:{history.UserId}"),
                extractionId,
                timestamp);

            await _db.SortedSetAddAsync(
                BuildKey($"history:by_label:{history.Label}"),
                extractionId,
                timestamp);

            // Adicionar no set por data
            var date = (history.ExtractedAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd");
            await _db.SetAddAsync(
                BuildKey($"history:by_date:{date}"),
                extractionId);

            // Incrementar estatísticas do usuário
            await _db.HashIncrementAsync(
                BuildKey($"stats:user:{history.UserId}"),
                "extractions_count",
                1);
            await _db.HashIncrementAsync(
                BuildKey($"stats:user:{history.UserId}"),
                "tokens_used",
                history.TokensUsed);

            _logger.LogInformation("Extraction history saved: {ExtractionId} for user {UserId}",
                extractionId, history.UserId);

            return extractionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving extraction history for user {UserId}", history.UserId);
            throw;
        }
    }

    public async Task<ExtractionHistoryDto?> GetExtractionHistoryAsync(string userId,
        string extractionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"history:{userId}:{extractionId}");
            var fields = await _db.HashGetAllAsync(key);

            if (fields.Length == 0)
            {
                return null;
            }

            var dict = fields.ToDictionary(
                x => x.Name.ToString(),
                x => x.Value.ToString());

            return new ExtractionHistoryDto
            {
                Id = dict.GetValueOrDefault("id"),
                UserId = dict.GetValueOrDefault("user_id") ?? string.Empty,
                PdfHash = dict.GetValueOrDefault("pdf_hash") ?? string.Empty,
                PdfFilename = dict.GetValueOrDefault("pdf_filename") ?? string.Empty,
                PdfSizeBytes = long.TryParse(dict.GetValueOrDefault("pdf_size_bytes"), out var psb) ? psb : 0,
                Label = dict.GetValueOrDefault("label") ?? string.Empty,
                TemplateId = dict.GetValueOrDefault("template_id"),
                SchemaHash = dict.GetValueOrDefault("schema_hash"),
                ExtractedAt = DateTime.TryParse(dict.GetValueOrDefault("extracted_at"), out var ea) ? ea : null,
                ProcessingTimeMs = int.TryParse(dict.GetValueOrDefault("processing_time_ms"), out var ptm) ? ptm : 0,
                TokensUsed = int.TryParse(dict.GetValueOrDefault("tokens_used"), out var tu) ? tu : 0,
                CostUsd = decimal.TryParse(dict.GetValueOrDefault("cost_usd"), out var cu) ? cu : 0,
                FieldsTotal = int.TryParse(dict.GetValueOrDefault("fields_total"), out var ft) ? ft : 0,
                FieldsExtracted = int.TryParse(dict.GetValueOrDefault("fields_extracted"), out var fe) ? fe : 0,
                SuccessRate = float.TryParse(dict.GetValueOrDefault("success_rate"), out var sr) ? sr : 0,
                Strategies = !string.IsNullOrEmpty(dict.GetValueOrDefault("strategies_json"))
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(dict["strategies_json"])
                    : null,
                Result = !string.IsNullOrEmpty(dict.GetValueOrDefault("result_json"))
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(dict["result_json"])
                    : null,
                EditedManually = bool.TryParse(dict.GetValueOrDefault("edited_manually"), out var em) && em,
                Status = dict.GetValueOrDefault("status") ?? "completed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting extraction history {ExtractionId}", extractionId);
            return null;
        }
    }

    public async Task<List<ExtractionHistoryDto>> GetUserHistoryAsync(string userId,
        int page = 0, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"history:by_user:{userId}");
            var start = page * pageSize;
            var stop = start + pageSize - 1;

            // Buscar IDs ordenados por timestamp (mais recentes primeiro)
            var extractionIds = await _db.SortedSetRangeByScoreAsync(
                key,
                start: double.NegativeInfinity,
                stop: double.PositiveInfinity,
                skip: start,
                take: pageSize,
                order: Order.Descending);

            var history = new List<ExtractionHistoryDto>();

            foreach (var extractionId in extractionIds)
            {
                var item = await GetExtractionHistoryAsync(userId, extractionId.ToString(), cancellationToken);
                if (item != null)
                {
                    history.Add(item);
                }
            }

            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history for user {UserId}", userId);
            return new List<ExtractionHistoryDto>();
        }
    }

    // ============================================================================
    // ESTATÍSTICAS
    // ============================================================================

    public async Task IncrementGlobalStatAsync(string metric, string date, long value = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"stats:global:{metric}:{date}");
            await _db.StringIncrementAsync(key, value);
            await _db.KeyExpireAsync(key, TimeSpan.FromDays(365));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing global stat {Metric} for {Date}", metric, date);
        }
    }

    public async Task<long> GetGlobalStatAsync(string metric, string date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"stats:global:{metric}:{date}");
            var value = await _db.StringGetAsync(key);
            return value.HasValue ? (long)value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting global stat {Metric} for {Date}", metric, date);
            return 0;
        }
    }

    public async Task<Dictionary<string, long>> GetGlobalStatsAsync(string metric,
        List<string> dates, CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = new Dictionary<string, long>();

            foreach (var date in dates)
            {
                var value = await GetGlobalStatAsync(metric, date, cancellationToken);
                stats[date] = value;
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting global stats for metric {Metric}", metric);
            return new Dictionary<string, long>();
        }
    }

    // ============================================================================
    // PADRÕES APRENDIDOS
    // ============================================================================

    public async Task SavePatternAsync(string label, string fieldName, string value,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"pattern:{label}:{fieldName}");
            await _db.SortedSetIncrementAsync(key, value, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving pattern for {Label}.{Field}", label, fieldName);
        }
    }

    public async Task<List<(string value, double frequency)>> GetTopPatternsAsync(
        string label, string fieldName, int topN = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"pattern:{label}:{fieldName}");
            var entries = await _db.SortedSetRangeByScoreWithScoresAsync(
                key,
                start: double.NegativeInfinity,
                stop: double.PositiveInfinity,
                order: Order.Descending,
                take: topN);

            return entries.Select(e => (e.Element.ToString(), e.Score)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top patterns for {Label}.{Field}", label, fieldName);
            return new List<(string, double)>();
        }
    }

    public async Task<double?> GetPatternFrequencyAsync(string label, string fieldName,
        string value, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"pattern:{label}:{fieldName}");
            var score = await _db.SortedSetScoreAsync(key, value);
            return score;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pattern frequency for {Label}.{Field}", label, fieldName);
            return null;
        }
    }

    // ============================================================================
    // VERSÕES DE SCHEMAS
    // ============================================================================

    public async Task<string> SaveOrUpdateSchemaVersionAsync(
        string label,
        Dictionary<string, string> schema,
        string? templateId = null,
        string? schemaHash = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // ✨ NOVO: Usar schemaHash fornecido ou calcular
            var hash = !string.IsNullOrEmpty(schemaHash)
                ? schemaHash
                : CalculateSchemaHash(schema);

            // Verificar se já existe uma versão com este hash
            var existingVersion = await GetSchemaVersionByHashAsync(label, hash, cancellationToken);

            if (existingVersion != null)
            {
                // Versão já existe, apenas atualizar usage_count e last_used_at
                existingVersion.UsageCount++;
                existingVersion.LastUsedAt = DateTime.UtcNow;

                var key = BuildKey($"schema_version:{existingVersion.Id}");
                var json = JsonSerializer.Serialize(existingVersion);
                await _db.StringSetAsync(key, json, TimeSpan.FromDays(365)); // 1 ano

                // Atualizar índice por label
                await _db.SortedSetAddAsync(
                    BuildKey($"schema_versions:by_label:{label}"),
                    existingVersion.Id,
                    existingVersion.LastUsedAt.Ticks);

                _logger.LogInformation("Updated existing schema version {SchemaId} for label {Label} (usage: {Usage})",
                    existingVersion.Id, label, existingVersion.UsageCount);

                return existingVersion.Id;
            }

            // Nova versão - criar
            var schemaId = $"sv_{Guid.NewGuid():N}";
            var now = DateTime.UtcNow;

            var schemaVersion = new SchemaVersionDto
            {
                Id = schemaId,
                Label = label,
                TemplateId = templateId,
                Schema = schema,
                SchemaHash = hash, // ✨ CORRIGIDO: usar 'hash' ao invés de 'schemaHash'
                UsageCount = 1,
                CreatedAt = now,
                LastUsedAt = now,
                AvgSuccessRate = 0f,
                AvgProcessingTimeMs = 0,
                ExtractionIds = new List<string>(),
                IsDefault = false // Será definido posteriormente se necessário
            };

            // Salvar schema version
            var schemaKey = BuildKey($"schema_version:{schemaId}");
            var schemaJson = JsonSerializer.Serialize(schemaVersion);
            await _db.StringSetAsync(schemaKey, schemaJson, TimeSpan.FromDays(365)); // 1 ano

            // Adicionar a índices
            await _db.SortedSetAddAsync(
                BuildKey($"schema_versions:by_label:{label}"),
                schemaId,
                now.Ticks);

            await _db.SortedSetAddAsync(
                BuildKey($"schema_versions:by_hash:{label}:{hash}"), // ✨ CORRIGIDO: usar 'hash'
                schemaId,
                now.Ticks);

            _logger.LogInformation("Created new schema version {SchemaId} for label {Label} with {FieldCount} fields",
                schemaId, label, schema.Count);

            return schemaId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving schema version for label {Label}", label);
            throw;
        }
    }

    public async Task<SchemaVersionDto?> GetSchemaVersionAsync(
        string schemaId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"schema_version:{schemaId}");
            var json = await _db.StringGetAsync(key);

            if (json.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<SchemaVersionDto>(json!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schema version {SchemaId}", schemaId);
            return null;
        }
    }

    public async Task<List<SchemaVersionDto>> GetSchemaVersionsByLabelAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var indexKey = BuildKey($"schema_versions:by_label:{label}");

            // Buscar IDs ordenados por last_used_at (descendente)
            var schemaIds = await _db.SortedSetRangeByScoreAsync(
                indexKey,
                order: Order.Descending,
                take: 50); // Máximo 50 versões

            var versions = new List<SchemaVersionDto>();

            foreach (var schemaId in schemaIds)
            {
                var version = await GetSchemaVersionAsync(schemaId!, cancellationToken);
                if (version != null)
                {
                    versions.Add(version);
                }
            }

            _logger.LogInformation("Found {Count} schema versions for label {Label}", versions.Count, label);
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schema versions for label {Label}", label);
            return new List<SchemaVersionDto>();
        }
    }

    public async Task<SchemaVersionDto?> GetSchemaVersionByHashAsync(
        string label,
        string schemaHash,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var indexKey = BuildKey($"schema_versions:by_hash:{label}:{schemaHash}");
            var schemaIds = await _db.SortedSetRangeByScoreAsync(indexKey, take: 1);

            if (schemaIds.Length == 0)
                return null;

            return await GetSchemaVersionAsync(schemaIds[0]!, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schema version by hash for label {Label}", label);
            return null;
        }
    }

    public async Task<bool> UpdateSchemaVersionStatsAsync(
        string schemaId,
        float successRate,
        int processingTimeMs,
        string extractionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var version = await GetSchemaVersionAsync(schemaId, cancellationToken);
            if (version == null)
                return false;

            var totalExtractions = version.ExtractionIds.Count + 1;
            version.AvgSuccessRate = ((version.AvgSuccessRate * version.ExtractionIds.Count) + successRate) / totalExtractions;

            version.AvgProcessingTimeMs = ((version.AvgProcessingTimeMs * version.ExtractionIds.Count) + processingTimeMs) / totalExtractions;

            version.ExtractionIds.Add(extractionId);

            if (version.ExtractionIds.Count > 100)
            {
                version.ExtractionIds = version.ExtractionIds.Skip(version.ExtractionIds.Count - 100).ToList();
            }

            var key = BuildKey($"schema_version:{schemaId}");
            var json = JsonSerializer.Serialize(version);
            await _db.StringSetAsync(key, json, TimeSpan.FromDays(365));

            _logger.LogDebug("Updated stats for schema version {SchemaId}: success={Success}%, time={Time}ms",
                schemaId, successRate, processingTimeMs);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schema version stats for {SchemaId}", schemaId);
            return false;
        }
    }

    /// <summary>
    /// Calcula hash único do schema baseado nos campos (ordenados alfabeticamente)
    /// </summary>
    private string CalculateSchemaHash(Dictionary<string, string> schema)
    {
        var sortedFields = schema.Keys.OrderBy(k => k).ToArray();
        var fieldsString = string.Join("|", sortedFields);

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(fieldsString));
        return Convert.ToHexString(hashBytes)[..16]; // Primeiros 16 chars
    }

    // ============================================================================
    // LABELS DETECTADAS (RoBERTa/Embeddings)
    // ============================================================================

    public async Task SaveDetectedLabelsAsync(DetectedLabelsDto detectedLabels,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"detected_labels:{detectedLabels.Label}:{detectedLabels.PdfHash}:{detectedLabels.SchemaHash}");

            // Serializar para JSON
            var json = JsonSerializer.Serialize(detectedLabels);

            // Salvar com TTL de 30 dias
            await _db.StringSetAsync(key, json, TimeSpan.FromDays(30));

            // Adicionar ao índice por label
            var indexKey = BuildKey($"detected_labels:by_label:{detectedLabels.Label}");
            await _db.SetAddAsync(indexKey, $"{detectedLabels.PdfHash}:{detectedLabels.SchemaHash}");

            _logger.LogInformation(
                "✅ Labels detectadas salvas: {Label} | PdfHash: {PdfHash} | SchemaHash: {SchemaHash} | Detected: {Count}",
                detectedLabels.Label,
                detectedLabels.PdfHash[..8],
                detectedLabels.SchemaHash[..8],
                detectedLabels.DetectedLabels.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Error saving detected labels for {Label}:{PdfHash}:{SchemaHash}",
                detectedLabels.Label,
                detectedLabels.PdfHash,
                detectedLabels.SchemaHash);
            throw;
        }
    }

    public async Task<DetectedLabelsDto?> GetDetectedLabelsAsync(string label, string pdfHash,
        string schemaHash, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey($"detected_labels:{label}:{pdfHash}:{schemaHash}");
            var json = await _db.StringGetAsync(key);

            if (json.IsNullOrEmpty)
            {
                return null;
            }

            var result = JsonSerializer.Deserialize<DetectedLabelsDto>(json!);

            _logger.LogInformation(
                "✅ Cache hit: Labels detectadas | {Label} | PdfHash: {PdfHash} | SchemaHash: {SchemaHash}",
                label,
                pdfHash[..8],
                schemaHash[..8]);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Error getting detected labels for {Label}:{PdfHash}:{SchemaHash}",
                label,
                pdfHash,
                schemaHash);
            return null;
        }
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}
