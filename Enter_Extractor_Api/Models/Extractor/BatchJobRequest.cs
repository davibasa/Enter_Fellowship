namespace Enter_Extractor_Api.Models.Extractor;

/// <summary>
/// Request para processamento em lote de múltiplos PDFs
/// </summary>
public class BatchJobRequest
{
    /// <summary>
    /// Label comum para todos os documentos do lote
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Lista de PDFs a processar (cada um com identificador único)
    /// </summary>
    public List<BatchPdfItem> PdfItems { get; set; } = new();

    /// <summary>
    /// ID do usuário (opcional, default: "default-user")
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// ID do template usado (opcional)
    /// </summary>
    public string? TemplateId { get; set; }
}

/// <summary>
/// Item individual de PDF no lote
/// </summary>
public class BatchPdfItem
{
    /// <summary>
    /// Identificador único do arquivo (ex: nome original)
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// PDF em base64
    /// </summary>
    public string PdfBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Hash SHA-256 do schema (16 caracteres) - individual por PDF, usado para cache
    /// </summary>
    public string? SchemaHash { get; set; }

    /// <summary>
    /// Nome do arquivo PDF (opcional)
    /// </summary>
    public string? PdfFilename { get; set; }

    /// <summary>
    /// Dados de validação esperados (opcional - usado para comparar com resultado)
    /// Ex: { "nome": "João Silva", "inscricao": "12345" }
    /// </summary>
    public Dictionary<string, object?>? ValidationData { get; set; }
    public Dictionary<string, string?>? ExtractionSchema { get; set; }
}

/// <summary>
/// Resposta da criação de job batch
/// </summary>
public class BatchJobResponse
{
    /// <summary>
    /// ID único do job criado
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Status inicial do job
    /// </summary>
    public string Status { get; set; } = "queued";

    /// <summary>
    /// Total de PDFs a processar
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Timestamp de criação
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Status de um job batch
/// </summary>
public class BatchJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued"; // queued, processing, completed, failed
    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<BatchItemResult> Results { get; set; } = new();
}

/// <summary>
/// Resultado de um item processado
/// </summary>
public class BatchItemResult
{
    public string FileId { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, processing, success, error
    public ExtractorResponse? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public long ProcessingTimeMs { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public bool UsedCache { get; set; }
    public string? CacheType { get; set; } // exact, partial_complete, partial_hybrid, none
    public string? SchemaHash { get; set; } // Hash do schema usado na extração
    public Dictionary<string, object?>? ValidationData { get; set; } // Dados esperados para validação
}

/// <summary>
/// Evento SSE enviado ao cliente
/// </summary>
public class SSEEvent
{
    public string Type { get; set; } = string.Empty; // progress, result, error, complete
    public object? Data { get; set; }
}
