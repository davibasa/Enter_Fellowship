using Enter_Extractor_Api.Models;
using System.Text.RegularExpressions;

namespace Enter_Extractor_Api.Services
{

    public interface IHeuristicExtractor
    {
        HeuristicResult TryExtract(string label, Dictionary<string, string> schema, string text);
    }

    public class HeuristicExtractor : IHeuristicExtractor
    {
        private readonly ITemplateStore _templateStore;
        private readonly ILogger<HeuristicExtractor> _logger;

        public HeuristicExtractor(ITemplateStore templateStore, ILogger<HeuristicExtractor> logger)
        {
            _templateStore = templateStore;
            _logger = logger;
        }

        public HeuristicResult TryExtract(string label, Dictionary<string, string> schema, string text)
        {
            var template = _templateStore.GetTemplate(label);
            if (template == null)
            {
                return new HeuristicResult { Data = new(), Confidence = 0 };
            }

            var result = new Dictionary<string, object?>();
            var fieldConfidences = new List<double>();

            foreach (var field in schema.Keys)
            {
                if (template.FieldPatterns.TryGetValue(field, out var pattern))
                {
                    var (value, confidence) = ExtractFieldValue(text, pattern, field);
                    result[field] = value;
                    fieldConfidences.Add(confidence);

                    _logger.LogDebug("Field: {Field}, Value: {Value}, Confidence: {Confidence}",
                        field, value, confidence);
                }
                else
                {
                    result[field] = null;
                    fieldConfidences.Add(0);
                }
            }

            var overallConfidence = fieldConfidences.Any() ? fieldConfidences.Average() : 0;

            return new HeuristicResult
            {
                Data = result,
                Confidence = overallConfidence
            };
        }

        private (object? value, double confidence) ExtractFieldValue(
            string text,
            FieldPattern pattern,
            string fieldName)
        {
            // Try regex patterns first
            foreach (var regexPattern in pattern.RegexPatterns)
            {
                try
                {
                    var match = Regex.Match(text, regexPattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var value = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : match.Value.Trim();
                        return (value, 0.95);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Regex pattern failed: {Pattern}", regexPattern);
                }
            }

            // Try keyword-based extraction
            foreach (var keyword in pattern.KeywordMarkers)
            {
                var value = ExtractValueAfterKeyword(text, keyword);
                if (value != null)
                {
                    return (value, 0.85);
                }
            }

            // Try common patterns based on field name
            var commonPatternValue = TryCommonPatterns(text, fieldName);
            if (commonPatternValue != null)
            {
                return (commonPatternValue, 0.80);
            }

            return (null, 0);
        }

        private string? ExtractValueAfterKeyword(string text, string keyword)
        {
            var index = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index == -1) return null;

            var afterKeyword = text.Substring(index + keyword.Length).Trim();

            // Extract until next line break or punctuation
            var match = Regex.Match(afterKeyword, @"^[:\s]*([^\n\r;,]+)");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            return null;
        }

        private string? TryCommonPatterns(string text, string fieldName)
        {
            var lowerFieldName = fieldName.ToLower();

            // CPF pattern
            if (lowerFieldName.Contains("cpf"))
            {
                var match = Regex.Match(text, @"\d{3}\.?\d{3}\.?\d{3}-?\d{2}");
                if (match.Success) return match.Value;
            }

            // CNPJ pattern
            if (lowerFieldName.Contains("cnpj"))
            {
                var match = Regex.Match(text, @"\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}");
                if (match.Success) return match.Value;
            }

            // Phone pattern
            if (lowerFieldName.Contains("telefone") || lowerFieldName.Contains("phone") || lowerFieldName.Contains("celular"))
            {
                var match = Regex.Match(text, @"\(?\d{2}\)?\s?\d{4,5}-?\d{4}");
                if (match.Success) return match.Value;
            }

            // CEP pattern
            if (lowerFieldName.Contains("cep"))
            {
                var match = Regex.Match(text, @"\d{5}-?\d{3}");
                if (match.Success) return match.Value;
            }

            // Date pattern
            if (lowerFieldName.Contains("data") || lowerFieldName.Contains("date") || lowerFieldName.Contains("nascimento"))
            {
                var match = Regex.Match(text, @"\d{2}/\d{2}/\d{4}");
                if (match.Success) return match.Value;
            }

            // Email pattern
            if (lowerFieldName.Contains("email") || lowerFieldName.Contains("e-mail"))
            {
                var match = Regex.Match(text, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
                if (match.Success) return match.Value;
            }

            // Number/Inscription patterns
            if (lowerFieldName.Contains("numero") || lowerFieldName.Contains("inscricao") || lowerFieldName.Contains("number"))
            {
                var match = Regex.Match(text, @"\b\d{4,}\b");
                if (match.Success) return match.Value;
            }

            return null;
        }
    }
}