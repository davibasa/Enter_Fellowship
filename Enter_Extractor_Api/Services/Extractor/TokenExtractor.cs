using Enter_Extractor_Api.Models.SmartExtraction;
using Enter_Extractor_Api.Services.SmartExtraction;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Enter_Extractor_Api.Services;

public interface ITokenExtractor
{
    //ValueTask<(FieldExtractionResult? result, int newLineIndex)> ExtractSimpleFieldAsync(
    //    string[] lines,
    //    int currentLine,
    //    string fieldName,
    //    string fieldDescription);

    //ValueTask<(FieldExtractionResult? result, int newLineIndex)> ExtractMultiLineFieldAsync(
    //    string[] lines,
    //    int currentLine,
    //    string fieldName,
    //    string fieldDescription);

    ValueTask<FieldExtractionResult>? ExtractEnumFieldAsync(
       List<(string line, int originalIndex)> availableLines,
       string fieldName,
       string fieldDescription);

    ValueTask<FieldExtractionResult>? ExtractRegexFieldAsync(
        List<(string line, int originalIndex)> availableLines,
        string fieldName,
        FieldType fieldType);
}

public partial class TokenExtractor : ITokenExtractor
{
    [GeneratedRegex(@"\b[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ]{2,}(?:\s+[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ]{2,})*\b", RegexOptions.Compiled)]
    private static partial Regex CapsLockPattern();

    private static readonly string[] EnumKeywords = { "pode ser", "valores:", "pode conter", "opções:", "tipos:" };


    public TokenExtractor(
        )
    {
    }

    /// <summary>
    /// Extração de campo enum - otimizada para performance
    /// </summary>
    public ValueTask<FieldExtractionResult>? ExtractEnumFieldAsync(
        List<(string line, int originalIndex)> availableLines,
        string fieldName,
        string fieldDescription)
    {
        var enumValues = ExtractEnumValuesOptimized(fieldDescription);
        if (enumValues.Count == 0)
            return null;

        var matchResult = FindBestEnumMatch(availableLines, enumValues);
        if (!matchResult.found)
            return null;

        var (matchedValue, lineIndex, _) = matchResult;

        var extractedLine = availableLines[lineIndex];
        var lineAfterRemoval = extractedLine.line.Replace(matchedValue, "", StringComparison.OrdinalIgnoreCase).Trim();

        if (string.IsNullOrWhiteSpace(lineAfterRemoval))
        {
            availableLines.RemoveAt(lineIndex);
        }
        else
        {
            availableLines[lineIndex] = (lineAfterRemoval, extractedLine.originalIndex);
        }

        var result = new FieldExtractionResult
        {
            Value = matchedValue,
        };

        return new ValueTask<FieldExtractionResult>(result);
    }

    /// <summary>
    /// Extrai valores de enum da descrição - versão otimizada
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static List<string> ExtractEnumValuesOptimized(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return new List<string>();

        var result = new List<string>(4); 

        foreach (var keyword in EnumKeywords)
        {
            var index = description.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;

            var textAfterKeyword = description.AsSpan(index + keyword.Length);
            var matches = CapsLockPattern().Matches(textAfterKeyword.ToString());

            if (matches.Count == 0)
                continue;

            var uniqueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in matches)
            {
                var value = match.Value.Trim();

                if (value.Length >= 3)
                {
                    uniqueValues.Add(value);
                }
            }

            if (uniqueValues.Count > 0)
            {
                result.AddRange(uniqueValues);
                return result; 
            }
        }

        return result;
    }

    /// <summary>
    /// Busca o melhor match de enum no texto - versão otimizada
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (string matchedValue, int lineIndex, bool found) FindBestEnumMatch(
        List<(string line, int originalIndex)> availableLines,
        List<string> enumValues)
    {
        if (enumValues.Count == 0 || availableLines.Count == 0)
            return (string.Empty, -1, false);

        for (int i = 0; i < availableLines.Count; i++)
        {
            var line = availableLines[i].line;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            foreach (var enumValue in enumValues)
            {
                if (line.Equals(enumValue, StringComparison.OrdinalIgnoreCase))
                {
                    return (enumValue, i, true);
                }
                
                if (line.Contains(enumValue, StringComparison.OrdinalIgnoreCase))
                {
                    return (enumValue, i, true);
                }
            }
        }

        return (string.Empty, -1, false);
    }

    /// <summary>
    /// Extração de campo regex - otimizada para performance O(n)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public ValueTask<FieldExtractionResult>? ExtractRegexFieldAsync(
        List<(string line, int originalIndex)> availableLines,
        string fieldName,
        FieldType fieldType)
    {
        var patterns = RegexPatternBank.GetPatternsForFieldType(fieldType);
        if (patterns.Length == 0)
            return null;

        var compiledPatterns = RegexPatternBank.GetCompiledPatternsForFieldType(fieldType);

        for (int i = 0; i < availableLines.Count; i++)
        {
            var lineData = availableLines[i];

            if (string.IsNullOrWhiteSpace(lineData.line))
                continue;

            var matchResult = RegexPatternBank.ApplyCompiledPatternsToLine(lineData.line, compiledPatterns);

            if (matchResult.success)
            {
                var lineAfterRemoval = lineData.line.Replace(matchResult.value, "", StringComparison.Ordinal).Trim();

                if (string.IsNullOrWhiteSpace(lineAfterRemoval))
                {
                    availableLines.RemoveAt(i);
                }
                else
                {
                    availableLines[i] = (lineAfterRemoval, lineData.originalIndex);
                }

                var result = new FieldExtractionResult
                {
                    Value = matchResult.value,
                };

                return new ValueTask<FieldExtractionResult>(result);
            }
        }

        return null;
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float ValidateTokenHeuristic(string fieldName, string fieldDescription, string token)
    {

        var keywords = ExtractKeywords(fieldName, fieldDescription);

        if (keywords.Contains("nome"))
        {
            if (char.IsUpper(token[0]) && (token.Contains(' ') || token.Contains("'")))
                return 0.85f;
        }

        if (keywords.Contains("inscricao") || keywords.Contains("numero"))
        {
            if (token.All(char.IsDigit))
                return 0.85f;
        }

        if (keywords.Contains("seccional") || keywords.Contains("estado") || keywords.Contains("uf"))
        {
            if (token.Length == 2 && token.All(char.IsUpper))
                return 0.85f;
        }

        if (keywords.Contains("cpf"))
        {
            if (token.Replace(".", "").Replace("-", "").Length == 11)
                return 0.85f;
        }

        if (keywords.Contains("cnpj"))
        {
            if (token.Replace(".", "").Replace("/", "").Replace("-", "").Length == 14)
                return 0.85f;
        }

        if (keywords.Contains("cep"))
        {
            if (token.Replace("-", "").Length == 8 && token.Replace("-", "").All(char.IsDigit))
                return 0.85f;
        }

        return 0.5f; // Confiança baixa
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static HashSet<string> ExtractKeywords(string fieldName, string fieldDescription)
    {
        var text = $"{fieldName} {fieldDescription}".ToLowerInvariant();
        var keywords = new HashSet<string>(StringComparer.Ordinal);

        var terms = new[]
        {
            "nome", "inscricao", "numero", "seccional", "estado", "uf",
            "cpf", "cnpj", "cep", "telefone", "email"
        };

        foreach (var term in terms)
        {
            if (text.Contains(term, StringComparison.Ordinal))
                keywords.Add(term);
        }

        return keywords;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string BuildCandidate(List<string> lines)
    {
        if (lines.Count == 1)
            return lines[0];

        // Usar StringBuilder para melhor performance
        var sb = new StringBuilder(lines.Sum(l => l.Length) + lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0)
                sb.Append('\n');
            sb.Append(lines[i]);
        }
        return sb.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private float ValidateMultiLineHeuristic(string fieldName, string fieldDescription, string candidate, int lineCount)
    {
        // Heurística simples para Fase 1
        // TODO: Substituir por Zero-Shot NLI na Fase 2

        var keywords = ExtractKeywords(fieldName, fieldDescription);

        // Endereço: deve ter logradouro, número, cidade/estado, CEP
        if (keywords.Contains("endereco"))
        {
            float score = 0.5f;
            var lowerCandidate = candidate.ToLowerInvariant();

            // Tem logradouro (avenida, rua, etc)?
            if (lowerCandidate.Contains("avenida") || lowerCandidate.Contains("rua") ||
                lowerCandidate.Contains("travessa") || lowerCandidate.Contains("alameda"))
                score += 0.15f;

            // Tem número (Nº, N°, nº)?
            if (lowerCandidate.Contains("nº") || lowerCandidate.Contains("n°") ||
                lowerCandidate.Contains("numero") || System.Text.RegularExpressions.Regex.IsMatch(candidate, @",\s*\d+"))
                score += 0.10f;

            // Tem CEP (8 dígitos)?
            if (System.Text.RegularExpressions.Regex.IsMatch(candidate, @"\b\d{8}\b") ||
                System.Text.RegularExpressions.Regex.IsMatch(candidate, @"\b\d{5}-\d{3}\b"))
                score += 0.15f;

            // Tem sigla de estado (2 letras maiúsculas)?
            if (System.Text.RegularExpressions.Regex.IsMatch(candidate, @"\b[A-Z]{2}\b"))
                score += 0.10f;

            // Penalizar se muito curto (provavelmente incompleto)
            if (lineCount == 1)
                score -= 0.10f;

            // Bonus se tem 3 linhas (típico: logradouro + cidade/estado + CEP)
            if (lineCount == 3)
                score += 0.10f;

            return Math.Clamp(score, 0f, 1f);
        }

        // Outros campos multi-linha (descrição, observação)
        if (keywords.Contains("descricao") || keywords.Contains("observacao"))
        {
            // Quanto mais linhas, maior a confiança (até um limite)
            float score = 0.6f + (lineCount * 0.05f);
            return Math.Clamp(score, 0f, 0.90f);
        }

        return 0.5f; // Confiança padrão
    }

}
