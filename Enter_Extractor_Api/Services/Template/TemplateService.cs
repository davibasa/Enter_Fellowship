using System.Text.Json;
using Enter_Extractor_Api.Models.Template;
using Enter_Extractor_Api.Services.Cache;

namespace Enter_Extractor_Api.Services.Template;

/// <summary>
/// Serviço de gerenciamento de templates
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly IRedisCacheService _redis;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(IRedisCacheService redis, ILogger<TemplateService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<Models.Template.Template> CreateAsync(string userId, CreateTemplateRequest request)
    {
        // Validações manuais (removidas dos atributos por conflito com OpenAPI)
        if (request.Schema == null || request.Schema.Count == 0)
        {
            throw new ArgumentException("Schema deve ter pelo menos 1 campo");
        }

        if (request.Schema.Count > 50)
        {
            throw new ArgumentException("Schema pode ter no máximo 50 campos");
        }

        if (request.Tags != null && request.Tags.Count > 10)
        {
            throw new ArgumentException("Máximo 10 tags permitidas");
        }

        var templateId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var template = new Models.Template.Template
        {
            Id = templateId,
            UserId = userId,
            Name = request.Name,
            Description = request.Description,
            SchemaJson = JsonSerializer.Serialize(request.Schema),
            Schema = request.Schema,
            Tags = string.Join(",", request.Tags ?? new List<string>()),
            IsPublic = request.IsPublic,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now,
            UsageCount = 0,
            AvgSuccessRate = 0,
            AvgProcessingTimeMs = 0,
            Version = 1,
            FieldsCount = request.Schema.Count
        };

        // Salvar template
        var key = $"template:{userId}:{templateId}";
        await _redis.HashSetAllAsync(key, template);

        // Adicionar aos índices
        await _redis.SetAddAsync($"templates:by_user:{userId}", templateId);

        foreach (var tag in request.Tags ?? new List<string>())
        {
            await _redis.SetAddAsync($"templates:by_tag:{tag.ToLower()}", templateId);
        }

        if (request.IsPublic)
        {
            await _redis.SortedSetAddAsync("templates:public", templateId, 0);
        }

        var unixTimestamp = new DateTimeOffset(now).ToUnixTimeSeconds();
        await _redis.SortedSetAddAsync("templates:recent", templateId, unixTimestamp);

        _logger.LogInformation("✅ Template criado: {TemplateId} - {Name} ({FieldsCount} campos)",
            templateId, request.Name, request.Schema.Count);

        return template;
    }

    public async Task<Models.Template.Template?> GetByIdAsync(string userId, string templateId)
    {
        var key = $"template:{userId}:{templateId}";
        var template = await _redis.HashGetAllAsync<Models.Template.Template>(key);

        if (template != null && !string.IsNullOrEmpty(template.SchemaJson))
        {
            template.Schema = JsonSerializer.Deserialize<Dictionary<string, string>>(template.SchemaJson);
        }

        return template;
    }

    public async Task<(List<Models.Template.Template> Templates, int Total)> ListUserTemplatesAsync(
        string userId, int page = 1, int limit = 20, string? searchQuery = null, List<string>? tags = null)
    {
        var templateIds = await _redis.SetMembersAsync($"templates:by_user:{userId}");
        var templates = new List<Models.Template.Template>();

        foreach (var templateId in templateIds)
        {
            var template = await GetByIdAsync(userId, templateId);
            if (template != null && !template.IsDeleted)
            {
                templates.Add(template);
            }
        }

        // Filtrar por busca
        if (!string.IsNullOrEmpty(searchQuery))
        {
            templates = templates.Where(t =>
                t.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Filtrar por tags
        if (tags != null && tags.Any())
        {
            templates = templates.Where(t =>
                tags.Any(tag => t.TagList.Contains(tag, StringComparer.OrdinalIgnoreCase))).ToList();
        }

        var total = templates.Count;

        // Ordenar e paginar
        templates = templates
            .OrderByDescending(t => t.UpdatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        return (templates, total);
    }

    public async Task<List<Models.Template.Template>> ListPublicTemplatesAsync(int limit = 10)
    {
        var templateIds = await _redis.SortedSetRangeByScoreAsync("templates:public", 0, double.MaxValue, limit, true);
        var templates = new List<Models.Template.Template>();

        foreach (var templateId in templateIds)
        {
            // Buscar template em todos os usuários (ineficiente, mas funcional para MVP)
            // TODO: Melhorar com índice secundário
            var template = await GetByIdAsync("*", templateId);
            if (template != null && !template.IsDeleted)
            {
                templates.Add(template);
            }
        }

        return templates;
    }

    public async Task<Models.Template.Template> UpdateAsync(string userId, string templateId, UpdateTemplateRequest request)
    {
        var template = await GetByIdAsync(userId, templateId);
        if (template == null)
        {
            throw new KeyNotFoundException($"Template {templateId} não encontrado");
        }

        // Validações manuais
        if (request.Schema != null && request.Schema.Count > 50)
        {
            throw new ArgumentException("Schema pode ter no máximo 50 campos");
        }

        if (request.Tags != null && request.Tags.Count > 10)
        {
            throw new ArgumentException("Máximo 10 tags permitidas");
        }

        var now = DateTime.UtcNow;
        var key = $"template:{userId}:{templateId}";

        if (request.Name != null) template.Name = request.Name;
        if (request.Description != null) template.Description = request.Description;

        if (request.Schema != null)
        {
            template.Schema = request.Schema;
            template.SchemaJson = JsonSerializer.Serialize(request.Schema);
            template.FieldsCount = request.Schema.Count;
        }

        if (request.Tags != null)
        {
            // Remover das tags antigas
            var oldTags = template.TagList;
            foreach (var oldTag in oldTags)
            {
                await _redis.SetRemoveAsync($"templates:by_tag:{oldTag.ToLower()}", templateId);
            }

            // Adicionar nas tags novas
            template.Tags = string.Join(",", request.Tags);
            foreach (var tag in request.Tags)
            {
                await _redis.SetAddAsync($"templates:by_tag:{tag.ToLower()}", templateId);
            }
        }

        if (request.IsPublic.HasValue)
        {
            template.IsPublic = request.IsPublic.Value;
            if (template.IsPublic)
            {
                await _redis.SortedSetAddAsync("templates:public", templateId, template.UsageCount);
            }
            else
            {
                await _redis.SortedSetRemoveAsync("templates:public", templateId);
            }
        }

        template.UpdatedAt = now;
        template.Version++;

        await _redis.HashSetAllAsync(key, template);

        _logger.LogInformation("✅ Template atualizado: {TemplateId} - {Name}", templateId, template.Name);

        return template;
    }

    public async Task<bool> DeleteAsync(string userId, string templateId)
    {
        var key = $"template:{userId}:{templateId}";

        // Soft delete
        await _redis.HashSetAsync(key, "is_deleted", "true");

        // Remover dos índices
        await _redis.SetRemoveAsync($"templates:by_user:{userId}", templateId);
        await _redis.SortedSetRemoveAsync("templates:public", templateId);
        await _redis.SortedSetRemoveAsync("templates:recent", templateId);

        // Definir TTL de 90 dias para recovery
        await _redis.ExpireAsync(key, TimeSpan.FromDays(90));

        _logger.LogInformation("🗑️ Template deletado (soft): {TemplateId}", templateId);

        return true;
    }

    public async Task IncrementUsageAsync(string userId, string templateId, double successRate, long processingTimeMs)
    {
        var key = $"template:{userId}:{templateId}";

        // Incrementar contador de uso
        await _redis.HashIncrementAsync(key, "usage_count", 1);

        // Atualizar última vez usado
        await _redis.HashSetAsync(key, "last_used_at", DateTime.UtcNow.ToString("O"));

        // TODO: Atualizar médias (requer calcular média móvel)

        // Incrementar no ranking público se aplicável
        var template = await GetByIdAsync(userId, templateId);
        if (template?.IsPublic == true)
        {
            await _redis.SortedSetIncrementAsync("templates:public", templateId, 1);
        }

        _logger.LogDebug("📊 Template {TemplateId} usado (total: {Count})", templateId, template?.UsageCount ?? 0);
    }

    public async Task<List<Models.Template.Template>> SearchAsync(string userId, string query)
    {
        var (templates, _) = await ListUserTemplatesAsync(userId, 1, 100, query);
        return templates;
    }

    public async Task<Models.Template.Template> ClonePublicTemplateAsync(string userId, string templateId)
    {
        // Buscar template original (de qualquer usuário)
        var original = await GetByIdAsync("*", templateId);
        if (original == null || !original.IsPublic)
        {
            throw new UnauthorizedAccessException("Template não é público ou não existe");
        }

        var request = new CreateTemplateRequest
        {
            Name = $"{original.Name} (Cópia)",
            Description = original.Description,
            Schema = original.Schema ?? new Dictionary<string, string>(),
            Tags = original.TagList,
            IsPublic = false
        };

        return await CreateAsync(userId, request);
    }
}
