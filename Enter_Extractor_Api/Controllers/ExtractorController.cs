using Enter_Extractor_Api.Models;
using Enter_Extractor_Api.Services;
using Enter_Extractor_Api.Services.SmartExtraction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Enter_Extractor_Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExtractorController : ControllerBase
    {
        private readonly ILogger<ExtractorController> _logger;
        private readonly IExtractorService _extractorService;
        private readonly HttpClient _httpClient;
        private readonly string _pythonApiUrl;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ExtractorController(ILogger<ExtractorController> logger, IExtractorService extractorService,
            HttpClient httpClient, IConfiguration configuration)
        {
            _logger = logger;
            _extractorService = extractorService;
            _httpClient = httpClient;
            _pythonApiUrl = configuration["PythonApi:BaseUrl"] ?? "http://pdf-extractor:5000";
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResponse<ExtractorResponse>>> Extractor([FromBody] ExtractorRequest request)
        {
            var response = new ServiceResponse<ExtractorResponse>();
            try
            {
                // Extrair texto do PDF chamando a API Python diretamente
                var requestPayload = new
                {
                    pdf_base64 = request.PdfBase64
                };

                var jsonContent = JsonSerializer.Serialize(requestPayload, _jsonOptions);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_pythonApiUrl}/extract-text")
                {
                    Content = httpContent
                };

                var httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogError("Failed to extract text from PDF. API returned {StatusCode}: {Error}",
                        httpResponse.StatusCode, errorContent);
                    throw new InvalidOperationException($"Failed to extract text from PDF. API returned {httpResponse.StatusCode}");
                }

                var responseContent = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                var extractionResult = JsonSerializer.Deserialize<PdfExtractionResponse>(responseContent, _jsonOptions);

                if (extractionResult == null || !extractionResult.Success)
                {
                    throw new InvalidOperationException("Failed to parse extraction result from Python API");
                }

                // Processar extração com o texto extraído
                var result = await _extractorService.ExtractAsync(request.Label, request.ExtractionSchema, extractionResult.Text)
                    .ConfigureAwait(false);

                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling Python API at {Url}", _pythonApiUrl);
                return StatusCode(503, new ServiceResponse<ExtractorResponse>
                {
                    Success = false,
                    Message = $"Failed to connect to PDF extraction service: {ex.Message}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing extraction request");
                return StatusCode(500, new ServiceResponse<ExtractorResponse>
                {
                    Success = false,
                    Message = $"Internal error: {ex.Message}"
                });
            }
        }
    }
}
