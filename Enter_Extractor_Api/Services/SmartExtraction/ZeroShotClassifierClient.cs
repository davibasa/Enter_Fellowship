using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Services.SmartExtraction;

public interface IZeroShotClassifier
{
    Task<float> ClassifyAsync(string premise, string hypothesis);
    Task<Dictionary<string, float>> ClassifyMultipleAsync(string premise, params string[] hypotheses);
    Task<ClassificationResult> ClassifyBestMatchAsync(string text, params string[] candidateLabels);
    Task<BinaryClassificationResult> IsCategoryOfAsync(string text, string category);
    bool IsReady { get; }
}

public class ZeroShotClassifierClient : IZeroShotClassifier
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ZeroShotClassifierClient> _logger;
    private readonly string _baseUrl;

    public bool IsReady => IsHealthyAsync().GetAwaiter().GetResult();

    public ZeroShotClassifierClient(
        HttpClient httpClient,
        ILogger<ZeroShotClassifierClient> logger,
        IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = config["ZeroShot:PythonApiUrl"] ?? "http://localhost:5000";
        
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        _logger.LogInformation("ZeroShotClassifierClient configurado para: {BaseUrl}", _baseUrl);
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar health da API Python");
            return false;
        }
    }

    // Implementação do método antigo para compatibilidade
    public async Task<float> ClassifyAsync(string premise, string hypothesis)
    {
        // Usa validação binária
        var result = await IsCategoryOfAsync(premise, hypothesis);
        return result.Confidence;
    }

    // Implementação do método antigo para compatibilidade
    public async Task<Dictionary<string, float>> ClassifyMultipleAsync(string premise, params string[] hypotheses)
    {
        var result = await ClassifyBestMatchAsync(premise, hypotheses);
        return result.AllScores;
    }

    public async Task<ClassificationResult> ClassifyBestMatchAsync(string text, params string[] candidateLabels)
    {
        try
        {
            _logger.LogInformation("🔍 Classificando texto: '{Text}' com {Count} labels",
                text.Substring(0, Math.Min(50, text.Length)), candidateLabels.Length);

            var request = new ZeroShotRequest
            {
                Text = text,
                CandidateLabels = candidateLabels.ToList(),
                HypothesisTemplate = "Este texto é sobre {}",
                MultiLabel = false
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/zero-shot/classify", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Erro na classificação: {StatusCode} - {Error}",
                    response.StatusCode, error);
                throw new HttpRequestException($"Erro ao classificar: {response.StatusCode}");
            }

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ZeroShotResponse>(resultJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                throw new InvalidOperationException("Resposta inválida da API Python");
            }

            // Converte para o formato esperado
            var allScores = new Dictionary<string, float>();
            for (int i = 0; i < result.Labels.Count; i++)
            {
                allScores[result.Labels[i]] = result.Scores[i];
            }

            _logger.LogInformation("✅ Classificado como: {Label} (confiança: {Score:P1})",
                result.BestLabel, result.BestScore);

            return new ClassificationResult
            {
                Text = result.Text,
                PredictedLabel = result.BestLabel,
                Confidence = result.BestScore,
                AllScores = allScores
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao classificar texto");
            throw;
        }
    }

    public async Task<BinaryClassificationResult> IsCategoryOfAsync(string text, string category)
    {
        try
        {
            _logger.LogInformation("✓ Validando se '{Text}' é '{Category}'",
                text.Substring(0, Math.Min(50, text.Length)), category);

            var request = new BinaryClassificationRequest
            {
                Text = text,
                Category = category,
                HypothesisTemplate = "Este texto é {}"
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/zero-shot/validate", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Erro na validação: {StatusCode} - {Error}",
                    response.StatusCode, error);
                throw new HttpRequestException($"Erro ao validar: {response.StatusCode}");
            }

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<BinaryClassificationResponse>(resultJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                throw new InvalidOperationException("Resposta inválida da API Python");
            }

            _logger.LogInformation("{Emoji} '{Text}' é '{Category}': {Result} (confiança: {Score:P1})",
                result.IsCategory ? "✅" : "❌",
                text.Substring(0, Math.Min(30, text.Length)),
                category,
                result.IsCategory ? "SIM" : "NÃO",
                result.Confidence);

            return new BinaryClassificationResult
            {
                Text = result.Text,
                Category = result.Category,
                IsCategory = result.IsCategory,
                Confidence = result.Confidence
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar categoria");
            throw;
        }
    }
}

// DTOs para comunicação com a API Python
internal class ZeroShotRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("candidate_labels")]
    public List<string> CandidateLabels { get; set; } = new();

    [JsonPropertyName("hypothesis_template")]
    public string HypothesisTemplate { get; set; } = "Este texto é sobre {}";

    [JsonPropertyName("multi_label")]
    public bool MultiLabel { get; set; } = false;
}

internal class ZeroShotResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = new();

    [JsonPropertyName("scores")]
    public List<float> Scores { get; set; } = new();

    [JsonPropertyName("best_label")]
    public string BestLabel { get; set; } = string.Empty;

    [JsonPropertyName("best_score")]
    public float BestScore { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

internal class BinaryClassificationRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("hypothesis_template")]
    public string HypothesisTemplate { get; set; } = "Este texto é {}";
}

internal class BinaryClassificationResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("is_category")]
    public bool IsCategory { get; set; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}


public class ClassificationResult
{
    public string Text { get; set; } = string.Empty;
    public string PredictedLabel { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public Dictionary<string, float> AllScores { get; set; } = new();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Texto: \"{Text}\"");
        sb.AppendLine($"Classificação: {PredictedLabel} (confiança: {Confidence:P1})");
        sb.AppendLine("Todos os scores:");
        foreach (var score in AllScores.OrderByDescending(x => x.Value))
        {
            sb.AppendLine($"  {score.Key}: {score.Value:P1}");
        }
        return sb.ToString();
    }
}


public class BinaryClassificationResult
{
    public string Text { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsCategory { get; set; }
    public float Confidence { get; set; }

    public override string ToString()
    {
        var emoji = IsCategory ? "✓" : "✗";
        return $"{emoji} \"{Text}\" é '{Category}'? {(IsCategory ? "SIM" : "NÃO")} (confiança: {Confidence:P1})";
    }
}
