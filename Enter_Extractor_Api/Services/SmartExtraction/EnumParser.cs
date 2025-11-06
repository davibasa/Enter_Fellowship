using System.Text.RegularExpressions;

namespace Enter_Extractor_Api.Services.SmartExtraction
{
    public interface IEnumParser
    {
        List<string> ExtractEnumValues(string description);
        Models.SmartExtraction.EnumMatchResult FindBestMatch(string[] textLines, int startLine, List<string> enumValues);
    }

    /// <summary>
    /// Parser que detecta enums dinâmicos a partir de palavras em CAPSLOCK nas descrições dos campos
    /// </summary>
    public class EnumParser : IEnumParser
    {
        private readonly ILogger<EnumParser> _logger;

        // Regex para detectar palavras em CAPSLOCK (2+ letras maiúsculas)
        private static readonly Regex CapsLockPattern = new Regex(
            @"\b[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ]{2,}(?:\s+[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ]{2,})*\b",
            RegexOptions.Compiled
        );

        public EnumParser(ILogger<EnumParser> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Extrai valores de enum da descrição do campo
        /// Procura por palavras em CAPSLOCK após keywords como "pode ser", "valores:", etc
        /// </summary>
        public List<string> ExtractEnumValues(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return new List<string>();

            // Keywords que indicam lista de valores possíveis
            var keywords = new[] { "pode ser", "pode conter", "valores:", "opções:", "tipos:" };

            foreach (var keyword in keywords)
            {
                var index = description.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    // Texto após a keyword
                    var textAfterKeyword = description.Substring(index + keyword.Length);
                    var matches = CapsLockPattern.Matches(textAfterKeyword);

                    var enumValues = matches
                        .Cast<Match>()
                        .Select(m => m.Value.Trim())
                        .Where(v => v.Length >= 3) // Filtrar siglas muito curtas (ex: "PR", "SP")
                        .Where(v => !IsCommonWord(v)) // Filtrar palavras comuns que não são enums
                        .Distinct()
                        .ToList();

                    if (enumValues.Any())
                    {
                        _logger.LogInformation(
                            "Enum detectado na descrição. Keyword: '{Keyword}', Valores: [{Values}]",
                            keyword,
                            string.Join(", ", enumValues)
                        );
                        return enumValues;
                    }
                }
            }

            _logger.LogDebug("Nenhum enum detectado na descrição: {Description}", description);
            return new List<string>();
        }

        /// <summary>
        /// Busca o melhor match de enum no texto, a partir de uma linha específica
        /// </summary>
        public Models.SmartExtraction.EnumMatchResult FindBestMatch(
            string[] textLines,
            int startLine,
            List<string> enumValues)
        {
            if (!enumValues.Any())
            {
                return new Models.SmartExtraction.EnumMatchResult { Found = false };
            }

            // Buscar linha por linha, a partir da linha inicial
            for (int i = startLine; i < textLines.Length; i++)
            {
                var line = textLines[i].Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Tentar match exato com cada valor do enum
                foreach (var enumValue in enumValues)
                {
                    // Match exato (case-insensitive)
                    if (line.Equals(enumValue, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation(
                            "Enum match encontrado (exato): '{Value}' na linha {Line}",
                            enumValue,
                            i
                        );

                        return new Models.SmartExtraction.EnumMatchResult
                        {
                            Value = enumValue,
                            LineIndex = i,
                            Found = true
                        };
                    }

                    // Match parcial (contém o valor)
                    if (line.Contains(enumValue, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation(
                            "Enum match encontrado (contém): '{Value}' na linha {Line}",
                            enumValue,
                            i
                        );

                        return new Models.SmartExtraction.EnumMatchResult
                        {
                            Value = enumValue,
                            LineIndex = i,
                            Found = true
                        };
                    }
                }
            }

            _logger.LogDebug(
                "Nenhum enum match encontrado. Valores buscados: [{Values}]",
                string.Join(", ", enumValues)
            );

            return new Models.SmartExtraction.EnumMatchResult { Found = false };
        }

        /// <summary>
        /// Verifica se uma palavra em CAPSLOCK é uma palavra comum (não enum)
        /// </summary>
        private bool IsCommonWord(string word)
        {
            var commonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "PDF", "CPF", "CNPJ", "RG", "CEP", "BRASIL", "BRASIL",
                "OAB", "CRM", "CRO", "CRC", // Siglas de conselhos (podem ser úteis)
            };

            return commonWords.Contains(word);
        }
    }
}
