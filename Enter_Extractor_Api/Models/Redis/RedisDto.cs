namespace Enter_Extractor_Api.Models.Redis;

public class TemplateDto
{
    public string? Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, string> Schema { get; set; } = new();
    public Dictionary<string, object>? ValidationSchema { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int UsageCount { get; set; }
    public float AvgSuccessRate { get; set; }
    public int AvgProcessingTimeMs { get; set; }
    public string? Tags { get; set; }
    public bool IsPublic { get; set; }
    public string? Category { get; set; }
}

public class ExtractionHistoryDto
{
    public string? Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PdfHash { get; set; } = string.Empty;
    public string PdfFilename { get; set; } = string.Empty;
    public long PdfSizeBytes { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? TemplateId { get; set; }
    public string? SchemaVersionId { get; set; } 
    public string? SchemaHash { get; set; } 
    public DateTime? ExtractedAt { get; set; }
    public int ProcessingTimeMs { get; set; }
    public int TokensUsed { get; set; }
    public decimal CostUsd { get; set; }
    public int FieldsTotal { get; set; }
    public int FieldsExtracted { get; set; }
    public float SuccessRate { get; set; }
    public Dictionary<string, string>? Strategies { get; set; }
    public Dictionary<string, object>? Result { get; set; }
    public Dictionary<string, string>? Schema { get; set; }
    public bool EditedManually { get; set; }
    public string Status { get; set; } = "completed";
}

public class RedisCacheStats
{
    public long TotalKeys { get; set; }
    public long UsedMemoryBytes { get; set; }
    public string UsedMemoryHuman { get; set; } = string.Empty;
    public int ConnectedClients { get; set; }
    public long TotalCommandsProcessed { get; set; }
    public TimeSpan Uptime { get; set; }
    public long KeyspaceHits { get; set; }
    public long KeyspaceMisses { get; set; }
    public double HitRate { get; set; }
}
