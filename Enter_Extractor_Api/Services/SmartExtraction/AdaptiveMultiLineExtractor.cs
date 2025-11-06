using Enter_Extractor_Api.Models.SmartExtraction;
using System.Text;
using System.Runtime.CompilerServices;

namespace Enter_Extractor_Api.Services.SmartExtraction;

public interface IAdaptiveMultiLineExtractor
{
    Task<(FieldExtractionResult? result, int newLineIndex)> ExtractMultiLineFieldAsync(
        string[] lines,
        int currentLine,
        string fieldName,
        string fieldDescription);
}

public class AdaptiveMultiLineExtractor : IAdaptiveMultiLineExtractor
{
    private const float MIN_CONFIDENCE_THRESHOLD = 0.75f;
    private const float DROP_THRESHOLD = 0.10f; // Parar se cair mais de 10%
    private const float NOISE_TOLERANCE = 0.03f; // Tolerar oscilações pequenas
    private const int MAX_LINES_TO_COMBINE = 10; // Limite de segurança

    private readonly ILabelDetector _labelDetector;
    private readonly INLIValidator? _nliValidator; // Opcional: usar se disponível

    public AdaptiveMultiLineExtractor(
        ILabelDetector labelDetector,
        INLIValidator? nliValidator = null) // Injeção opcional
    {
        _labelDetector = labelDetector;
        _nliValidator = nliValidator;
    }

    public async Task<(FieldExtractionResult? result, int newLineIndex)> ExtractMultiLineFieldAsync(
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static HashSet<string> ExtractKeywords(string fieldName, string fieldDescription)
    {
        var text = $"{fieldName} {fieldDescription}".ToLowerInvariant();
        var keywords = new HashSet<string>();

        var terms = new[]
        {
            "endereco", "endereço", "descricao", "descrição",
            "observacao", "observação", "historico", "histórico"
        };

        foreach (var term in terms)
        {
            if (text.Contains(term))
                keywords.Add(term.Replace("ç", "c").Replace("ã", "a").Replace("õ", "o"));
        }

        return keywords;
    }
}
