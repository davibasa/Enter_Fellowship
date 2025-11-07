using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.SmartExtraction
{
    /// <summary>
    /// Request para Smart Extraction
    /// </summary>
    public class SmartExtractionRequest
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("schema")]
        public Dictionary<string, string> Schema { get; set; } = new();

        [JsonPropertyName("label")]
        public string? Label { get; set; }
    }

    /// <summary>
    /// Response da Smart Extraction
    /// </summary>
    public class SmartExtractionResponse
    {
        [JsonPropertyName("fields")]
        public Dictionary<string, FieldExtractionResult> Fields { get; set; } = new();

        [JsonPropertyName("processing_time_ms")]
        public long ProcessingTimeMs { get; set; }

        [JsonPropertyName("total_confidence")]
        public float TotalConfidence { get; set; }

        [JsonPropertyName("fields_found")]
        public int FieldsFound { get; set; }

        [JsonPropertyName("fields_total")]
        public int FieldsTotal { get; set; }

        [JsonPropertyName("trace_id")]
        public string? TraceId { get; set; }
    }

    /// <summary>
    /// Resultado da extração de um campo específico
    /// </summary>
    public class FieldExtractionResult
    {
        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    /// <summary>
    /// Candidato testado durante extração (para debug)
    /// </summary>
    public class CandidateValue
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("line_count")]
        public int LineCount { get; set; }

        [JsonPropertyName("end_line_index")]
        public int EndLineIndex { get; set; }

        [JsonPropertyName("selected")]
        public bool Selected { get; set; }

        [JsonPropertyName("rejection_reason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RejectionReason { get; set; }
    }

    /// <summary>
    /// Tipo de campo com subtipos específicos
    /// </summary>
    public enum FieldType
    {
        // Campos simples com padrões específicos
        Date,           // Data (dd/mm/yyyy, yyyy-mm-dd, etc.)
        Currency,       // Valor monetário (R$ 1.000,00)
        Percentage,     // Percentual (10%, 0.15)
        Phone,          // Telefone ((11) 98765-4321)
        CPF,            // CPF (123.456.789-00)
        CNPJ,           // CNPJ (12.345.678/0001-00)
        Email,          // Email (user@domain.com)
        CEP,            // CEP (12345-678)
        Number,         // Número simples (inteiro ou decimal)
        Regex,         // Número simples (inteiro ou decimal)

        // Campos sem padrão específico
        Simple,         // Campo simples genérico (1 token)
        MultiLine,      // Campo multi-linha (endereço, descrição)
        Enum            // Campo com valores fixos (ATIVO, INATIVO)
    }

    /// <summary>
    /// Resultado de match de enum
    /// </summary>
    public class EnumMatchResult
    {
        public string Value { get; set; } = string.Empty;
        public int LineIndex { get; set; }
        public bool Found { get; set; }
    }

    /// <summary>
    /// Resultado de extração por regex
    /// </summary>
    public class RegexMatchResult
    {
        public string Value { get; set; } = string.Empty;
        public int LineIndex { get; set; }
        public string PatternType { get; set; } = string.Empty;
    }


    /// <summary>
    /// Request estendido para Smart Extraction com threshold configurável
    /// </summary>
    public class EnhancedSmartExtractionRequest
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("schema")]
        public Dictionary<string, string> Schema { get; set; } = new();

        [JsonPropertyName("confidence_threshold")]
        public float ConfidenceThreshold { get; set; } = 0.6f;

        [JsonPropertyName("enable_gpt_fallback")]
        public bool EnableGptFallback { get; set; } = true;
    }

    /// <summary>
    /// Response estendido com informações de cache, métodos usados e custos
    /// </summary>
    public class EnhancedSmartExtractionResponse
    {
        [JsonPropertyName("extracted_fields")]
        public Dictionary<string, EnhancedFieldResult> ExtractedFields { get; set; } = new();

        [JsonPropertyName("processing_time_ms")]
        public long ProcessingTimeMs { get; set; }

        [JsonPropertyName("cache_hit")]
        public bool CacheHit { get; set; }

        [JsonPropertyName("methods_used")]
        public List<string> MethodsUsed { get; set; } = new();

        [JsonPropertyName("gpt_fallback_used")]
        public bool GptFallbackUsed { get; set; }

        [JsonPropertyName("gpt_cost_usd")]
        public float? GptCostUsd { get; set; }

        [JsonPropertyName("total_confidence")]
        public float TotalConfidence { get; set; }

        [JsonPropertyName("fields_extracted")]
        public int FieldsExtracted { get; set; }

        [JsonPropertyName("fields_total")]
        public int FieldsTotal { get; set; }
    }

    /// <summary>
    /// Resultado de campo com informações detalhadas do pipeline
    /// </summary>
    public class EnhancedFieldResult
    {
        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty; // "cache", "ner", "embeddings", "gpt"

        [JsonPropertyName("found")]
        public bool Found { get; set; }

        [JsonPropertyName("entity_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EntityType { get; set; } // Para NER: "PER", "ORG", "LOC", etc.

        [JsonPropertyName("similarity_score")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? SimilarityScore { get; set; } // Para embeddings
    }

    /// <summary>
    /// Estatísticas do cache Redis
    /// </summary>
    public class CacheStats
    {
        [JsonPropertyName("total_keys")]
        public int TotalKeys { get; set; }

        [JsonPropertyName("memory_used_mb")]
        public float MemoryUsedMb { get; set; }

        [JsonPropertyName("hit_rate")]
        public float HitRate { get; set; }

        [JsonPropertyName("uptime_seconds")]
        public long UptimeSeconds { get; set; }
    }
    public class Phase25SmartExtractResponse
    {
        [JsonPropertyName("fields")]
        public Dictionary<string, Phase25FieldExtraction> Fields { get; set; } = new();

        [JsonPropertyName("confidence_avg")]
        public float ConfidenceAvg { get; set; }

        [JsonPropertyName("processing_time_ms")]
        public int ProcessingTimeMs { get; set; }

        [JsonPropertyName("cache_hit")]
        public bool CacheHit { get; set; }

        [JsonPropertyName("methods_used")]
        public Dictionary<string, int> MethodsUsed { get; set; } = new();

        [JsonPropertyName("gpt_fallback_used")]
        public bool GptFallbackUsed { get; set; }
    }

    /// <summary>
    /// Resultado da extração de um campo na FASE 2.5
    /// </summary>
    public class Phase25FieldExtraction
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("line_index")]
        public int? LineIndex { get; set; }
    }
}
