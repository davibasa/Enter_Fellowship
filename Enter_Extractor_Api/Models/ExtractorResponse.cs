using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models
{
    public class ExtractorResponse
    {
        [JsonPropertyName("schema")]
        public Dictionary<string, object?> Schema { get; set; } = new();
    }
}
