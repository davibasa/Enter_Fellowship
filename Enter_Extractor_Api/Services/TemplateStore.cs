using Enter_Extractor_Api.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Enter_Extractor_Api.Services
{

    public interface ITemplateStore
    {
        bool HasTemplate(string label);
        TemplatePattern? GetTemplate(string label);
        void LearnPattern(string label, Dictionary<string, string> schema, string text, Dictionary<string, object?> extractedData);
    }

    public class TemplateStore : ITemplateStore
    {
        private readonly ConcurrentDictionary<string, TemplatePattern> _templates = new();
        private readonly ILogger<TemplateStore> _logger;

        public TemplateStore(ILogger<TemplateStore> logger)
        {
            _logger = logger;
        }

        public bool HasTemplate(string label)
        {
            return _templates.ContainsKey(label);
        }

        public TemplatePattern? GetTemplate(string label)
        {
            _templates.TryGetValue(label, out var template);
            return template;
        }

        public void LearnPattern(
            string label,
            Dictionary<string, string> schema,
            string text,
            Dictionary<string, object?> extractedData)
        {
            try
            {
                var template = _templates.GetOrAdd(label, _ => new TemplatePattern
                {
                    Label = label,
                    FieldPatterns = new Dictionary<string, FieldPattern>(),
                    LastUpdated = DateTime.UtcNow,
                    DocumentCount = 0
                });

                template.DocumentCount++;
                template.LastUpdated = DateTime.UtcNow;

                foreach (var kvp in extractedData)
                {
                    var fieldName = kvp.Key;
                    var fieldValue = kvp.Value?.ToString();

                    if (string.IsNullOrEmpty(fieldValue))
                        continue;

                    if (!template.FieldPatterns.ContainsKey(fieldName))
                    {
                        template.FieldPatterns[fieldName] = new FieldPattern();
                    }

                    var fieldPattern = template.FieldPatterns[fieldName];
                    fieldPattern.SampleCount++;

                    // Learn regex pattern for this value
                    var regexPattern = GenerateRegexPattern(fieldValue);
                    if (!string.IsNullOrEmpty(regexPattern) && !fieldPattern.RegexPatterns.Contains(regexPattern))
                    {
                        fieldPattern.RegexPatterns.Add(regexPattern);
                    }

                    // Find keyword markers (text that appears before the value)
                    var keywords = FindKeywordMarkers(text, fieldValue, fieldName);
                    foreach (var keyword in keywords)
                    {
                        if (!fieldPattern.KeywordMarkers.Contains(keyword))
                        {
                            fieldPattern.KeywordMarkers.Add(keyword);
                        }
                    }

                    // Store position information
                    var position = text.IndexOf(fieldValue, StringComparison.OrdinalIgnoreCase);
                    if (position >= 0)
                    {
                        fieldPattern.Positions.Add(position);
                    }
                }

                _logger.LogInformation(
                    "Learned pattern for label: {Label}, document count: {Count}",
                    label,
                    template.DocumentCount
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error learning pattern for label: {Label}", label);
            }
        }

        private string GenerateRegexPattern(string value)
        {
            // Generate a regex pattern based on the value structure

            // CPF: 123.456.789-01
            if (Regex.IsMatch(value, @"^\d{3}\.?\d{3}\.?\d{3}-?\d{2}$"))
                return @"\d{3}\.?\d{3}\.?\d{3}-?\d{2}";

            // CNPJ: 12.345.678/0001-90
            if (Regex.IsMatch(value, @"^\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}$"))
                return @"\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}";

            // Phone: (11) 98765-4321
            if (Regex.IsMatch(value, @"^\(?\d{2}\)?\s?\d{4,5}-?\d{4}$"))
                return @"\(?\d{2}\)?\s?\d{4,5}-?\d{4}";

            // CEP: 12345-678
            if (Regex.IsMatch(value, @"^\d{5}-?\d{3}$"))
                return @"\d{5}-?\d{3}";

            // Date: 01/01/2024
            if (Regex.IsMatch(value, @"^\d{2}/\d{2}/\d{4}$"))
                return @"\d{2}/\d{2}/\d{4}";

            // Email
            if (Regex.IsMatch(value, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
                return @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";

            // Pure numbers (like inscription numbers)
            if (Regex.IsMatch(value, @"^\d+$"))
                return $@"\b{value}\b"; // Exact match for numbers

            // Alphanumeric
            if (Regex.IsMatch(value, @"^[A-Za-z0-9\s]+$"))
            {
                // Create a pattern that preserves the structure
                var pattern = Regex.Escape(value);
                return pattern;
            }

            return string.Empty;
        }

        private List<string> FindKeywordMarkers(string text, string value, string fieldName)
        {
            var keywords = new List<string>();

            var index = text.IndexOf(value, StringComparison.OrdinalIgnoreCase);
            if (index <= 0) return keywords;

            // Get text before the value (up to 100 characters)
            var beforeText = text.Substring(Math.Max(0, index - 100), Math.Min(100, index));

            // Find the last line or word before the value
            var lines = beforeText.Split('\n');
            if (lines.Length > 0)
            {
                var lastLine = lines[^1].Trim();
                if (!string.IsNullOrEmpty(lastLine) && lastLine.Length < 50)
                {
                    keywords.Add(lastLine);
                }
            }

            // Also try to find the field name itself in the text before the value
            var fieldNameIndex = beforeText.LastIndexOf(fieldName, StringComparison.OrdinalIgnoreCase);
            if (fieldNameIndex >= 0)
            {
                var marker = beforeText.Substring(fieldNameIndex).Trim();
                if (marker.Length < 50)
                {
                    keywords.Add(marker);
                }
            }

            // Look for common label patterns
            var commonPatterns = new[] { ":", "Nome:", "Número:", "CPF:", "Inscrição:", "Data:", "Situação:" };
            foreach (var pattern in commonPatterns)
            {
                if (beforeText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    var patternIndex = beforeText.LastIndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                    var marker = beforeText.Substring(patternIndex, beforeText.Length - patternIndex).Trim();
                    if (marker.Length < 50 && !keywords.Contains(marker))
                    {
                        keywords.Add(marker);
                    }
                }
            }

            return keywords.Take(5).ToList(); // Limit to top 5 keywords
        }
    }
}
