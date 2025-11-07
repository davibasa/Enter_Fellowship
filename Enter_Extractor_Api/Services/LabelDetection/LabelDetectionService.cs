using Enter_Extractor_Api.Models.Redis;
using Enter_Extractor_Api.Services.Redis;
using Enter_Extractor_Api.Services.SmartExtraction;
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;

namespace Enter_Extractor_Api.Services.LabelDetection;

/// <summary>
/// Implementação do serviço de detecção de labels usando RoBERTa/Embeddings + Redis
/// </summary>
public class LabelDetectionService : ILabelDetectionService
{
    private readonly IPythonExtractorClient _pythonClient;
    private readonly IRedisPersistenceService _redisPersistence;
    private readonly ILogger<LabelDetectionService> _logger;

    public LabelDetectionService(
        IPythonExtractorClient pythonClient,
        IRedisPersistenceService redisPersistence,
        ILogger<LabelDetectionService> logger)
    {
        _pythonClient = pythonClient;
        _redisPersistence = redisPersistence;
        _logger = logger;
    }

    public async Task<DetectedLabelsDto> DetectLabelsAsync(
        string label,
        string pdfHash,
        Dictionary<string, string> schema,
        string schemaHash,
        string text,
        int topK = 3,
        int minTokenLength = 3,
        float similarityThreshold = 0.5f,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "🔍 Detectando labels | Label: {Label} | PdfHash: {PdfHash} | SchemaHash: {SchemaHash} | Campos: {FieldCount}",
                label,
                pdfHash[..8],
                schemaHash[..8],
                schema.Count);

            // 1. Tentar buscar do cache
            var cached = await _redisPersistence.GetDetectedLabelsAsync(label, pdfHash, schemaHash, cancellationToken);
            if (cached != null)
            {
                _logger.LogInformation("⚡ Cache hit: Labels detectadas | {DetectedCount} labels",
                    cached.DetectedLabels.Count);
                return cached;
            }

            // 2. Chamar Python API para detecção
            var pythonResponse = await _pythonClient.SemanticLabelDetectAsync(
                labels: schema,
                text: text,
                topK: topK,
                minTokenLength: minTokenLength,
                similarityThreshold: similarityThreshold,
                cancellationToken: cancellationToken);

            // 3. Converter resposta Python para DTO
            var detectedLabelsDto = new DetectedLabelsDto
            {
                PdfHash = pdfHash,
                Label = label,
                Schema = schema,
                SchemaHash = schemaHash,
                DetectedLabels = pythonResponse.DetectedLabels.Select(d => new DetectedLabelMatch
                {
                    CandidateText = d.CandidateText,
                    MatchedLabel = d.MatchedLabel,
                    Score = d.Score,
                    Rank = d.Rank
                }).ToList(),
                TotalCandidates = pythonResponse.TotalCandidates,
                ModelUsed = pythonResponse.ModelUsed,
                ProcessingTimeMs = pythonResponse.ProcessingTimeMs,
                DetectedAt = DateTime.UtcNow
            };

            // 4. Salvar no cache Redis
            await _redisPersistence.SaveDetectedLabelsAsync(detectedLabelsDto, cancellationToken);

            _logger.LogInformation(
                "✅ Labels detectadas: {Count} labels | Tempo: {Ms}ms | Modelo: {Model}",
                detectedLabelsDto.DetectedLabels.Count,
                pythonResponse.ProcessingTimeMs,
                pythonResponse.ModelUsed);

            return detectedLabelsDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao detectar labels para {Label}", label);
            throw;
        }
    }

    public string RemoveLabelsFromText(string text, DetectedLabelsDto detectedLabels)
    {
        if (string.IsNullOrWhiteSpace(text) || detectedLabels.DetectedLabels.Count == 0)
        {
            return text;
        }

        try
        {
            _logger.LogInformation(
                "🗑️ Removendo labels do texto | Labels: {Count}",
                detectedLabels.DetectedLabels.Count);

            var normalizedText = text;

            // Ordenar labels por tamanho (maiores primeiro) para evitar remoções parciais
            var sortedLabels = detectedLabels.DetectedLabels
                .OrderByDescending(l => l.CandidateText.Length)
                .ToList();

            // Remover cada label detectada
            foreach (var labelMatch in sortedLabels)
            {
                var labelText = labelMatch.CandidateText;

                // Normalizar para comparação case-insensitive
                var normalizedLabel = RemoveAccents(labelText);

                // Criar padrão regex para remover (com boundaries para não remover parcialmente)
                // Exemplo: "Nome Completo:" → removido
                var pattern = $@"\b{Regex.Escape(normalizedLabel)}\b";

                normalizedText = Regex.Replace(
                    normalizedText,
                    pattern,
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.Compiled);

                _logger.LogDebug(
                    "  ➖ Removida label: '{Label}' (score: {Score:F2}) → campo '{Field}'",
                    labelText,
                    labelMatch.Score,
                    labelMatch.MatchedLabel);
            }

            // Limpeza final: remover espaços múltiplos e linhas vazias
            normalizedText = Regex.Replace(normalizedText, @"\s+", " ", RegexOptions.Compiled);
            normalizedText = Regex.Replace(normalizedText, @"^\s*$\n", string.Empty, RegexOptions.Multiline | RegexOptions.Compiled);
            normalizedText = normalizedText.Trim();

            _logger.LogInformation(
                "✅ Labels removidas | Antes: {Before} chars → Depois: {After} chars",
                text.Length,
                normalizedText.Length);

            return normalizedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao remover labels do texto");
            return text; // Retornar texto original em caso de erro
        }
    }

    /// <summary>
    /// Remove acentos de uma string usando normalização Unicode
    /// </summary>
    private static string RemoveAccents(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}
