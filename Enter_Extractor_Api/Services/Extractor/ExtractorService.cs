using Enter_Extractor_Api.Models.SmartExtraction;
using Enter_Extractor_Api.Services.SmartExtraction;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Enter_Extractor_Api.Models.Extractor;

namespace Enter_Extractor_Api.Services.Extractor
{
    public class ExtractorService : IExtractorService
    {
        private readonly IPdfTextExtractor _pdfExtractor;
        private readonly ILogger<ExtractorService> _logger;
        private readonly IFieldTypeClassifier _fieldTypeClassifier;
        private readonly ITokenExtractor _tokenExtractor;
        private readonly IPythonExtractorClient _pythonClient;

        public ExtractorService(ILogger<ExtractorService> logger, IPdfTextExtractor pdfExtractor,
            IFieldTypeClassifier fieldTypeClassifier, ITokenExtractor tokenExtractor,
            IEnumParser enumParser, IPythonExtractorClient pythonClient)
        {
            _logger = logger;
            _pdfExtractor = pdfExtractor;
            _fieldTypeClassifier = fieldTypeClassifier;
            _tokenExtractor = tokenExtractor;
            _pythonClient = pythonClient;
        }

        public async Task<ExtractorResponse> ExtractAsync(string label, Dictionary<string, string> schema, string pdfBase64)
        {
            try
            {
                var lines = pdfBase64.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var availableLines = lines.Select((line, index) => (line, originalIndex: index)).ToList();

                var result = new ExtractorResponse
                {
                    Schema = new Dictionary<string, object>(schema.Count)!
                };

                var (enumRegexFields, simpleMultiLineFields) = schema
                    .Select((kvp, index) => (
                        fieldName: kvp.Key,
                        description: kvp.Value,
                        type: _fieldTypeClassifier.ClassifyField(kvp.Key, kvp.Value),
                        originalOrder: index
                    ))
                    .OrderBy(f => GetFieldTypePriority(f.type))
                    .ThenBy(f => f.originalOrder)
                    .Aggregate(
                        (
                            enumRegex: new List<(string fieldName, string description, FieldType type, int originalOrder)>(schema.Count),
                            simpleMultiLine: new List<(string fieldName, string description, FieldType type, int originalOrder)>(schema.Count)
                        ),
                        (acc, field) =>
                        {
                            if (GetFieldTypePriority(field.type) <= 2)
                                acc.enumRegex.Add(field);
                            else
                                acc.simpleMultiLine.Add(field);
                            return acc;
                        }
                    );

                var keywordsToRemove = simpleMultiLineFields.Count > 0
                    ? simpleMultiLineFields
                        .SelectMany(field => new[] { field.fieldName }
                            .Concat(field.description.Split(new[] { ' ', ',', ';', ':', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(w => w.Length >= 3)))
                        .Distinct()
                        .ToList()
                    : new List<string>(0);

                var linesToRemove = new HashSet<int>();

                foreach (var field in enumRegexFields)
                {
                    FieldExtractionResult? extracted = null;

                    try
                    {
                        var task = field.type switch
                        {
                            FieldType.Enum => _tokenExtractor.ExtractEnumFieldAsync(availableLines, field.fieldName, field.description),
                            FieldType.Date or FieldType.Currency or FieldType.Percentage or FieldType.Phone or
                            FieldType.CPF or FieldType.CNPJ or FieldType.Email or FieldType.CEP or FieldType.Number
                                => _tokenExtractor.ExtractRegexFieldAsync(availableLines, field.fieldName, field.type),
                            _ => null
                        };

                        if (task.HasValue)
                            extracted = await task.Value;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao extrair campo '{FieldName}'", field.fieldName);
                    }

                    if (extracted != null)
                    {
                        result.Schema[field.fieldName] = extracted!.Value;
                        var extractedValue = extracted.Value?.ToString() ?? string.Empty;

                        if (!string.IsNullOrWhiteSpace(extractedValue))
                        {
                            for (int i = availableLines.Count - 1; i >= 0; i--)
                            {
                                if (linesToRemove.Contains(i))
                                    continue;

                                var lineData = availableLines[i];
                                var currentLine = lineData.line;

                                var indexOfValue = currentLine.IndexOf(extractedValue, StringComparison.OrdinalIgnoreCase);
                                if (indexOfValue >= 0)
                                {
                                    var sbLine = new StringBuilder(currentLine.Length);
                                    sbLine.Append(currentLine.AsSpan(0, indexOfValue));
                                    sbLine.Append(currentLine.AsSpan(indexOfValue + extractedValue.Length));
                                    currentLine = sbLine.ToString().Trim();

                                    if (string.IsNullOrWhiteSpace(currentLine))
                                    {
                                        linesToRemove.Add(i);
                                    }
                                    else
                                    {
                                        availableLines[i] = (currentLine, lineData.originalIndex);
                                    }

                                    break; 
                                }
                            }
                        }
                    }
                    else
                    {
                        result.Schema[field.fieldName] = null;
                    }
                }

                foreach (var index in linesToRemove.OrderByDescending(x => x))
                {
                    availableLines.RemoveAt(index);
                }

                if (keywordsToRemove.Count > 0)
                {
                    RemoveKeywordsFromAllLines(availableLines, keywordsToRemove);
                }

                var pendingSchema = new Dictionary<string, string>(simpleMultiLineFields.Count);
                foreach (var field in simpleMultiLineFields)
                {
                    pendingSchema[field.fieldName] = field.description;
                }

                var sb = new StringBuilder(availableLines.Sum(l => l.line.Length + 1));
                for (int i = 0; i < availableLines.Count; i++)
                {
                    sb.Append(availableLines[i].line);
                    if (i < availableLines.Count - 1)
                        sb.Append('\n');
                }
                var cleanedText = sb.ToString();

                var smartResult = await _pythonClient.EmbeddingsPythonAsync(
                    label: null,
                    text: cleanedText,
                    schema: pendingSchema,
                    confidenceThreshold: 0.7f,
                    enableGptFallback: true
                );

                var finalResult = new ExtractorResponse
                {
                    Schema = new Dictionary<string, object?>(schema.Count)
                };

                foreach (var kvp in schema)
                {
                    finalResult.Schema[kvp.Key] = result.Schema.TryGetValue(kvp.Key, out var regexValue)
                        ? regexValue
                        : smartResult.Fields.TryGetValue(kvp.Key, out var semanticValue)
                            ? semanticValue
                            : null;
                }

                return finalResult;

            }
            catch (Exception)
            {

                throw;
            }
        }

        private async Task RemoveLabelsClientAsync(
            List<(string line, int originalIndex)> availableLines,
            string[] pendingFieldNames)
        {
            try
            {
                var schema = pendingFieldNames.ToDictionary(
                    name => name,
                    name => $"Campo {name}"
                );

                var textBlocks = availableLines.Select(l => l.line).ToList();

                var result = await _pythonClient!.ClassifyNliAsync(
                    label: "label_removal",
                    schema: schema,
                    textBlocks: textBlocks
                );

                var linesToRemove = new HashSet<string>(result.LabelsDetected);
                int removedCount = 0;

                for (int i = availableLines.Count - 1; i >= 0; i--)
                {
                    if (linesToRemove.Contains(availableLines[i].line))
                    {
                        availableLines.RemoveAt(i);
                        removedCount++;
                    }
                }

            }
            catch (Exception)
            {
                throw;
            }
        }

        private static int GetFieldTypePriority(FieldType fieldType)
        {
            return fieldType switch
            {
                // Prioridade 1: Enums (valores fixos mais confiáveis)
                FieldType.Enum => 1,

                // Prioridade 2: Padrões regex específicos (alta precisão)
                FieldType.Regex => 2,
                FieldType.CPF => 2,
                FieldType.CNPJ => 2,
                FieldType.Date => 2,
                FieldType.Phone => 2,
                FieldType.Email => 2,
                FieldType.CEP => 2,
                FieldType.Currency => 2,
                FieldType.Percentage => 2,
                FieldType.Number => 2,

                // Prioridade 3: Campos simples genéricos
                FieldType.Simple => 3,

                // Prioridade 4: Campos multi-linha (menos precisos, capturam múltiplas linhas)
                FieldType.MultiLine => 4,

                _ => 99
            };
        }

        /// <summary>
        /// Remove palavras-chave de todas as linhas de uma só vez.
        /// Complexidade: O(m * k) onde m = número de linhas, k = número de keywords.
        /// Muito mais eficiente que chamar RemoveKeywordsOptimized dentro de um loop.
        /// </summary>
        private static void RemoveKeywordsFromAllLines(
            List<(string line, int originalIndex)> lines,
            List<string> keywords)
        {
            if (keywords == null || keywords.Count == 0)
                return;

            var normalizedKeywords = keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => RemoveAccents(k.ToLowerInvariant()))
                .OrderByDescending(k => k.Length)
                .Distinct()
                .ToList();

            if (normalizedKeywords.Count == 0)
                return;

            var linesToRemove = new HashSet<int>();

            for (int i = 0; i < lines.Count; i++)
            {
                var (line, originalIndex) = lines[i];

                var normalizedLine = RemoveAccents(line.ToLowerInvariant());

                foreach (var keyword in normalizedKeywords)
                {
                    normalizedLine = normalizedLine.Replace(keyword, " ");
                }

                normalizedLine = Regex.Replace(normalizedLine, @"\s+", " ", RegexOptions.Compiled).Trim();

                if (string.IsNullOrWhiteSpace(normalizedLine))
                {
                    linesToRemove.Add(i);
                }
                else
                {
                    lines[i] = (normalizedLine, originalIndex);
                }
            }

            foreach (var index in linesToRemove.OrderByDescending(x => x))
            {
                lines.RemoveAt(index);
            }
        }

        /// <summary>
        /// Remove palavras-chave de um texto de forma otimizada.
        /// Tolerante a erros de digitação (1 caractere a mais ou a menos).
        /// Remove tanto palavras completas quanto substrings.
        /// Complexidade: O(m) onde m = tamanho do texto.
        /// </summary>
        /// <param name="text">Texto a ser limpo</param>
        /// <param name="keywords">Lista de palavras-chave para remover</param>
        /// <returns>Texto limpo sem as palavras-chave</returns>
        private static string RemoveKeywordsOptimized(string text, IEnumerable<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(text) || keywords == null || !keywords.Any())
                return text;

            var normalizedText = RemoveAccents(text.ToLowerInvariant());

            var sortedKeywords = keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => RemoveAccents(k.ToLowerInvariant()))
                .OrderByDescending(k => k.Length)
                .Distinct()
                .ToList();

            foreach (var keyword in sortedKeywords)
            {
                normalizedText = normalizedText.Replace(keyword, " ");

            }

            normalizedText = Regex.Replace(normalizedText, @"\s+", " ", RegexOptions.Compiled).Trim();

            return normalizedText;
        }

        /// <summary>
        /// Remove acentos de uma string usando normalização Unicode.
        /// Otimizado com StringBuilder.
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

        /// <summary>
        /// Gera padrão regex para palavras com 1 caractere faltando em qualquer posição.
        /// </summary>
        private static string GenerateMissingCharPattern(string word)
        {
            if (string.IsNullOrWhiteSpace(word) || word.Length <= 2)
                return string.Empty;

            var patterns = new List<string>();

            for (int i = 0; i < word.Length; i++)
            {
                var variant = word.Remove(i, 1);
                if (!string.IsNullOrWhiteSpace(variant))
                {
                    patterns.Add($@"\b{Regex.Escape(variant)}\b");
                }
            }

            return patterns.Any() ? $"({string.Join("|", patterns)})" : string.Empty;
        }
    }
}
