using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.Extractor
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

        /// <summary>
        /// ID do usuário (opcional, default: "default-user")
        /// </summary>
        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }

        /// <summary>
        /// Nome do arquivo PDF (opcional)
        /// </summary>
        [JsonPropertyName("pdf_filename")]
        public string? PdfFilename { get; set; }

        /// <summary>
        /// ID do template usado (opcional)
        /// </summary>
        [JsonPropertyName("template_id")]
        public string? TemplateId { get; set; }
    }
}
