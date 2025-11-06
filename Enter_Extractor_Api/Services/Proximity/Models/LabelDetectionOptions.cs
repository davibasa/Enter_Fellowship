namespace Enter_Extractor_Api.Services.Proximity.Models;

/// <summary>
/// Opções de configuração para detecção de labels
/// </summary>
public class LabelDetectionOptions
{
    /// <summary>
    /// Threshold mínimo de similaridade para fuzzy matching (0.0 a 1.0)
    /// Default: 0.75
    /// </summary>
    public float FuzzyMatchThreshold { get; init; } = 0.75f;

    /// <summary>
    /// Threshold mínimo de similaridade para semantic matching (0.0 a 1.0)
    /// Default: 0.70
    /// </summary>
    public float SemanticMatchThreshold { get; init; } = 0.70f;

    /// <summary>
    /// Se deve usar a API Python para detecção semântica
    /// Default: true
    /// </summary>
    public bool EnableSemanticDetection { get; init; } = true;

    /// <summary>
    /// Se deve tentar fuzzy matching quando exact/normalized falharem
    /// Default: true
    /// </summary>
    public bool EnableFuzzyMatching { get; init; } = true;

    /// <summary>
    /// Se deve remover pontuação ao buscar labels
    /// Default: true
    /// </summary>
    public bool RemovePunctuation { get; init; } = true;

    /// <summary>
    /// Caracteres de pontuação a remover
    /// Default: : ; , . - _ ( ) [ ] { }
    /// </summary>
    public char[] PunctuationChars { get; init; } =
        [':', ';', ',', '.', '-', '_', '(', ')', '[', ']', '{', '}'];

    /// <summary>
    /// Tamanho mínimo da label para buscar (evita falsos positivos)
    /// Default: 3 caracteres
    /// </summary>
    public int MinLabelLength { get; init; } = 3;

    /// <summary>
    /// Timeout para chamada da API Python (milissegundos)
    /// Default: 5000ms (5 segundos)
    /// </summary>
    public int PythonApiTimeoutMs { get; init; } = 5000;
}
