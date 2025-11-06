
using Enter_Extractor_Api.Models;

namespace Enter_Extractor_Api.Services
{
    public interface IExtractorService
    {
        Task<ExtractorResponse> ExtractAsync(string label, Dictionary<string, string> schema, string pdfBase64);
    }
}
