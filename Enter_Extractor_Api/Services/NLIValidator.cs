using Enter_Extractor_Api.Services.SmartExtraction;
using System.Runtime.CompilerServices;

namespace Enter_Extractor_Api.Services;

/// <summary>
/// Interface para validação de campos usando NLI (Natural Language Inference)
/// </summary>
public interface INLIValidator
{
    /// <summary>
    /// Valida se o valor extraído é válido para o campo especificado
    /// </summary>
    Task<float> ValidateFieldAsync(string fieldName, string fieldDescription, string extractedValue);

    /// <summary>
    /// Valida campo multi-linha (endereços, descrições)
    /// </summary>
    Task<float> ValidateMultiLineFieldAsync(string fieldName, string fieldDescription, string extractedValue, int lineCount);

    /// <summary>
    /// Valida token simples (nome, CPF, sigla)
    /// </summary>
    Task<float> ValidateSimpleTokenAsync(string fieldName, string fieldDescription, string token);

    /// <summary>
    /// Valida valor enum
    /// </summary>
    Task<float> ValidateEnumValueAsync(string fieldName, string fieldDescription, string enumValue, string[] possibleValues);

    /// <summary>
    /// Verifica se uma linha é uma label/rótulo de formulário
    /// </summary>
    Task<bool> IsLabelAsync(string text);

    /// <summary>
    /// Classifica um texto entre múltiplas categorias candidatas
    /// </summary>
    Task<ClassificationResult> ClassifyTextAsync(string text, string[] candidateLabels);
}

/// <summary>
/// Validador de campos usando Zero-Shot Classification
/// Converte validações de campos em queries de NLI
/// </summary>
public class NLIValidator : INLIValidator
{
    private readonly IZeroShotClassifier _classifier;
    private readonly ILogger<NLIValidator> _logger;

    // Templates de hipóteses para diferentes tipos de campos
    private static class HypothesisTemplates
    {
        // Multi-linha
        public const string Address = "Este texto é um endereço completo com logradouro, número, cidade, estado e CEP";
        public const string AddressSimple = "Este texto contém informações de endereço";
        public const string Description = "Este texto é uma descrição ou observação detalhada";

        // Simples
        public const string FullName = "Este texto é um nome completo de pessoa";
        public const string CPF = "Este texto é um número de CPF válido";
        public const string CNPJ = "Este texto é um número de CNPJ válido";
        public const string Sigla = "Este texto é uma sigla ou abreviação de 2-3 letras";
        public const string Number = "Este texto é um número de identificação ou inscrição";
        public const string Date = "Este texto é uma data válida";
        public const string Phone = "Este texto é um número de telefone";
        public const string Email = "Este texto é um endereço de email válido";

        // Enum
        public const string EnumValue = "Este texto é um valor válido de categoria ou classificação";

        // Template genérico
        public static string Generic(string fieldName, string fieldDescription) =>
            $"Este texto é um valor válido para o campo '{fieldName}': {fieldDescription}";
    }

    public NLIValidator(IZeroShotClassifier classifier, ILogger<NLIValidator> logger)
    {
        _classifier = classifier;
        _logger = logger;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async Task<float> ValidateFieldAsync(string fieldName, string fieldDescription, string extractedValue)
    {
        // Criar hipótese baseada no nome e descrição do campo
        var hypothesis = BuildHypothesis(fieldName, fieldDescription);

        var score = await _classifier.ClassifyAsync(extractedValue, hypothesis);

        _logger.LogDebug("Validação NLI [{FieldName}]: score={Score:F3}, value='{Value}'",
            fieldName, score, TruncateForLog(extractedValue));

        return score;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async Task<float> ValidateMultiLineFieldAsync(
        string fieldName,
        string fieldDescription,
        string extractedValue,
        int lineCount)
    {
        // Detectar tipo de campo multi-linha
        var fieldNameLower = fieldName.ToLowerInvariant();
        var descriptionLower = fieldDescription.ToLowerInvariant();

        string hypothesis;

        // Endereço: usar template específico
        if (fieldNameLower.Contains("endereco") || descriptionLower.Contains("endereco") ||
            fieldNameLower.Contains("endereço") || descriptionLower.Contains("endereço"))
        {
            // Se tem múltiplas linhas, esperar endereço completo
            hypothesis = lineCount >= 2
                ? HypothesisTemplates.Address
                : HypothesisTemplates.AddressSimple;
        }
        // Descrição/Observação
        else if (fieldNameLower.Contains("descricao") || fieldNameLower.Contains("observacao") ||
                 descriptionLower.Contains("descricao") || descriptionLower.Contains("observacao"))
        {
            hypothesis = HypothesisTemplates.Description;
        }
        // Genérico
        else
        {
            hypothesis = HypothesisTemplates.Generic(fieldName, fieldDescription);
        }

        var score = await _classifier.ClassifyAsync(extractedValue, hypothesis);

        // Ajuste: penalizar se muito curto para campo multi-linha
        if (lineCount == 1 && extractedValue.Length < 20)
        {
            score *= 0.85f; // -15%
        }

        // Bonus se tem múltiplas linhas (esperado para endereços)
        if (lineCount >= 3 && hypothesis.Contains("endereço", StringComparison.OrdinalIgnoreCase))
        {
            score = Math.Min(score * 1.10f, 1.0f); // +10% (cap em 1.0)
        }

        _logger.LogDebug("Validação NLI Multi-linha [{FieldName}]: score={Score:F3}, lines={LineCount}",
            fieldName, score, lineCount);

        return score;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async Task<float> ValidateSimpleTokenAsync(string fieldName, string fieldDescription, string token)
    {
        var fieldNameLower = fieldName.ToLowerInvariant();
        var descriptionLower = fieldDescription.ToLowerInvariant();

        string hypothesis;

        // Detectar tipo de token
        if (fieldNameLower.Contains("nome") && !fieldNameLower.Contains("nomefantasia"))
        {
            hypothesis = HypothesisTemplates.FullName;
        }
        else if (fieldNameLower.Contains("cpf"))
        {
            hypothesis = HypothesisTemplates.CPF;
        }
        else if (fieldNameLower.Contains("cnpj"))
        {
            hypothesis = HypothesisTemplates.CNPJ;
        }
        else if (fieldNameLower.Contains("sigla") || fieldNameLower.Contains("uf") || fieldNameLower.Contains("estado"))
        {
            hypothesis = HypothesisTemplates.Sigla;
        }
        else if (fieldNameLower.Contains("telefone") || fieldNameLower.Contains("celular") || fieldNameLower.Contains("fone"))
        {
            hypothesis = HypothesisTemplates.Phone;
        }
        else if (fieldNameLower.Contains("email") || fieldNameLower.Contains("e-mail"))
        {
            hypothesis = HypothesisTemplates.Email;
        }
        else if (fieldNameLower.Contains("data") || fieldNameLower.Contains("dt_"))
        {
            hypothesis = HypothesisTemplates.Date;
        }
        else if (fieldNameLower.Contains("numero") || fieldNameLower.Contains("inscricao") || fieldNameLower.Contains("registro"))
        {
            hypothesis = HypothesisTemplates.Number;
        }
        else
        {
            hypothesis = HypothesisTemplates.Generic(fieldName, fieldDescription);
        }

        var score = await _classifier.ClassifyAsync(token, hypothesis);

        _logger.LogDebug("Validação NLI Token [{FieldName}]: score={Score:F3}, token='{Token}'",
            fieldName, score, token);

        return score;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async Task<float> ValidateEnumValueAsync(
        string fieldName,
        string fieldDescription,
        string enumValue,
        string[] possibleValues)
    {
        // Validação 1: O texto é um enum válido?
        var hypothesis1 = HypothesisTemplates.EnumValue;
        var score1 = await _classifier.ClassifyAsync(enumValue, hypothesis1);

        // Validação 2: Está semanticamente relacionado às opções possíveis?
        if (possibleValues != null && possibleValues.Length > 0)
        {
            var possibleValuesText = string.Join(", ", possibleValues.Take(5)); // Limitar a 5
            var hypothesis2 = $"Este texto é uma categoria relacionada a: {possibleValuesText}";
            var score2 = await _classifier.ClassifyAsync(enumValue, hypothesis2);

            // Média ponderada (70% validação genérica, 30% similaridade com opções)
            var finalScore = score1 * 0.7f + score2 * 0.3f;

            _logger.LogDebug("Validação NLI Enum [{FieldName}]: score={Score:F3}, value='{Value}', possibleValues={Count}",
                fieldName, finalScore, enumValue, possibleValues.Length);

            return finalScore;
        }

        return score1;
    }

    /// <summary>
    /// Constrói hipótese baseada no nome e descrição do campo
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string BuildHypothesis(string fieldName, string fieldDescription)
    {
        // Se descrição é rica, usar ela
        if (!string.IsNullOrWhiteSpace(fieldDescription) && fieldDescription.Length > 20)
        {
            return $"Este texto representa: {fieldDescription}";
        }

        // Senão, usar template genérico
        return HypothesisTemplates.Generic(fieldName, fieldDescription);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string TruncateForLog(string text, int maxLength = 50)
    {
        if (text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }

    /// <summary>
    /// Verifica se um texto é uma label/rótulo de formulário
    /// </summary>
    public async Task<bool> IsLabelAsync(string text)
    {
        var score = await _classifier.ClassifyAsync(text, "Este texto é um cabeçalho, título de seção ou label descritivo de campo?");

        var isLabel = score > 0.7f;

        _logger.LogDebug(
            "Label detection: '{Text}' | score={Score:F3} → {IsLabel}",
            TruncateForLog(text),
            score,
            isLabel);

        return isLabel;
    }

    /// <summary>
    /// Classifica um texto entre múltiplas categorias candidatas
    /// </summary>
    public async Task<ClassificationResult> ClassifyTextAsync(string text, string[] candidateLabels)
    {
        _logger.LogDebug("Classificando texto '{Text}' entre {Count} categorias",
            TruncateForLog(text), candidateLabels.Length);

        var result = await _classifier.ClassifyBestMatchAsync(text, candidateLabels);

        _logger.LogDebug("Classificação: '{Text}' → {Label} (score={Score:F3})",
            TruncateForLog(text), result.PredictedLabel, result.Confidence);

        return result;
    }
}

