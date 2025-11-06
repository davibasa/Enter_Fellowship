using Enter_Extractor_Api.Models.SmartExtraction;
using Enter_Extractor_Api.Services.SmartExtraction;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Enter_Extractor_Api.Services;

public interface ITokenExtractor
{
    ValueTask<(FieldExtractionResult? result, int newLineIndex)> ExtractSimpleFieldAsync(
        string[] lines,
        int currentLine,
        string fieldName,
        string fieldDescription);

    ValueTask<(FieldExtractionResult? result, int newLineIndex)> ExtractMultiLineFieldAsync(
        string[] lines,
        int currentLine,
        string fieldName,
        string fieldDescription);

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
    private const float MIN_CONFIDENCE = 0.80f;
    private const int MAX_LINES_TO_SEARCH = 5;
    private const float MIN_CONFIDENCE_THRESHOLD = 0.75f;
    private const float DROP_THRESHOLD = 0.10f;
    private const float NOISE_TOLERANCE = 0.03f;
    private const int MAX_LINES_TO_COMBINE = 10;

    // Regex compilada estaticamente para detectar palavras em CAPSLOCK (2+ letras maiúsculas)
    [GeneratedRegex(@"\b[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ]{2,}(?:\s+[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ]{2,})*\b", RegexOptions.Compiled)]
    private static partial Regex CapsLockPattern();

    // Keywords que indicam lista de valores possíveis (mais comuns primeiro)
    private static readonly string[] EnumKeywords = { "pode ser", "valores:", "pode conter", "opções:", "tipos:" };

    private readonly ITextTokenizer _tokenizer;
    private readonly ILabelDetector _labelDetector;
    private readonly INLIValidator? _nliValidator;

    public TokenExtractor(
        ITextTokenizer tokenizer,
        ILabelDetector labelDetector,
        INLIValidator? nliValidator = null)
    {
        _tokenizer = tokenizer;
        _labelDetector = labelDetector;
        _nliValidator = nliValidator;
    }

    /// <summary>
    /// Extração de campo enum - otimizada para performance
    /// </summary>
    public ValueTask<FieldExtractionResult>? ExtractEnumFieldAsync(
        List<(string line, int originalIndex)> availableLines,
        string fieldName,
        string fieldDescription)
    {
        // Extrair valores de enum da descrição (inline otimizado)
        var enumValues = ExtractEnumValuesOptimized(fieldDescription);
        if (enumValues.Count == 0)
            return null;

        // Buscar melhor match nas linhas disponíveis
        var matchResult = FindBestEnumMatch(availableLines, enumValues);
        if (!matchResult.found)
            return null;

        var (matchedValue, lineIndex, _) = matchResult;

        // Remover valor extraído da linha
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

        var result = new List<string>(4); // Pre-allocate for typical case

        // Buscar keywords (mais comuns primeiro para early exit)
        foreach (var keyword in EnumKeywords)
        {
            var index = description.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;

            // Texto após a keyword
            var textAfterKeyword = description.AsSpan(index + keyword.Length);
            var matches = CapsLockPattern().Matches(textAfterKeyword.ToString());

            if (matches.Count == 0)
                continue;

            // Usar HashSet para evitar duplicatas (mais rápido que Distinct())
            var uniqueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in matches)
            {
                var value = match.Value.Trim();

                // Filtrar siglas muito curtas (< 3 chars)
                if (value.Length >= 3)
                {
                    uniqueValues.Add(value);
                }
            }

            if (uniqueValues.Count > 0)
            {
                result.AddRange(uniqueValues);
                return result; // Early exit - primeiro match é suficiente
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
        // Early exit
        if (enumValues.Count == 0 || availableLines.Count == 0)
            return (string.Empty, -1, false);

        // Buscar linha por linha
        for (int i = 0; i < availableLines.Count; i++)
        {
            var line = availableLines[i].line;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Tentar match com cada valor do enum
            foreach (var enumValue in enumValues)
            {
                // Match exato (mais comum) - check first
                if (line.Equals(enumValue, StringComparison.OrdinalIgnoreCase))
                {
                    return (enumValue, i, true);
                }

                // Match parcial (contém o valor)
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
        // Buscar padrões apropriados para o tipo de campo
        var patterns = RegexPatternBank.GetPatternsForFieldType(fieldType);
        if (patterns.Length == 0)
            return null;

        // Concatenar todos os padrões em um único regex com grupos nomeados
        // Isso permite testar todos os padrões em uma única passada (O(1) por linha)
        var compiledPatterns = RegexPatternBank.GetCompiledPatternsForFieldType(fieldType);

        // Buscar match em todas as linhas disponíveis - agora apenas O(n)
        for (int i = 0; i < availableLines.Count; i++)
        {
            var lineData = availableLines[i];

            if (string.IsNullOrWhiteSpace(lineData.line))
                continue;

            // Testar todos os padrões de uma vez usando o regex compilado
            var matchResult = RegexPatternBank.ApplyCompiledPatternsToLine(lineData.line, compiledPatterns);

            if (matchResult.success)
            {
                // Remover valor extraído da linha
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

    public async ValueTask<(FieldExtractionResult? result, int newLineIndex)> ExtractSimpleFieldAsync(
        string[] lines,
        int currentLine,
        string fieldName,
        string fieldDescription)
    {
        // Procurar nas próximas N linhas
        for (int i = currentLine; i < Math.Min(currentLine + MAX_LINES_TO_SEARCH, lines.Length); i++)
        {
            var line = lines[i];

            // Tokenizar linha (pode ter múltiplos valores)
            var tokens = _tokenizer.TokenizeLine(line.AsSpan());

            // Testar cada token individualmente
            foreach (var token in tokens)
            {
                // Validação com Zero-Shot (se disponível) ou heurística
                var confidence = await ValidateTokenAsync(fieldName, fieldDescription, token);

                if (confidence >= MIN_CONFIDENCE)
                {
                    var result = new FieldExtractionResult
                    {
                        Value = token
                    };

                    return (result, i + 1);
                }
            }
        }

        return (null, currentLine);
    }

    public async ValueTask<(FieldExtractionResult? result, int newLineIndex)> ExtractMultiLineFieldAsync(
        string[] lines,
        int currentLine,
        string fieldName,
        string fieldDescription)
    {
        var candidates = new List<CandidateValue>();
        var currentLinesInWindow = new List<string>();

        float previousConfidence = 0f;
        float peakConfidence = 0f;
        string? peakValue = null;
        int peakLineCount = 0;
        int peakEndLine = currentLine;

        // Expandir janela linha por linha
        for (int lineOffset = 0; lineOffset < MAX_LINES_TO_COMBINE; lineOffset++)
        {
            int currentLineIndex = currentLine + lineOffset;

            // Fim do documento
            if (currentLineIndex >= lines.Length)
                break;

            var line = lines[currentLineIndex].Trim();

            // Pular linhas vazias
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // ⭐ FASE 2: Pular labels/cabeçalhos usando Zero-Shot NLI (async)
            //bool isLabel = await _labelDetector.IsLabelLineAsync(line);

            //if (isLabel)
            //{
            //    // Se já temos conteúdo e encontramos um label, pode ser fim do campo
            //    if (currentLinesInWindow.Count > 0 && peakConfidence >= MIN_CONFIDENCE_THRESHOLD)
            //    {
            //        Console.WriteLine($"[{fieldName}] Label NLI detectado após conteúdo - parar expansão");
            //        break;
            //    }
            //    continue;
            //}

            // Adicionar linha à janela atual
            currentLinesInWindow.Add(line);

            // Combinar linhas acumuladas usando StringBuilder (otimização)
            string candidate = BuildCandidate(currentLinesInWindow);

            // ===================================
            // VALIDAR COM ZERO-SHOT (Fase 2) ou HEURÍSTICA (Fallback)
            // ===================================
            float confidence = await ValidateMultiLineAsync(fieldName, fieldDescription, candidate, currentLinesInWindow.Count);

            // Registrar candidato
            var candidateRecord = new CandidateValue
            {
                Value = candidate,
                Confidence = confidence,
                LineCount = currentLinesInWindow.Count,
                EndLineIndex = currentLineIndex,
                Selected = false
            };
            candidates.Add(candidateRecord);

            Console.WriteLine($"[{fieldName}] Janela {currentLinesInWindow.Count}: confidence = {confidence:F2}");

            // ===================================
            // DETECÇÃO DE PICO
            // ===================================

            // 1. Se confiança subiu: atualizar pico
            if (confidence > peakConfidence)
            {
                peakConfidence = confidence;
                peakValue = candidate;
                peakLineCount = currentLinesInWindow.Count;
                peakEndLine = currentLineIndex + 1;
                Console.WriteLine($"[{fieldName}] Novo pico! Confidence = {confidence:F2}");
            }

            // 2. Se confiança caiu significativamente: PARAR
            if (previousConfidence > 0 &&
                confidence < previousConfidence - DROP_THRESHOLD &&
                confidence < peakConfidence - NOISE_TOLERANCE)
            {
                Console.WriteLine($"[{fieldName}] Confiança caiu de {previousConfidence:F2} para {confidence:F2} - PARAR");
                candidates.Last().RejectionReason = "confidence_drop";
                break;
            }

            // 3. Se já atingiu confiança excelente (>0.90) e está estável: PARAR
            if (confidence > 0.90f &&
                Math.Abs(confidence - previousConfidence) < 0.05f)
            {
                Console.WriteLine($"[{fieldName}] Confiança excelente ({confidence:F2}) e estável - PARAR");
                peakConfidence = confidence;
                peakValue = candidate;
                peakLineCount = currentLinesInWindow.Count;
                peakEndLine = currentLineIndex + 1;
                break;
            }

            previousConfidence = confidence;
        }

        // ===================================
        // RETORNAR MELHOR RESULTADO
        // ===================================

        if (peakConfidence >= MIN_CONFIDENCE_THRESHOLD && peakValue != null)
        {
            // Marcar candidato escolhido
            var selectedCandidate = candidates.FirstOrDefault(c => c.Value == peakValue);
            if (selectedCandidate != null)
            {
                selectedCandidate.Selected = true;
            }

            var result = new FieldExtractionResult
            {
                Value = peakValue,
            };

            return (result, peakEndLine);
        }

        // Não encontrou com confiança suficiente
        return (null, currentLine);
    }

    /// <summary>
    /// Valida token usando Zero-Shot (se disponível) ou heurística
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async Task<float> ValidateTokenAsync(string fieldName, string fieldDescription, string token)
    {
        // Se NLI disponível, usar
        if (_nliValidator != null)
        {
            try
            {
                return await _nliValidator.ValidateSimpleTokenAsync(fieldName, fieldDescription, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] NLI falhou, usando heurística fallback: {ex.Message}");
            }
        }

        // Fallback: heurística
        return ValidateTokenHeuristic(fieldName, fieldDescription, token);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float ValidateTokenHeuristic(string fieldName, string fieldDescription, string token)
    {
        // Heurística simples baseada em keywords

        var keywords = ExtractKeywords(fieldName, fieldDescription);

        // Nome: deve começar com letra maiúscula e ter pelo menos 2 palavras ou D'ARC
        if (keywords.Contains("nome"))
        {
            if (char.IsUpper(token[0]) && (token.Contains(' ') || token.Contains("'")))
                return 0.85f;
        }

        // Inscrição/Número: deve ser número
        if (keywords.Contains("inscricao") || keywords.Contains("numero"))
        {
            if (token.All(char.IsDigit))
                return 0.85f;
        }

        // Seccional/Estado: sigla de 2 letras maiúsculas
        if (keywords.Contains("seccional") || keywords.Contains("estado") || keywords.Contains("uf"))
        {
            if (token.Length == 2 && token.All(char.IsUpper))
                return 0.85f;
        }

        // CPF: formato XXX.XXX.XXX-XX
        if (keywords.Contains("cpf"))
        {
            if (token.Replace(".", "").Replace("-", "").Length == 11)
                return 0.85f;
        }

        // CNPJ: formato XX.XXX.XXX/XXXX-XX
        if (keywords.Contains("cnpj"))
        {
            if (token.Replace(".", "").Replace("/", "").Replace("-", "").Length == 14)
                return 0.85f;
        }

        // CEP: 8 dígitos
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

    /// <summary>
    /// Valida campo multi-linha usando Zero-Shot (se disponível) ou heurística
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async Task<float> ValidateMultiLineAsync(string fieldName, string fieldDescription, string candidate, int lineCount)
    {
        // Se NLI disponível, usar
        if (_nliValidator != null)
        {
            try
            {
                return await _nliValidator.ValidateMultiLineFieldAsync(fieldName, fieldDescription, candidate, lineCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] NLI falhou, usando heurística fallback: {ex.Message}");
            }
        }

        // Fallback: heurística
        return ValidateMultiLineHeuristic(fieldName, fieldDescription, candidate, lineCount);
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
