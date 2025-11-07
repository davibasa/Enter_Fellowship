namespace Enter_Extractor_Api.Models
{
    public class ServiceResponse<T>
    {
        public T? Data { get; set; }
        public bool Success { get; set; } = true;
        public string? Message { get; set; } = "";
        public string? Details { get; set; } = "";
        public int? StatusCode { get; set; } = 200;
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
