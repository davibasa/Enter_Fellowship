using Enter_Extractor_Api.Services.Proximity.Models;
using Enter_Extractor_Api.Services.SmartExtraction;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Enter_Extractor_Api.Services.Proximity;

/// <summary>
/// Implementação de detecção de labels usando estratégias hierárquicas:
/// 1. Exact Match (rápido)
/// 2. Normalized Match (remove acentos/pontuação)
/// 3. Fuzzy Match (Levenshtein distance)
/// 4. Semantic Match (Python API - embeddings)
/// </summary>
public class LabelDetectorProximity : ILabelDetectorProximity
{
    private readonly IPythonExtractorClient _pythonClient;
    private readonly ILogger<LabelDetectorProximity> _logger;

    public LabelDetectorProximity(IPythonExtractorClient pythonClient, ILogger<LabelDetectorProximity> logger)
    {
        _pythonClient = pythonClient;
        _logger = logger;
    }

    public async Task<List<DetectedLabel>> DetectLabelsAsync(
        Dictionary<string, string> schema,
        string[] lines,
        LabelDetectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new LabelDetectionOptions();
        var results = new List<DetectedLabel>();

        // Processar cada campo do schema
        foreach (var (fieldName, fieldDescription) in schema)
        {
            var detections = await DetectLabelAsync(fieldName, fieldDescription, lines, options, cancellationToken);
            results.AddRange(detections);
        }

        // Ordenar por linha e posição
        return results.OrderBy(r => r.LineIndex).ThenBy(r => r.CharStartIndex).ToList();
    }

    public async Task<List<DetectedLabel>> DetectLabelAsync(
        string fieldName,
        string fieldDescription,
        string[] lines,
        LabelDetectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new LabelDetectionOptions();
        var results = new List<DetectedLabel>();

        // Validar tamanho mínimo
        if (fieldName.Length < options.MinLabelLength)
        {
            _logger.LogDebug("Campo {FieldName} muito curto para detecção (mín: {MinLength})",
                fieldName, options.MinLabelLength);
            return results;
        }

        // Estratégia 1: Exact Match
        var exactMatches = DetectExactMatch(fieldName, lines, options);
        if (exactMatches.Any())
        {
            _logger.LogInformation("Detectado {Count} exact matches para campo {FieldName}",
                exactMatches.Count, fieldName);
            results.AddRange(exactMatches);
        }

        // Estratégia 2: Normalized Match (se não encontrou exact)
        if (!results.Any())
        {
            var normalizedMatches = DetectNormalizedMatch(fieldName, lines, options);
            if (normalizedMatches.Any())
            {
                _logger.LogInformation("Detectado {Count} normalized matches para campo {FieldName}",
                    normalizedMatches.Count, fieldName);
                results.AddRange(normalizedMatches);
            }
        }

        // Estratégia 3: Fuzzy Match (se habilitado e não encontrou ainda)
        if (!results.Any() && options.EnableFuzzyMatching)
        {
            var fuzzyMatches = DetectFuzzyMatch(fieldName, lines, options);
            if (fuzzyMatches.Any())
            {
                _logger.LogInformation("Detectado {Count} fuzzy matches para campo {FieldName}",
                    fuzzyMatches.Count, fieldName);
                results.AddRange(fuzzyMatches);
            }
        }

        // Estratégia 4: Semantic Match (se habilitado e não encontrou ainda)
        if (!results.Any() && options.EnableSemanticDetection)
        {
            try
            {
                var semanticMatches = await DetectSemanticMatchAsync(
                    fieldName, fieldDescription, lines, options, cancellationToken);
                if (semanticMatches.Any())
                {
                    _logger.LogInformation("Detectado {Count} semantic matches para campo {FieldName}",
                        semanticMatches.Count, fieldName);
                    results.AddRange(semanticMatches);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao tentar detecção semântica para campo {FieldName}", fieldName);
            }
        }

        return results;
    }

    /// <summary>
    /// Estratégia 1: Busca exact match (case-insensitive)
    /// </summary>
    private List<DetectedLabel> DetectExactMatch(string fieldName, string[] lines, LabelDetectionOptions options)
    {
        var results = new List<DetectedLabel>();
        var pattern = options.RemovePunctuation
            ? Regex.Escape(RemovePunctuation(fieldName, options.PunctuationChars))
            : Regex.Escape(fieldName);

        var regex = new Regex($@"\b{pattern}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var searchLine = options.RemovePunctuation
                ? RemovePunctuation(line, options.PunctuationChars)
                : line;

            var matches = regex.Matches(searchLine);
            foreach (Match match in matches)
            {
                results.Add(new DetectedLabel
                {
                    FieldName = fieldName,
                    DetectedText = match.Value,
                    LineIndex = i,
                    CharStartIndex = match.Index,
                    CharEndIndex = match.Index + match.Length,
                    SimilarityScore = 1.0f,
                    Strategy = LabelDetectionStrategy.ExactMatch,
                    FullLine = line
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Estratégia 2: Busca normalized match (remove acentos e pontuação)
    /// </summary>
    private List<DetectedLabel> DetectNormalizedMatch(string fieldName, string[] lines, LabelDetectionOptions options)
    {
        var results = new List<DetectedLabel>();
        var normalizedFieldName = NormalizeString(fieldName, options);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var words = SplitIntoWords(line);

            foreach (var (word, startIndex) in words)
            {
                var normalizedWord = NormalizeString(word, options);

                if (normalizedWord.Equals(normalizedFieldName, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new DetectedLabel
                    {
                        FieldName = fieldName,
                        DetectedText = word,
                        LineIndex = i,
                        CharStartIndex = startIndex,
                        CharEndIndex = startIndex + word.Length,
                        SimilarityScore = 0.95f,
                        Strategy = LabelDetectionStrategy.NormalizedMatch,
                        FullLine = line
                    });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Estratégia 3: Busca fuzzy match (Levenshtein distance)
    /// </summary>
    private List<DetectedLabel> DetectFuzzyMatch(string fieldName, string[] lines, LabelDetectionOptions options)
    {
        var results = new List<DetectedLabel>();
        var normalizedFieldName = NormalizeString(fieldName, options);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var words = SplitIntoWords(line);

            foreach (var (word, startIndex) in words)
            {
                // Ignorar palavras muito diferentes em tamanho
                if (Math.Abs(word.Length - fieldName.Length) > fieldName.Length * 0.3)
                    continue;

                var normalizedWord = NormalizeString(word, options);
                var similarity = CalculateSimilarity(normalizedFieldName, normalizedWord);

                if (similarity >= options.FuzzyMatchThreshold)
                {
                    results.Add(new DetectedLabel
                    {
                        FieldName = fieldName,
                        DetectedText = word,
                        LineIndex = i,
                        CharStartIndex = startIndex,
                        CharEndIndex = startIndex + word.Length,
                        SimilarityScore = similarity,
                        Strategy = LabelDetectionStrategy.FuzzyMatch,
                        FullLine = line
                    });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Estratégia 4: Busca semantic match usando Python API
    /// </summary>
    private async Task<List<DetectedLabel>> DetectSemanticMatchAsync(
        string fieldName,
        string fieldDescription,
        string[] lines,
        LabelDetectionOptions options,
        CancellationToken cancellationToken)
    {
        var results = new List<DetectedLabel>();

        // Preparar labels para busca semântica
        var labels = new Dictionary<string, string>
        {
            { fieldName, fieldDescription }
        };

        // Juntar linhas em texto único
        var text = string.Join("\n", lines);

        // Chamar API Python
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(options.PythonApiTimeoutMs);

        var response = await _pythonClient.SemanticLabelDetectAsync(
            labels: labels,
            text: text,
            topK: 5,
            minTokenLength: options.MinLabelLength,
            similarityThreshold: options.SemanticMatchThreshold,
            cancellationToken: cts.Token
        );

        // Processar resultados
        if (response.DetectedLabels != null)
        {
            foreach (var detection in response.DetectedLabels)
            {
                if (detection.FieldName != fieldName)
                    continue;

                // Encontrar a linha correspondente
                var lineIndex = detection.LineIndex;
                if (lineIndex >= 0 && lineIndex < lines.Length)
                {
                    var line = lines[lineIndex];
                    var charStart = detection.CharStartIndex;
                    var charEnd = detection.CharEndIndex;

                    // Validar índices
                    if (charStart >= 0 && charEnd <= line.Length && charStart < charEnd)
                    {
                        var detectedText = line.Substring(charStart, charEnd - charStart);

                        results.Add(new DetectedLabel
                        {
                            FieldName = fieldName,
                            DetectedText = detectedText,
                            LineIndex = lineIndex,
                            CharStartIndex = charStart,
                            CharEndIndex = charEnd,
                            SimilarityScore = detection.Score,
                            Strategy = LabelDetectionStrategy.SemanticMatch,
                            FullLine = line
                        });
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Normaliza string removendo acentos e opcionalmente pontuação
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string NormalizeString(string text, LabelDetectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Remover acentos
        var normalized = RemoveAccents(text);

        // Remover pontuação se habilitado
        if (options.RemovePunctuation)
        {
            normalized = RemovePunctuation(normalized, options.PunctuationChars);
        }

        return normalized.Trim();
    }

    /// <summary>
    /// Remove acentos de uma string
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string RemoveAccents(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Remove caracteres de pontuação
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string RemovePunctuation(string text, char[] punctuationChars)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!punctuationChars.Contains(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Divide texto em palavras com suas posições originais
    /// </summary>
    private static List<(string word, int startIndex)> SplitIntoWords(string text)
    {
        var results = new List<(string, int)>();
        var regex = new Regex(@"\b[\w'-]+\b", RegexOptions.Compiled);
        var matches = regex.Matches(text);

        foreach (Match match in matches)
        {
            results.Add((match.Value, match.Index));
        }

        return results;
    }

    /// <summary>
    /// Calcula similaridade entre duas strings usando Levenshtein distance
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
            return 1.0f;

        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0.0f;

        var distance = LevenshteinDistance(s1.ToLowerInvariant(), s2.ToLowerInvariant());
        var maxLength = Math.Max(s1.Length, s2.Length);

        return 1.0f - ((float)distance / maxLength);
    }

    /// <summary>
    /// Calcula a distância de Levenshtein entre duas strings
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static int LevenshteinDistance(string s1, string s2)
    {
        var len1 = s1.Length;
        var len2 = s2.Length;

        // Otimização: usar apenas duas linhas da matriz
        var previousRow = new int[len2 + 1];
        var currentRow = new int[len2 + 1];

        // Inicializar primeira linha
        for (int j = 0; j <= len2; j++)
            previousRow[j] = j;

        for (int i = 1; i <= len1; i++)
        {
            currentRow[0] = i;

            for (int j = 1; j <= len2; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                currentRow[j] = Math.Min(
                    Math.Min(currentRow[j - 1] + 1, previousRow[j] + 1),
                    previousRow[j - 1] + cost
                );
            }

            // Trocar linhas
            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[len2];
    }
}
