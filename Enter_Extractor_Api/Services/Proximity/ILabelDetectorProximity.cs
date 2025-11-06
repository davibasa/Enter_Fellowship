using Enter_Extractor_Api.Services.Proximity.Models;

namespace Enter_Extractor_Api.Services.Proximity;

/// <summary>
/// Interface para detecção de labels do schema no texto do PDF
/// </summary>
public interface ILabelDetectorProximity
{
    /// <summary>
    /// Detecta todas as labels dos campos do schema no texto fornecido
    /// </summary>
    /// <param name="schema">Dicionário campo → descrição</param>
    /// <param name="lines">Linhas do texto do PDF</param>
    /// <param name="options">Opções de configuração (opcional)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de labels detectadas, ordenadas por linha e posição</returns>
    Task<List<DetectedLabel>> DetectLabelsAsync(
        Dictionary<string, string> schema,
        string[] lines,
        LabelDetectionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detecta label de um campo específico no texto
    /// </summary>
    /// <param name="fieldName">Nome do campo (ex: "inscricao")</param>
    /// <param name="fieldDescription">Descrição do campo</param>
    /// <param name="lines">Linhas do texto do PDF</param>
    /// <param name="options">Opções de configuração (opcional)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Labels detectadas para o campo específico</returns>
    Task<List<DetectedLabel>> DetectLabelAsync(
        string fieldName,
        string fieldDescription,
        string[] lines,
        LabelDetectionOptions? options = null,
        CancellationToken cancellationToken = default);
}
