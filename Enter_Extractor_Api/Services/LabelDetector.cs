using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Enter_Extractor_Api.Services.SmartExtraction;

namespace Enter_Extractor_Api.Services;

public interface ILabelDetector
{
    bool IsLabelLine(ReadOnlySpan<char> line);
    Task<bool> IsLabelLineAsync(string line);
}

/// <summary>
/// Detector de labels/cabeçalhos usando Zero-Shot NLI (Fase 2)
/// Fallback para heurística se NLI não disponível
/// </summary>
public class LabelDetector : ILabelDetector
{
    private const float LABEL_CONFIDENCE_THRESHOLD = 0.75f;

    private static readonly string[] LabelKeywords =
    {
        "profissional", "endereço", "endereco", "telefone",
        "situação", "situacao", "inscrição", "inscricao",
        "categoria", "nome", "dados", "informações", "informacoes"
    };

    // Regex compilado para reuso
    private static readonly Regex CapitalizedPattern = new Regex(
        @"^[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ][a-zà-ú]+(\s[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ][a-zà-ú]+)*$",
        RegexOptions.Compiled
    );

    private static readonly Regex DigitPattern = new Regex(@"\d", RegexOptions.Compiled);

    private readonly IZeroShotClassifier? _classifier;

    public LabelDetector(IZeroShotClassifier? classifier = null)
    {
        _classifier = classifier;
    }

    /// <summary>
    /// Versão síncrona (mantida para compatibilidade) - usa apenas heurística
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool IsLabelLine(ReadOnlySpan<char> lineSpan)
    {
        // Checks rápidos sem alocar string
        if (lineSpan.IsEmpty || lineSpan.IsWhiteSpace())
            return false;

        // Muito longa para ser label
        if (lineSpan.Length > 60)
            return false;

        // Converter para string apenas uma vez
        var line = lineSpan.ToString();

        // Tem números? Provavelmente não é label
        if (DigitPattern.IsMatch(line))
            return false;

        // Padrão de label: "Palavra Capitalizada" ou "Palavra Palavra"
        if (!CapitalizedPattern.IsMatch(line))
            return false;

        // Contém keywords típicos de labels? (StringComparison.OrdinalIgnoreCase para performance)
        return ContainsAnyKeyword(line);
    }

    /// <summary>
    /// ⭐ FASE 2: Versão async com Zero-Shot NLI
    /// Detecta se linha é um label/cabeçalho usando validação semântica
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async Task<bool> IsLabelLineAsync(string line)
    {
        // Validações rápidas antes de chamar NLI
        if (string.IsNullOrWhiteSpace(line))
            return false;

        if (line.Length > 60)
            return false;

        // Se tem números, provavelmente não é label (check rápido)
        if (DigitPattern.IsMatch(line))
            return false;

        // Se NLI disponível, usar validação semântica
        if (_classifier != null)
        {
            try
            {
                // Hipótese: "Este texto é um cabeçalho ou título de seção"
                var hypothesis = "Este texto é um cabeçalho, título de seção ou label descritivo de campo";
                var score = await _classifier.ClassifyAsync(line, hypothesis);

                // Se alta confiança que é label, retornar true
                if (score >= LABEL_CONFIDENCE_THRESHOLD)
                    return true;

                // Se baixa confiança que é label (<0.5), retornar false
                if (score < 0.85f)
                    return false;

                // Se confiança intermediária (0.5-0.75), usar heurística como desempate
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] NLI label detection falhou: {ex.Message}");
                // Continuar para heurística fallback
            }
        }

        // Fallback: usar heurística tradicional
        return IsLabelLineHeuristic(line);
    }

    /// <summary>
    /// Heurística tradicional para detecção de labels
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLabelLineHeuristic(string line)
    {
        // Padrão de label: "Palavra Capitalizada" ou "Palavra Palavra"
        if (!CapitalizedPattern.IsMatch(line))
            return false;

        // Contém keywords típicos de labels?
        return ContainsAnyKeyword(line);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ContainsAnyKeyword(string line)
    {
        foreach (var keyword in LabelKeywords)
        {
            if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
