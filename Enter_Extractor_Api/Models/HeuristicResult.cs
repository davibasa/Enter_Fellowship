namespace Enter_Extractor_Api.Models
{
    public class HeuristicResult
    {
        public Dictionary<string, object?> Data { get; set; } = new();
        public double Confidence { get; set; }
    }
}
