using Enter_Extractor_Api.Models;
using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Enter_Extractor_Api.Services
{

    public interface IPdfTextExtractor
    {
        ValueTask<string> ExtractTextAsync(byte[] pdfBytes);
    }

    public class PdfTextExtractor : IPdfTextExtractor
    {
        private readonly ILogger<PdfTextExtractor> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _pythonApiUrl;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public PdfTextExtractor(ILogger<PdfTextExtractor> logger, HttpClient httpClient, IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            _pythonApiUrl = configuration["PythonApi:BaseUrl"] ?? "http://pdf-extractor:5000";
        }

        public async ValueTask<string> ExtractTextAsync(byte[] pdfBytes)
        {
            try
            {
                var pdfBase64 = Convert.ToBase64String(pdfBytes);

                var requestPayload = new
                {
                    pdf_base64 = pdfBase64
                };

                var jsonContent = JsonSerializer.Serialize(requestPayload, _jsonOptions);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Chamar a API Python com ConfigureAwait(false) para melhor performance
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_pythonApiUrl}/extract-text")
                {
                    Content = httpContent
                };

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException($"Failed to extract text from PDF. API returned {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var extractionResult = JsonSerializer.Deserialize<PdfExtractionResponse>(responseContent, _jsonOptions);

                if (extractionResult == null || !extractionResult.Success)
                {
                    throw new InvalidOperationException("Failed to parse extraction result from Python API");
                }

                _logger.LogInformation("Successfully extracted {CharCount} characters from PDF via Python API", extractionResult.CharCount);

                return extractionResult.Text;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling Python API at {Url}", _pythonApiUrl);
                throw new InvalidOperationException($"Failed to connect to PDF extraction service at {_pythonApiUrl}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF");
                throw new InvalidOperationException("Failed to extract text from PDF", ex);
            }
        }
    }
}
