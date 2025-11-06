using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models
{
    public class ExtractorRequest
    {
        [Required]
        [JsonRequired]
        [JsonPropertyName("label")]
        public required string Label { get; set; } = string.Empty;

        [Required]
        [JsonRequired]
        [JsonPropertyName("extraction_schema")]
        public required Dictionary<string, string> ExtractionSchema { get; set; } = new();

        [Required]
        [JsonRequired]
        [JsonPropertyName("pdf")]
        public required string PdfBase64 { get; set; } = string.Empty;
    }
}
