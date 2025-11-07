using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.Extractor
{
    public class ExtractorResponse
    {
        [JsonPropertyName("schema")]
        public Dictionary<string, object?> Schema { get; set; } = new();
    }

    public class PdfExtractionResponse
    {
        public string Text { get; set; } = string.Empty;
        public int CharCount { get; set; }
        public bool Success { get; set; }
    }
}
