using Enter_Extractor_Api.Models;
using Enter_Extractor_Api.Models.SmartExtraction;
using Enter_Extractor_Api.Services.SmartExtraction;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Enter_Extractor_Api.Services
{
    public class ExtractorService : IExtractorService
    {
        private readonly IPdfTextExtractor _pdfExtractor;
        private readonly ILogger<ExtractorService> _logger;
        private readonly IFieldTypeClassifier _fieldTypeClassifier;
        private readonly ITokenExtractor _tokenExtractor;
        private readonly IPythonExtractorClient _pythonClient;

        public ExtractorService(ILogger<ExtractorService> logger, IPdfTextExtractor pdfExtractor,
            IFieldTypeClassifier fieldTypeClassifier, ITokenExtractor tokenExtractor,
            IEnumParser enumParser, IPythonExtractorClient pythonClient)
        {
            _logger = logger;
            _pdfExtractor = pdfExtractor;
            _fieldTypeClassifier = fieldTypeClassifier;
            _tokenExtractor = tokenExtractor;
            _pythonClient = pythonClient;
        }

        public async Task<ExtractorResponse> ExtractAsync(string label, Dictionary<string, string> schema, string pdf)
        {
            //var stopwatch = Stopwatch.StartNew();

            try
            {
                // var pdfBytes = Convert.FromBase64String(pdfBase64);
                // var extractedText = await _pdfExtractor.ExtractTextAsync(pdfBytes);

                //_logger.LogInformation("Extracted {CharCount} characters from PDF", extractedText.Length);

                // Otimização: usar Span<char> para evitar alocações desnecessárias e criar lista com índices em uma operação
                var lines = pdf.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                //_logger.LogInformation("Total de linhas: {Count}", lines.Length);

                // Otimização: criar lista com índices usando LINQ Select (uma única operação)
                var availableLines = lines.Select((line, index) => (line, originalIndex: index)).ToList();

                var result = new ExtractorResponse
                {
                    Schema = new Dictionary<string, object>(schema.Count)!
                };

                // Otimização: classificar, ordenar e particionar em UMA ÚNICA operação LINQ
                var (enumRegexFields, simpleMultiLineFields) = schema
                    .Select((kvp, index) => (
                        fieldName: kvp.Key,
                        description: kvp.Value,
                        type: _fieldTypeClassifier.ClassifyField(kvp.Key, kvp.Value),
                        originalOrder: index
                    ))
                    .OrderBy(f => GetFieldTypePriority(f.type))
                    .ThenBy(f => f.originalOrder)
                    .Aggregate(
                        (
                            enumRegex: new List<(string fieldName, string description, FieldType type, int originalOrder)>(schema.Count),
                            simpleMultiLine: new List<(string fieldName, string description, FieldType type, int originalOrder)>(schema.Count)
                        ),
                        (acc, field) =>
                        {
                            if (GetFieldTypePriority(field.type) <= 2)
                                acc.enumRegex.Add(field);
                            else
                                acc.simpleMultiLine.Add(field);
                            return acc;
                        }
                    );

                // 🏷️ Preparar keywords para remoção (ANTES do loop principal)
                var keywordsToRemove = simpleMultiLineFields.Count > 0
                    ? simpleMultiLineFields
                        .SelectMany(field => new[] { field.fieldName }
                            .Concat(field.description.Split(new[] { ' ', ',', ';', ':', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(w => w.Length >= 3)))
                        .Distinct()
                        .ToList()
                    : new List<string>(0);

                // 🎯 OTIMIZAÇÃO: Processar extração SEM limpeza de keywords (apenas remover valores)
                var linesToRemove = new HashSet<int>();

                foreach (var field in enumRegexFields)
                {
                    FieldExtractionResult? extracted = null;

                    try
                    {
                        // Otimização: usar switch expression para simplificar lógica de extração
                        var task = field.type switch
                        {
                            FieldType.Enum => _tokenExtractor.ExtractEnumFieldAsync(availableLines, field.fieldName, field.description),
                            FieldType.Date or FieldType.Currency or FieldType.Percentage or FieldType.Phone or
                            FieldType.CPF or FieldType.CNPJ or FieldType.Email or FieldType.CEP or FieldType.Number
                                => _tokenExtractor.ExtractRegexFieldAsync(availableLines, field.fieldName, field.type),
                            _ => null
                        };

                        if (task.HasValue)
                            extracted = await task.Value;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao extrair campo '{FieldName}'", field.fieldName);
                    }

                    // Armazenar resultado
                    if (extracted != null)
                    {
                        result.Schema[field.fieldName] = extracted!.Value;
                        var extractedValue = extracted.Value?.ToString() ?? string.Empty;

                        if (!string.IsNullOrWhiteSpace(extractedValue))
                        {
                            // 🔥 Remover apenas o VALOR extraído das linhas
                            for (int i = availableLines.Count - 1; i >= 0; i--)
                            {
                                // Pular linhas já marcadas para remoção
                                if (linesToRemove.Contains(i))
                                    continue;

                                var lineData = availableLines[i];
                                var currentLine = lineData.line;

                                // Verificar se a linha contém o valor extraído
                                var indexOfValue = currentLine.IndexOf(extractedValue, StringComparison.OrdinalIgnoreCase);
                                if (indexOfValue >= 0)
                                {
                                    // Remover apenas o valor extraído
                                    var sbLine = new StringBuilder(currentLine.Length);
                                    sbLine.Append(currentLine.AsSpan(0, indexOfValue));
                                    sbLine.Append(currentLine.AsSpan(indexOfValue + extractedValue.Length));
                                    currentLine = sbLine.ToString().Trim();

                                    // Verificar se linha ficou vazia
                                    if (string.IsNullOrWhiteSpace(currentLine))
                                    {
                                        linesToRemove.Add(i);
                                    }
                                    else
                                    {
                                        // Atualizar linha na lista
                                        availableLines[i] = (currentLine, lineData.originalIndex);
                                    }

                                    break; // Valor geralmente aparece uma única vez
                                }
                            }
                        }
                    }
                    else
                    {
                        result.Schema[field.fieldName] = null;
                    }
                }

                // Remover linhas marcadas como vazias (do final para o início)
                foreach (var index in linesToRemove.OrderByDescending(x => x))
                {
                    availableLines.RemoveAt(index);
                }

                // 🏷️ OTIMIZAÇÃO: Remover keywords UMA ÚNICA VEZ após todas as extrações
                if (keywordsToRemove.Count > 0)
                {
                    RemoveKeywordsFromAllLines(availableLines, keywordsToRemove);
                }

                // 🏷️ Criar pendingSchema para extração semântica
                var pendingSchema = new Dictionary<string, string>(simpleMultiLineFields.Count);
                foreach (var field in simpleMultiLineFields)
                {
                    pendingSchema[field.fieldName] = field.description;
                }

                //await RemoveLabelsClientAsync(availableLines, pendingFieldNames);

                /////////////////////////////////////

                // Processar linhas em paralelo e acumular resultados
                //var allSemanticResults = new ConcurrentBag<SemanticExtractResponse>();

                // Otimização: remover variável não utilizada
                // var allSemanticResults = new List<SemanticLabelDetectResponse>();

                // Otimização: remover variável não utilizada
                // var parallelOptions = new ParallelOptions
                // {
                //     MaxDegreeOfParallelism = Environment.ProcessorCount
                // };

                // Otimização: construir string usando StringBuilder para melhor performance
                var sb = new StringBuilder(availableLines.Sum(l => l.line.Length + 1));
                for (int i = 0; i < availableLines.Count; i++)
                {
                    sb.Append(availableLines[i].line);
                    if (i < availableLines.Count - 1)
                        sb.Append('\n');
                }
                var cleanedText = sb.ToString();


                //foreach (var lineData in availableLines)
                //{
                //try
                //{
                //    //var semanticResult = await _pythonClient.SemanticExtractAsync(
                //    //    labels: pendingSchema,
                //    //    text: lineData.line
                //    //);

                //    var semanticResult = await _pythonClient.SemanticLabelDetectAsync(
                //        labels: pendingSchema,
                //        text: cleanedText,
                //        topK: 3,
                //        minTokenLength: 3,
                //        similarityThreshold: 0.5f
                //    );

                //    allSemanticResults.Add(semanticResult);
                //}
                //catch (Exception ex)
                //{
                //    //_logger.LogWarning(ex, "Erro ao processar linha sequencialmente: {Line}", lineData.line);
                //}
                //}

                // Juntar todo o texto para processamento final
                //var cleanedText = string.Join("\n", availableLines.Select(l => l.line));

                var smartResult = await _pythonClient.EmbeddingsPythonAsync(
                    label: null,
                    text: cleanedText,
                    schema: pendingSchema,
                    confidenceThreshold: 0.7f,
                    enableGptFallback: true
                );

                // Otimização: reconstruir resultado usando operador coalescente
                var finalResult = new ExtractorResponse
                {
                    Schema = new Dictionary<string, object?>(schema.Count)
                };

                foreach (var kvp in schema)
                {
                    // Prioridade: result.Schema (enum/regex) > smartResult.Fields (semantic) > null
                    finalResult.Schema[kvp.Key] = result.Schema.TryGetValue(kvp.Key, out var regexValue)
                        ? regexValue
                        : smartResult.Fields.TryGetValue(kvp.Key, out var semanticValue)
                            ? semanticValue
                            : null;
                }

                return finalResult;

            }
            catch (Exception)
            {

                throw;
            }
        }

        private async Task RemoveLabelsClientAsync(
            List<(string line, int originalIndex)> availableLines,
            string[] pendingFieldNames)
        {
            try
            {
                // Preparar schema com campos pendentes
                var schema = pendingFieldNames.ToDictionary(
                    name => name,
                    name => $"Campo {name}"
                );

                // Extrair texto dos blocos
                var textBlocks = availableLines.Select(l => l.line).ToList();

                // Chamar Python API (já validado acima)
                var result = await _pythonClient!.ClassifyNliAsync(
                    label: "label_removal",
                    schema: schema,
                    textBlocks: textBlocks
                );

                // Remover blocos identificados como labels
                var linesToRemove = new HashSet<string>(result.LabelsDetected);
                int removedCount = 0;

                for (int i = availableLines.Count - 1; i >= 0; i--)
                {
                    if (linesToRemove.Contains(availableLines[i].line))
                    {
                        availableLines.RemoveAt(i);
                        removedCount++;
                    }
                }

            }
            catch (Exception)
            {
                throw;
            }
        }

        private static int GetFieldTypePriority(FieldType fieldType)
        {
            return fieldType switch
            {
                // Prioridade 1: Enums (valores fixos mais confiáveis)
                FieldType.Enum => 1,

                // Prioridade 2: Padrões regex específicos (alta precisão)
                FieldType.Regex => 2,
                FieldType.CPF => 2,
                FieldType.CNPJ => 2,
                FieldType.Date => 2,
                FieldType.Phone => 2,
                FieldType.Email => 2,
                FieldType.CEP => 2,
                FieldType.Currency => 2,
                FieldType.Percentage => 2,
                FieldType.Number => 2,

                // Prioridade 3: Campos simples genéricos
                FieldType.Simple => 3,

                // Prioridade 4: Campos multi-linha (menos precisos, capturam múltiplas linhas)
                FieldType.MultiLine => 4,

                _ => 99
            };
        }

        /// <summary>
        /// Remove palavras-chave de todas as linhas de uma só vez.
        /// Complexidade: O(m * k) onde m = número de linhas, k = número de keywords.
        /// Muito mais eficiente que chamar RemoveKeywordsOptimized dentro de um loop.
        /// </summary>
        private static void RemoveKeywordsFromAllLines(
            List<(string line, int originalIndex)> lines,
            List<string> keywords)
        {
            if (keywords == null || keywords.Count == 0)
                return;

            // 1️⃣ Normalizar e ordenar keywords por tamanho (uma única vez)
            var normalizedKeywords = keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => RemoveAccents(k.ToLowerInvariant()))
                .OrderByDescending(k => k.Length)
                .Distinct()
                .ToList();

            if (normalizedKeywords.Count == 0)
                return;

            // 2️⃣ Processar todas as linhas
            var linesToRemove = new HashSet<int>();

            for (int i = 0; i < lines.Count; i++)
            {
                var (line, originalIndex) = lines[i];

                // Normalizar linha
                var normalizedLine = RemoveAccents(line.ToLowerInvariant());

                // Remover cada keyword
                foreach (var keyword in normalizedKeywords)
                {
                    normalizedLine = normalizedLine.Replace(keyword, " ");
                }

                // Limpeza final: remover espaços múltiplos
                normalizedLine = Regex.Replace(normalizedLine, @"\s+", " ", RegexOptions.Compiled).Trim();

                // Verificar se linha ficou vazia
                if (string.IsNullOrWhiteSpace(normalizedLine))
                {
                    linesToRemove.Add(i);
                }
                else
                {
                    // Atualizar linha na lista
                    lines[i] = (normalizedLine, originalIndex);
                }
            }

            // 3️⃣ Remover linhas vazias (do final para o início)
            foreach (var index in linesToRemove.OrderByDescending(x => x))
            {
                lines.RemoveAt(index);
            }
        }

        /// <summary>
        /// Remove palavras-chave de um texto de forma otimizada.
        /// Tolerante a erros de digitação (1 caractere a mais ou a menos).
        /// Remove tanto palavras completas quanto substrings.
        /// Complexidade: O(m) onde m = tamanho do texto.
        /// </summary>
        /// <param name="text">Texto a ser limpo</param>
        /// <param name="keywords">Lista de palavras-chave para remover</param>
        /// <returns>Texto limpo sem as palavras-chave</returns>
        private static string RemoveKeywordsOptimized(string text, IEnumerable<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(text) || keywords == null || !keywords.Any())
                return text;

            // 1️⃣ Normalizar texto (remover acentos e converter para minúsculo)
            var normalizedText = RemoveAccents(text.ToLowerInvariant());

            // 2️⃣ Ordenar keywords por tamanho (maiores primeiro) para remover substrings antes
            var sortedKeywords = keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => RemoveAccents(k.ToLowerInvariant()))
                .OrderByDescending(k => k.Length)
                .Distinct()
                .ToList();

            // 3️⃣ Remover cada keyword como substring (não apenas word boundary)
            foreach (var keyword in sortedKeywords)
            {
                // Remove a keyword exata
                normalizedText = normalizedText.Replace(keyword, " ");

                // Opcional: remover variações com ±1 caractere (fuzzy matching)
                // Comentado para melhor performance - descomente se necessário

                // Variação com 1 char a mais no início
                // if (keyword.Length < normalizedText.Length)
                // {
                //     for (int i = 0; i < normalizedText.Length - keyword.Length; i++)
                //     {
                //         var substr = normalizedText.Substring(i, keyword.Length + 1);
                //         if (substr.Substring(1) == keyword)
                //         {
                //             normalizedText = normalizedText.Remove(i, keyword.Length + 1).Insert(i, " ");
                //         }
                //     }
                // }

            }

            // 4️⃣ Limpeza final: remover espaços múltiplos e trim
            normalizedText = Regex.Replace(normalizedText, @"\s+", " ", RegexOptions.Compiled).Trim();

            return normalizedText;
        }

        /// <summary>
        /// Remove acentos de uma string usando normalização Unicode.
        /// Otimizado com StringBuilder.
        /// </summary>
        private static string RemoveAccents(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Normalizar para FormD (decompor caracteres acentuados)
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(normalizedString.Length);

            // Remover marcas diacríticas (acentos)
            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            // Normalizar de volta para FormC
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Gera padrão regex para palavras com 1 caractere faltando em qualquer posição.
        /// </summary>
        private static string GenerateMissingCharPattern(string word)
        {
            if (string.IsNullOrWhiteSpace(word) || word.Length <= 2)
                return string.Empty;

            var patterns = new List<string>();

            // Para cada posição, gerar variação sem aquele caractere
            for (int i = 0; i < word.Length; i++)
            {
                var variant = word.Remove(i, 1);
                if (!string.IsNullOrWhiteSpace(variant))
                {
                    patterns.Add($@"\b{Regex.Escape(variant)}\b");
                }
            }

            return patterns.Any() ? $"({string.Join("|", patterns)})" : string.Empty;
        }
    }
}
