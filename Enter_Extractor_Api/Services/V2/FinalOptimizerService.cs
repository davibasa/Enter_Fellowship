using Enter_Extractor_Api.Models.V2;

namespace Enter_Extractor_Api.Services.V2;

/// <summary>
/// FASE 3: Final Optimizer
/// Merge de resultados com priorização: Regex > SmartExtract > Heurísticas
/// </summary>
public class FinalOptimizerService
{
    private readonly ILogger<FinalOptimizerService> _logger;

    // Pesos para cálculo de confiança final
    private const float WeightRegex = 1.0f;
    private const float WeightSmartExtract = 0.8f;
    private const float WeightHeuristic = 0.6f;

    public FinalOptimizerService(ILogger<FinalOptimizerService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Merge resultados de todas as fases
    /// Priorização: FASE 1 (Regex) > FASE 2.5 (SmartExtract) > FASE 3 (Heurísticas)
    /// </summary>
    public Dictionary<string, ExtractedFieldV2> MergeResults(
        Dictionary<string, ExtractedFieldV2> phase1Results,
        Dictionary<string, ExtractedFieldV2> phase25Results,
        Dictionary<string, string> schema,
        string traceId)
    {
        _logger.LogInformation(
            "🔄 [TraceId: {TraceId}] [FinalOptimizer] Merge: Phase1={P1}, Phase2.5={P25}, Schema={S}",
            traceId, phase1Results.Count, phase25Results.Count, schema.Count
        );

        var merged = new Dictionary<string, ExtractedFieldV2>();

        // Processar todos os campos do schema
        foreach (var (fieldName, fieldDescription) in schema)
        {
            ExtractedFieldV2? selected = null;
            string reason = "";

            // 1. Prioridade: FASE 1 (Regex) - se existir e tiver confiança alta
            if (phase1Results.TryGetValue(fieldName, out var phase1Field) &&
                !string.IsNullOrWhiteSpace(phase1Field.Value))
            {
                if (phase1Field.Confidence >= 0.8f)
                {
                    selected = phase1Field;
                    reason = "Phase1 (alta confiança)";
                }
            }

            // 2. Se não tem Phase1 válida, usar SmartExtract
            if (selected == null &&
                phase25Results.TryGetValue(fieldName, out var phase25Field) &&
                !string.IsNullOrWhiteSpace(phase25Field.Value))
            {
                selected = phase25Field;
                reason = "Phase2.5 (SmartExtract)";
            }

            // 3. Se ainda não tem, aplicar heurística simples
            if (selected == null)
            {
                selected = new ExtractedFieldV2
                {
                    Value = "",
                    Confidence = 0.0f,
                    Method = "not_found",
                    SourcePhase = "phase3",
                    LineIndex = null
                };
                reason = "Phase3 (não encontrado)";
            }

            // Aplicar otimizações finais (limpeza, normalização)
            selected = ApplyHeuristics(selected, fieldName, fieldDescription, traceId);

            merged[fieldName] = selected;

            if (!string.IsNullOrWhiteSpace(selected.Value))
            {
                _logger.LogInformation(
                    "  ✅ [TraceId: {TraceId}] {Field}: '{Value}' (conf: {Conf:F3}, {Reason})",
                    traceId, fieldName, selected.Value, selected.Confidence, reason
                );
            }
            else
            {
                _logger.LogWarning(
                    "  ⚠️ [TraceId: {TraceId}] {Field}: NÃO ENCONTRADO",
                    traceId, fieldName
                );
            }
        }

        return merged;
    }

    /// <summary>
    /// Merge resultados após fallback GPT
    /// </summary>
    public Dictionary<string, ExtractedFieldV2> MergeWithFallback(
        Dictionary<string, ExtractedFieldV2> currentResults,
        Dictionary<string, LlmFallbackField> fallbackResults,
        string traceId)
    {
        _logger.LogInformation(
            "🤖 [TraceId: {TraceId}] [FinalOptimizer] Merge com Fallback GPT: {Count} campos",
            traceId, fallbackResults.Count
        );

        foreach (var (fieldName, fallbackField) in fallbackResults)
        {
            if (string.IsNullOrWhiteSpace(fallbackField.Value))
                continue;

            // Sobrescrever apenas se GPT encontrou valor e está mais confiante
            if (!currentResults.TryGetValue(fieldName, out var currentField) ||
                string.IsNullOrWhiteSpace(currentField.Value) ||
                fallbackField.Confidence > currentField.Confidence)
            {
                currentResults[fieldName] = new ExtractedFieldV2
                {
                    Value = fallbackField.Value,
                    Confidence = fallbackField.Confidence,
                    Method = fallbackField.Method,
                    SourcePhase = "fallback",
                    LineIndex = null
                };

                _logger.LogInformation(
                    "  🤖 [TraceId: {TraceId}] {Field}: GPT corrigiu para '{Value}' (conf: {Conf:F3})",
                    traceId, fieldName, fallbackField.Value, fallbackField.Confidence
                );
            }
        }

        return currentResults;
    }

    /// <summary>
    /// Aplica heurísticas de limpeza e normalização
    /// </summary>
    private ExtractedFieldV2 ApplyHeuristics(
        ExtractedFieldV2 field,
        string fieldName,
        string fieldDescription,
        string traceId)
    {
        if (string.IsNullOrWhiteSpace(field.Value))
            return field;

        var originalValue = field.Value;
        var cleanedValue = field.Value;

        // 1. Remover espaços extras
        cleanedValue = System.Text.RegularExpressions.Regex.Replace(cleanedValue, @"\s+", " ").Trim();

        // 2. Remover labels comuns que podem ter escapado
        var commonLabels = new[] { "Nome:", "CPF:", "CNPJ:", "Data:", "Valor:", "Endereço:", "Telefone:" };
        foreach (var label in commonLabels)
        {
            if (cleanedValue.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            {
                cleanedValue = cleanedValue[label.Length..].Trim();
            }
        }

        // 3. Normalizar campos específicos
        cleanedValue = fieldName.ToLowerInvariant() switch
        {
            var f when f.Contains("cpf") => NormalizeCpf(cleanedValue),
            var f when f.Contains("cnpj") => NormalizeCnpj(cleanedValue),
            var f when f.Contains("telefone") || f.Contains("fone") => NormalizeTelefone(cleanedValue),
            var f when f.Contains("cep") => NormalizeCep(cleanedValue),
            _ => cleanedValue
        };

        // 4. Se valor mudou, ajustar confiança
        if (cleanedValue != originalValue)
        {
            field.Confidence *= 0.95f; // Pequena penalidade por limpeza
            field.Value = cleanedValue;

            _logger.LogDebug(
                "  🧹 [TraceId: {TraceId}] {Field}: '{Original}' → '{Cleaned}'",
                traceId, fieldName, originalValue, cleanedValue
            );
        }

        return field;
    }

    /// <summary>
    /// Calcula confiança média ponderada
    /// </summary>
    public float CalculateWeightedConfidence(Dictionary<string, ExtractedFieldV2> results)
    {
        if (results.Count == 0)
            return 0.0f;

        float totalWeight = 0.0f;
        float weightedSum = 0.0f;

        foreach (var field in results.Values)
        {
            if (string.IsNullOrWhiteSpace(field.Value))
                continue;

            var weight = field.SourcePhase switch
            {
                "phase1" => WeightRegex,
                "phase2.5" => WeightSmartExtract,
                "phase3" => WeightHeuristic,
                "fallback" => 1.0f, // GPT tem peso máximo
                _ => 0.5f
            };

            weightedSum += field.Confidence * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0.0f;
    }

    // ========== Normalizadores ==========

    private string NormalizeCpf(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (digits.Length == 11)
        {
            return $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..11]}";
        }

        return value;
    }

    private string NormalizeCnpj(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (digits.Length == 14)
        {
            return $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..14]}";
        }

        return value;
    }

    private string NormalizeTelefone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());

        // (XX) XXXXX-XXXX ou (XX) XXXX-XXXX
        if (digits.Length == 11)
        {
            return $"({digits[..2]}) {digits[2..7]}-{digits[7..11]}";
        }
        else if (digits.Length == 10)
        {
            return $"({digits[..2]}) {digits[2..6]}-{digits[6..10]}";
        }

        return value;
    }

    private string NormalizeCep(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (digits.Length == 8)
        {
            return $"{digits[..5]}-{digits[5..8]}";
        }

        return value;
    }
}
