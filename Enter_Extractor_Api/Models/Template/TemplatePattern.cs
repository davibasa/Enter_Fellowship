using Enter_Extractor_Api.Models.Extractor;

namespace Enter_Extractor_Api.Models.Template
{
    public class TemplatePattern
    {
        public string Label { get; set; } = string.Empty;
        public Dictionary<string, FieldPattern> FieldPatterns { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public int DocumentCount { get; set; }
    }
}
