namespace Enter_Extractor_Api.Services.Proximity.Models;

/// <summary>
/// Representa uma label detectada no texto do PDF
/// </summary>
public class DetectedLabel
{
    /// <summary>
    /// Nome do campo no schema (ex: "inscricao")
    /// </summary>
    public required string FieldName { get; init; }

    /// <summary>
    /// Texto encontrado no PDF (ex: "Inscrição", "Inscricao:", "INSCRIÇÃO")
    /// </summary>
    public required string DetectedText { get; init; }

    /// <summary>
    /// Índice da linha onde a label foi encontrada (zero-based)
    /// </summary>
    public required int LineIndex { get; init; }

    /// <summary>
    /// Índice do caractere inicial na linha (zero-based)
    /// </summary>
    public required int CharStartIndex { get; init; }

    /// <summary>
    /// Índice do caractere final na linha (exclusive)
    /// </summary>
    public required int CharEndIndex { get; init; }

    /// <summary>
    /// Score de similaridade (0.0 a 1.0) entre o campo e o texto detectado
    /// </summary>
    public required float SimilarityScore { get; init; }

    /// <summary>
    /// Estratégia que detectou a label (para debug/logging)
    /// </summary>
    public required LabelDetectionStrategy Strategy { get; init; }

    /// <summary>
    /// Linha completa onde a label foi encontrada
    /// </summary>
    public string FullLine { get; init; } = string.Empty;
}

/// <summary>
/// Estratégias de detecção de labels
/// </summary>
public enum LabelDetectionStrategy
{
    /// <summary>
    /// Match exato (case-insensitive)
    /// </summary>
    ExactMatch = 1,

    /// <summary>
    /// Match após normalização (remove acentos, pontuação)
    /// </summary>
    NormalizedMatch = 2,

    /// <summary>
    /// Match usando fuzzy string matching (Levenshtein)
    /// </summary>
    FuzzyMatch = 3,

    /// <summary>
    /// Match usando embeddings semânticos (Python API)
    /// </summary>
    SemanticMatch = 4
}
