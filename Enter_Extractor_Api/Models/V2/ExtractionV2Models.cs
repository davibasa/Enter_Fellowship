using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.V2;

/// <summary>
/// Request para extração V2 (novo fluxo orquestrado)
/// </summary>
public class ExtractionV2Request
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("schema")]
    public Dictionary<string, string> Schema { get; set; } = new();

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("options")]
    public ExtractionOptions? Options { get; set; }
}

/// <summary>
/// Opções de extração
/// </summary>
public class ExtractionOptions
{
    [JsonPropertyName("enable_smart_extract")]
    public bool EnableSmartExtract { get; set; } = true;

    [JsonPropertyName("enable_fallback_llm")]
    public bool EnableFallbackLLM { get; set; } = false;

    [JsonPropertyName("enable_nli_caching")]
    public bool EnableNliCaching { get; set; } = true;

    [JsonPropertyName("confidence_threshold")]
    public float ConfidenceThreshold { get; set; } = 0.7f;
}

/// <summary>
/// Response da extração V2
/// </summary>
public class ExtractionV2Response
{
    [JsonPropertyName("trace_id")]
    public string TraceId { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public Dictionary<string, ExtractedFieldV2> Fields { get; set; } = new();

    [JsonPropertyName("confidence_avg")]
    public float ConfidenceAvg { get; set; }

    [JsonPropertyName("processing_ms")]
    public long ProcessingMs { get; set; }

    [JsonPropertyName("phases")]
    public PhasesMetrics Phases { get; set; } = new();

    [JsonPropertyName("fallback_used")]
    public bool FallbackUsed { get; set; }
}

/// <summary>
/// Campo extraído V2
/// </summary>
public class ExtractedFieldV2
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("line_index")]
    public int? LineIndex { get; set; }

    [JsonPropertyName("source_phase")]
    public string SourcePhase { get; set; } = string.Empty; // "phase1", "phase2.5", "phase3", "fallback"
}

/// <summary>
/// Métricas por fase
/// </summary>
public class PhasesMetrics
{
    [JsonPropertyName("phase1_ms")]
    public long Phase1Ms { get; set; }

    [JsonPropertyName("phase1_fields")]
    public int Phase1Fields { get; set; }

    [JsonPropertyName("phase2_ms")]
    public long Phase2Ms { get; set; }

    [JsonPropertyName("phase2_labels_removed")]
    public int Phase2LabelsRemoved { get; set; }

    [JsonPropertyName("phase2_cache_hit")]
    public bool Phase2CacheHit { get; set; }

    [JsonPropertyName("phase2_5_ms")]
    public long Phase25Ms { get; set; }

    [JsonPropertyName("phase2_5_fields")]
    public int Phase25Fields { get; set; }

    [JsonPropertyName("phase2_5_cache_hit")]
    public bool Phase25CacheHit { get; set; }

    [JsonPropertyName("phase3_ms")]
    public long Phase3Ms { get; set; }

    [JsonPropertyName("fallback_ms")]
    public long FallbackMs { get; set; }

    [JsonPropertyName("fallback_fields")]
    public int FallbackFields { get; set; }
}

// ========== DTOs para chamadas Python ==========

/// <summary>
/// Request para /nli/classify
/// </summary>
public class NliClassifyRequest
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("schema")]
    public Dictionary<string, string> Schema { get; set; } = new();

    [JsonPropertyName("text_blocks")]
    public List<string> TextBlocks { get; set; } = new();
}

/// <summary>
/// Response de /nli/classify
/// </summary>
public class NliClassifyResponse
{
    [JsonPropertyName("classified_blocks")]
    public List<ClassifiedBlock> ClassifiedBlocks { get; set; } = new();

    [JsonPropertyName("labels_detected")]
    public List<string> LabelsDetected { get; set; } = new();

    [JsonPropertyName("processing_time_ms")]
    public int ProcessingTimeMs { get; set; }

    [JsonPropertyName("cache_hits")]
    public int CacheHits { get; set; }

    [JsonPropertyName("total_blocks")]
    public int TotalBlocks { get; set; }
}

public class ClassifiedBlock
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }
}

/// <summary>
/// Request para /smart-extract
/// </summary>
public class SmartExtractRequest
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("schema")]
    public Dictionary<string, string> Schema { get; set; } = new();

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("confidence_threshold")]
    public float ConfidenceThreshold { get; set; } = 0.7f;

    [JsonPropertyName("enable_gpt_fallback")]
    public bool EnableGptFallback { get; set; } = false;

    [JsonPropertyName("options")]
    public SmartExtractOptions? Options { get; set; }
}

public class SmartExtractOptions
{
    [JsonPropertyName("use_memory")]
    public bool UseMemory { get; set; } = true;

    [JsonPropertyName("max_lines")]
    public int MaxLines { get; set; } = 200;
}

/// <summary>
/// Response de /smart-extract
/// </summary>
public class SmartExtractResponse
{
    [JsonPropertyName("fields")]
    public Dictionary<string, SmartExtractField> Fields { get; set; } = new();

    [JsonPropertyName("confidence_avg")]
    public float ConfidenceAvg { get; set; }

    [JsonPropertyName("processing_time_ms")]
    public int ProcessingTimeMs { get; set; }

    [JsonPropertyName("cache_hit")]
    public bool CacheHit { get; set; }

    [JsonPropertyName("methods_used")]
    public Dictionary<string, int>? MethodsUsed { get; set; }

    [JsonPropertyName("gpt_fallback_used")]
    public bool GptFallbackUsed { get; set; }
}

public class SmartExtractField
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("line_index")]
    public int? LineIndex { get; set; }
}

/// <summary>
/// Request para /llm/fallback
/// </summary>
public class LlmFallbackRequest
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("schema")]
    public Dictionary<string, string> Schema { get; set; } = new();

    [JsonPropertyName("partial_results")]
    public Dictionary<string, PartialResult> PartialResults { get; set; } = new();

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("options")]
    public LlmFallbackOptions? Options { get; set; }
}

public class PartialResult
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }
}

public class LlmFallbackOptions
{
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0.2f;

    [JsonPropertyName("model")]
    public string Model { get; set; } = "gpt-4o-mini";
}

/// <summary>
/// Response de /llm/fallback
/// </summary>
public class LlmFallbackResponse
{
    [JsonPropertyName("fields")]
    public Dictionary<string, LlmFallbackField> Fields { get; set; } = new();

    [JsonPropertyName("processing_ms")]
    public int ProcessingMs { get; set; }

    [JsonPropertyName("cache_hit")]
    public bool CacheHit { get; set; }
}

public class LlmFallbackField
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "gpt_fallback";
}

/// <summary>
/// Response de /metrics/threshold/{label}
/// </summary>
public class ThresholdMetricsResponse
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("threshold")]
    public float Threshold { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}
