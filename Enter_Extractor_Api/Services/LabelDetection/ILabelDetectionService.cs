using Enter_Extractor_Api.Models.Redis;

namespace Enter_Extractor_Api.Services.LabelDetection;

/// <summary>
/// Serviço para detectar labels no texto usando RoBERTa/Embeddings
/// Utiliza cache Redis para otimização
/// </summary>
public interface ILabelDetectionService
{
    /// <summary>
    /// Detecta labels presentes no texto do documento usando similaridade semântica
    /// Retorna lista de textos candidatos que correspondem aos campos do schema
    /// Usa cache Redis se disponível
    /// </summary>
    /// <param name="label">Label do documento (ex: "Carteira OAB")</param>
    /// <param name="pdfHash">Hash SHA256 do PDF</param>
    /// <param name="schema">Schema com campos a detectar</param>
    /// <param name="schemaHash">Hash do schema para cache</param>
    /// <param name="text">Texto do documento</param>
    /// <param name="topK">Número de matches por label (padrão: 3)</param>
    /// <param name="minTokenLength">Tamanho mínimo de tokens (padrão: 3)</param>
    /// <param name="similarityThreshold">Score mínimo de similaridade 0-1 (padrão: 0.5)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>DTO com labels detectadas</returns>
    Task<DetectedLabelsDto> DetectLabelsAsync(
        string label,
        string pdfHash,
        Dictionary<string, string> schema,
        string schemaHash,
        string text,
        int topK = 3,
        int minTokenLength = 3,
        float similarityThreshold = 0.5f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove labels detectadas do texto
    /// Retorna texto limpo sem os candidatos identificados como labels
    /// </summary>
    /// <param name="text">Texto original</param>
    /// <param name="detectedLabels">Labels detectadas</param>
    /// <returns>Texto sem as labels</returns>
    string RemoveLabelsFromText(string text, DetectedLabelsDto detectedLabels);
}
