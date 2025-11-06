using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models
{
    public class ExtractionRequest
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("extraction_schema")]
        public Dictionary<string, string> ExtractionSchema { get; set; } = new();

        [JsonPropertyName("pdf")]
        public string PdfBase64 { get; set; } = string.Empty;
    }

    public class ExtractionResponse
    {
        [JsonPropertyName("data")]
        public Dictionary<string, object?> Data { get; set; } = new();

        [JsonPropertyName("processing_time_ms")]
        public long ProcessingTimeMs { get; set; }

        [JsonPropertyName("used_cache")]
        public bool UsedCache { get; set; }

        [JsonPropertyName("used_heuristics")]
        public bool UsedHeuristics { get; set; }

        [JsonPropertyName("tokens_used")]
        public int TokensUsed { get; set; }
    }

    public class ExtractionResult
    {
        public Dictionary<string, object?> Data { get; set; } = new();
        public bool UsedCache { get; set; }
        public bool UsedHeuristics { get; set; }
        public int TokensUsed { get; set; }
        public double Confidence { get; set; }
    }

    public class PdfExtractionResponse
    {
        public string Text { get; set; } = string.Empty;
        public int CharCount { get; set; }
        public bool Success { get; set; }
    }
}
