namespace Enter_Extractor_Api.Models
{
    public class FieldPattern
    {
        public List<string> RegexPatterns { get; set; } = new();
        public List<string> KeywordMarkers { get; set; } = new();
        public List<int> Positions { get; set; } = new();
        public int SampleCount { get; set; }
    }
}
