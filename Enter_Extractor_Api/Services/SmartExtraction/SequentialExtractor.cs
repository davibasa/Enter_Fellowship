//using Enter_Extractor_Api.Models.SmartExtraction;
//using System.Diagnostics;

//namespace Enter_Extractor_Api.Services.SmartExtraction
//{
//    public interface ISequentialExtractor
//    {
//        Task<SmartExtractionResponse> ExtractAsync(string extractedText, Dictionary<string, string> schema);
//    }

//    /// <summary>
//    /// Extrator sequencial que respeita a ordem dos campos no schema
//    /// Implementa estratégias adaptativas: Enum, Simple Token, Adaptive Multi-Line
//    /// ⭐ FASE 2: Integrado com Zero-Shot Classification via Python API
//    /// ⭐ FASE 2.5: Integrado com Smart Extract (NER + Embeddings + Cache)
//    /// </summary>
//    public class SequentialExtractor : ISequentialExtractor
//    {
//        private readonly IEnumParser _enumParser;
//        private readonly IFieldTypeClassifier _fieldTypeClassifier;
//        private readonly ISimpleTokenExtractor _simpleTokenExtractor;
//        private readonly IAdaptiveMultiLineExtractor _multiLineExtractor;
//        private readonly INLIValidator? _nliValidator;
//        private readonly IPythonExtractorClient? _pythonClient;
//        private readonly ISmartExtractorClient? _smartExtractorClient;
//        private readonly ILogger<SequentialExtractor> _logger;

//        public SequentialExtractor(
//            IEnumParser enumParser,
//            IFieldTypeClassifier fieldTypeClassifier,
//            ISimpleTokenExtractor simpleTokenExtractor,
//            IAdaptiveMultiLineExtractor multiLineExtractor,
//            ILogger<SequentialExtractor> logger,
//            INLIValidator? nliValidator = null,
//            IPythonExtractorClient? pythonClient = null,
//            ISmartExtractorClient? smartExtractorClient = null)
//        {
//            _enumParser = enumParser;
//            _fieldTypeClassifier = fieldTypeClassifier;
//            _simpleTokenExtractor = simpleTokenExtractor;
//            _multiLineExtractor = multiLineExtractor;
//            _nliValidator = nliValidator;
//            _pythonClient = pythonClient;
//            _smartExtractorClient = smartExtractorClient;
//            _logger = logger;
//        }

//        public async Task<SmartExtractionResponse> ExtractAsync(string extractedText, Dictionary<string, string> schema)
//        {
//            var stopwatch = Stopwatch.StartNew();
//            var traceId = Guid.NewGuid().ToString("N")[..12]; // 12 chars para trace

//            // ============================================================================
//            // FLUXO COMPLETO DE EXTRAÇÃO
//            // ============================================================================
//            // FASE 1: Regex/Enum Extraction (C#) → Remove valores do texto
//            // FASE 2: Zero-Shot NLI (Python /nli/classify) → Remove labels
//            // FASE 2.5: Smart Extract (Python /smart-extract) → NER + Embeddings + Cache
//            // FASE 3: Final Optimizer (C#) → Heurísticas + Decisão Fallback LLM
//            // ============================================================================

//            var nliStatus = _nliValidator != null ? "NLI ATIVO" : "HEURÍSTICAS";
//            _logger.LogInformation("🚀 [TraceId: {TraceId}] Iniciando extração sequencial adaptativa [{Status}]. Total de campos: {Count}",
//                traceId, nliStatus, schema.Count);

//            // Preprocessar: dividir em linhas válidas
//            var lines = extractedText.Split('\n')
//                .Select(l => l.Trim())
//                .Where(l => !string.IsNullOrWhiteSpace(l))
//                .ToArray();

//            _logger.LogInformation("Total de linhas válidas: {Count}", lines.Length);

//            // ⭐ Lista mutável para remover linhas já extraídas (Enum/Regex)
//            var availableLines = lines.Select((line, index) => (line, originalIndex: index)).ToList();

//            var result = new SmartExtractionResponse
//            {
//                Fields = new Dictionary<string, FieldExtractionResult>(),
//                FieldsTotal = schema.Count,
//                TraceId = traceId
//            };

//            int currentLine = 0;
//            var totalConfidence = 0f;
//            var fieldsFound = 0;

//            // ⭐ PRÉ-CLASSIFICAÇÃO: Analisar TODOS os campos antes de iniciar extração
//            _logger.LogInformation("📋 Iniciando pré-classificação de {Count} campos...", schema.Count);

//            var fieldClassifications = new List<(string fieldName, string description, FieldType type, int originalOrder)>();

//            int order = 0;
//            foreach (var kvp in schema)
//            {
//                var fieldName = kvp.Key;
//                var fieldDescription = kvp.Value;
//                var fieldType = _fieldTypeClassifier.ClassifyField(fieldName, fieldDescription);

//                fieldClassifications.Add((fieldName, fieldDescription, fieldType, order++));
//                _logger.LogInformation("  • {FieldName}: {FieldType}", fieldName, fieldType);
//            }

//            // Agrupar por tipo para logging
//            var typeGroups = fieldClassifications
//                .GroupBy(f => f.type)
//                .OrderBy(g => g.Key.ToString())
//                .Select(g => $"{g.Count()}x {g.Key}")
//                .ToList();

//            _logger.LogInformation(
//                "📊 Classificação detalhada: {TypeSummary}",
//                string.Join(", ", typeGroups));

//            // ⭐ REORDENAR: Priorizar campos com padrões específicos (Enum e Regex) primeiro
//            var orderedFields = fieldClassifications.OrderBy(f => GetFieldTypePriority(f.type)).ThenBy(f => f.originalOrder).ToList();

//            _logger.LogInformation("🔄 Ordem de processamento otimizada:");
//            _logger.LogInformation("📊 Total de linhas disponíveis inicialmente: {Count}", availableLines.Count);

//            if (_logger.IsEnabled(LogLevel.Debug))
//            {
//                _logger.LogDebug("📄 Texto inicial:\n{Text}", string.Join("\n", availableLines.Select(l => l.line)));
//            }

//            // Separar campos por prioridade
//            var enumRegexFields = orderedFields.Where(f => GetFieldTypePriority(f.type) <= 2).ToList();
//            var simpleMultiLineFields = orderedFields.Where(f => GetFieldTypePriority(f.type) > 2).ToList();

//            // ============================================================
//            // FASE 1: Regex/Enum Extraction → Remove valores do texto
//            // ============================================================
//            _logger.LogInformation("🎯 [TraceId: {TraceId}] FASE 1: Processando {Count} campos Enum/Regex...",
//                traceId, enumRegexFields.Count);

//            foreach (var field in enumRegexFields)
//            {
//                var fieldName = field.fieldName;
//                var fieldDescription = field.description;
//                var fieldType = field.type;

//                _logger.LogInformation("--- Campo: '{FieldName}' [{FieldType}] (linhas disponíveis: {Count}) ---",
//                    fieldName, fieldType, availableLines.Count);

//                FieldExtractionResult? extracted = null;

//                try
//                {
//                    // ETAPA 2: Aplicar estratégia apropriada (já pré-classificada)
//                    switch (fieldType)
//                    {
//                        case FieldType.Enum:
//                            extracted = await ExtractEnumFieldAsync(availableLines, fieldName, fieldDescription);
//                            break;

//                        // Campos com padrões regex específicos
//                        case FieldType.Date:
//                        case FieldType.Currency:
//                        case FieldType.Percentage:
//                        case FieldType.Phone:
//                        case FieldType.CPF:
//                        case FieldType.CNPJ:
//                        case FieldType.Email:
//                        case FieldType.CEP:
//                        case FieldType.Number:
//                            extracted = ExtractRegexFieldAsync(availableLines, fieldName, fieldType);
//                            break;

//                        case FieldType.Simple:
//                            // Converter availableLines para array simples, ajustando currentLine
//                            var simpleLines = availableLines.Select(l => l.line).ToArray();
//                            (extracted, currentLine) = await _simpleTokenExtractor.ExtractSimpleFieldAsync(
//                                simpleLines,
//                                currentLine,
//                                fieldName,
//                                fieldDescription);

//                            // Ajustar currentLine se necessário (não pode exceder linhas disponíveis)
//                            if (currentLine >= availableLines.Count)
//                                currentLine = availableLines.Count;
//                            break;

//                        case FieldType.MultiLine:
//                            // Converter availableLines para array simples, ajustando currentLine
//                            var multiLines = availableLines.Select(l => l.line).ToArray();
//                            (extracted, currentLine) = await _multiLineExtractor.ExtractMultiLineFieldAsync(
//                                multiLines,
//                                currentLine,
//                                fieldName,
//                                fieldDescription);

//                            // Ajustar currentLine se necessário
//                            if (currentLine >= availableLines.Count)
//                                currentLine = availableLines.Count;
//                            break;
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Erro ao extrair campo '{FieldName}'", fieldName);
//                }

//                // ETAPA 3: Registrar resultado
//            //    if (extracted != null && extracted.Found)
//            //    {
//            //        result.Fields[fieldName] = extracted;
//            //        totalConfidence += extracted.Confidence;
//            //        fieldsFound++;
//            //        _logger.LogInformation("✓ Extraído [{Method}]: '{Value}' (confidence: {Confidence:F2})",
//            //            extracted.Method, extracted.Value, extracted.Confidence);

//            //        // Mostrar texto restante após remoção (apenas para Enum/Regex)
//            //        if (fieldType == FieldType.Enum ||
//            //            fieldType == FieldType.Date || fieldType == FieldType.Currency ||
//            //            fieldType == FieldType.Percentage || fieldType == FieldType.Phone ||
//            //            fieldType == FieldType.CPF || fieldType == FieldType.CNPJ ||
//            //            fieldType == FieldType.Email || fieldType == FieldType.CEP ||
//            //            fieldType == FieldType.Number)
//            //        {
//            //            if (_logger.IsEnabled(LogLevel.Debug))
//            //            {
//            //                _logger.LogDebug("📄 Texto após remoção:\n{Text}",
//            //                    string.Join("\n", availableLines.Select(l => l.line)));
//            //            }
//            //        }
//            //    }
//            //    else
//            //    {
//            //        result.Fields[fieldName] = new FieldExtractionResult
//            //        {
//            //            Value = null,
//            //        };
//            //        _logger.LogWarning("✗ Campo '{FieldName}' não encontrado", fieldName);
//            //    }
//            //}

//            // ============================================================
//            // FASE 2: Zero-Shot NLI → Remove labels (Python /nli/classify)
//            // ============================================================
//            _logger.LogInformation("🏷️ [TraceId: {TraceId}] FASE 2: Removendo labels com Zero-Shot NLI...", traceId);
//            _logger.LogInformation("Linhas antes da limpeza de labels: {Count}", availableLines.Count);

//            if (_nliValidator != null)
//            {
//                // ⭐ Obter lista de campos ainda não preenchidos (Simple/MultiLine)
//                var pendingFieldNames = simpleMultiLineFields
//                    .Select(f => f.fieldName)
//                    .ToArray();

//                await RemoveLabelsWithNLIAsync(availableLines, pendingFieldNames);
//            }
//            else
//            {
//                _logger.LogWarning("⚠️ NLI não disponível. Pulando remoção automática de labels.");
//            }

//            _logger.LogInformation("Linhas após limpeza de labels: {Count}", availableLines.Count);

//            if (_logger.IsEnabled(LogLevel.Debug))
//            {
//                _logger.LogDebug("📄 Texto após remoção de labels:\n{Text}",
//                    string.Join("\n", availableLines.Select(l => l.line)));
//            }

//            // ============================================================
//            // FASE 2.5: Smart Extract → NER + Embeddings + Cache (Python /smart-extract)
//            // ============================================================
//            // Calcular confiança dos campos já extraídos
//            //var phase1Confidence = result.Fields.Values
//            //    .Where(f => f.Value != null)
//            //    .Average(f => (double)f.Confidence);

//            var pendingFieldsCount = simpleMultiLineFields.Count;

//            // Só chama FASE 2.5 se:
//            // 1. Cliente Python disponível
//            // 2. Confiança da FASE 1 < 0.7 OU há muitos campos pendentes (> 3)
//            // 3. Há texto disponível após limpeza de labels
//            if (_smartExtractorClient != null &&
//                availableLines.Any())
//            {
//                _logger.LogInformation("🧠 [TraceId: {TraceId}] FASE 2.5: Usando Smart Extract (Python)...", traceId);
//                _logger.LogInformation("  📦 Campos pendentes: {Count}", pendingFieldsCount);

//                try
//                {
//                    // ⭐ IMPORTANTE: Montar schema PRESERVANDO A ORDEM dos campos
//                    // Python irá processar SEQUENCIALMENTE na ordem recebida
//                    var pendingSchema = new Dictionary<string, string>();
//                    foreach (var field in simpleMultiLineFields)
//                    {
//                        pendingSchema[field.fieldName] = field.description;
//                    }

//                    _logger.LogInformation("  📋 Ordem de extração: {Fields}",
//                        string.Join(" → ", pendingSchema.Keys));

//                    // Texto limpo (pós-FASE 2)
//                    var cleanedText = string.Join("\n", availableLines.Select(l => l.line));

//                    // Chamar Python Smart Extract (SEQUENCIAL com GPT Fallback habilitado)
//                    var smartResult = await _smartExtractorClient.SmartExtractPhase25Async(
//                        label: null, // Pode adicionar label se disponível
//                        text: cleanedText,
//                        schema: pendingSchema,
//                        confidenceThreshold: 0.7f,
//                        enableGptFallback: true  // ✅ GPT Fallback HABILITADO
//                    );

//                    // Processar resultados e preencher campos
//                    int smartExtractedCount = 0;
//                    foreach (var (fieldName, extraction) in smartResult.Fields)
//                    {
//                        // Só aceita se confiança >= 0.7
//                        if (extraction.Confidence >= 0.7f && !string.IsNullOrWhiteSpace(extraction.Value))
//                        {
//                            result.Fields[fieldName] = new FieldExtractionResult
//                            {
//                                Value = extraction.Value,
//                                Confidence = extraction.Confidence,
//                                Method = $"smart_extract_{extraction.Method}",
//                                LineIndex = extraction.LineIndex ?? -1
//                            };

//                            smartExtractedCount++;
//                            fieldsFound++;
//                            totalConfidence += extraction.Confidence;

//                            // Remover linhas usadas
//                            if (extraction.LineIndex.HasValue &&
//                                extraction.LineIndex.Value >= 0 &&
//                                extraction.LineIndex.Value < availableLines.Count)
//                            {
//                                availableLines.RemoveAt(extraction.LineIndex.Value);
//                            }

//                            _logger.LogInformation("  ✅ '{FieldName}': '{Value}' (conf: {Confidence:F3}, method: {Method})",
//                                fieldName,
//                                extraction.Value.Length > 50 ? extraction.Value.Substring(0, 50) + "..." : extraction.Value,
//                                extraction.Confidence,
//                                extraction.Method);
//                        }
//                    }

//                    _logger.LogInformation("✅ FASE 2.5 completa: {Count} campos extraídos | Tempo: {Time}ms | Cache hit: {CacheHit}",
//                        smartExtractedCount, smartResult.ProcessingTimeMs, smartResult.CacheHit);

//                    // Atualizar lista de campos pendentes (remover os já extraídos)
//                    simpleMultiLineFields = simpleMultiLineFields
//                        .Where(f => !smartResult.Fields.ContainsKey(f.fieldName) ||
//                                    smartResult.Fields[f.fieldName].Confidence < 0.7f)
//                        .ToList();

//                    _logger.LogInformation("  📝 Campos ainda pendentes: {Count}", simpleMultiLineFields.Count);
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogWarning(ex, "⚠️ Erro na FASE 2.5 (Smart Extract). Continuando com FASE 3...");
//                }
//            }
//            else
//            {
//                var reason = _smartExtractorClient == null ? "Cliente Python não disponível" :
//                            !availableLines.Any() ? "Sem texto disponível" :
//                            $"Confiança FASE 1 alta ({phase1Confidence:F3}) e poucos campos pendentes ({pendingFieldsCount})";

//                _logger.LogInformation("⏭️ Pulando FASE 2.5: {Reason}", reason);
//            }

//            // ============================================================
//            // FASE 3: Final Optimizer → Heurísticas + Limpeza final
//            // ============================================================
//            _logger.LogInformation("📝 [TraceId: {TraceId}] FASE 3: Processando {Count} campos Simple/MultiLine...",
//                traceId, simpleMultiLineFields.Count);

//            foreach (var field in simpleMultiLineFields)
//            {
//                var fieldName = field.fieldName;
//                var fieldDescription = field.description;
//                var fieldType = field.type;

//                _logger.LogInformation("--- Campo: '{FieldName}' [{FieldType}] (linhas disponíveis: {Count}) ---",
//                    fieldName, fieldType, availableLines.Count);

//                FieldExtractionResult? extracted = null;

//                try
//                {
//                    switch (fieldType)
//                    {
//                        case FieldType.Simple:
//                            // Converter availableLines para array simples
//                            var simpleLines = availableLines.Select(l => l.line).ToArray();
//                            (extracted, currentLine) = await _simpleTokenExtractor.ExtractSimpleFieldAsync(
//                                simpleLines,
//                                currentLine,
//                                fieldName,
//                                fieldDescription);

//                            // ⭐ REMOVER valor extraído
//                            if (extracted != null && extracted.Found && !string.IsNullOrEmpty(extracted.Value))
//                            {
//                                RemoveExtractedValueFromLines(availableLines, extracted.Value);
//                            }

//                            // Ajustar currentLine se necessário
//                            if (currentLine >= availableLines.Count)
//                                currentLine = availableLines.Count;
//                            break;

//                        case FieldType.MultiLine:
//                            // Converter availableLines para array simples
//                            var multiLines = availableLines.Select(l => l.line).ToArray();
//                            (extracted, currentLine) = await _multiLineExtractor.ExtractMultiLineFieldAsync(
//                                multiLines,
//                                currentLine,
//                                fieldName,
//                                fieldDescription);

//                            // ⭐ REMOVER valor extraído (pode ser múltiplas linhas)
//                            if (extracted != null && extracted.Found && !string.IsNullOrEmpty(extracted.Value))
//                            {
//                                RemoveExtractedValueFromLines(availableLines, extracted.Value);
//                            }

//                            // Ajustar currentLine se necessário
//                            if (currentLine >= availableLines.Count)
//                                currentLine = availableLines.Count;
//                            break;
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Erro ao extrair campo '{FieldName}'", fieldName);
//                }

//                // Registrar resultado
//                if (extracted != null && extracted.Found)
//                {
//                    result.Fields[fieldName] = extracted;
//                    totalConfidence += extracted.Confidence;
//                    fieldsFound++;
//                    _logger.LogInformation("✓ Extraído [{Method}]: '{Value}' (confidence: {Confidence:F2})",
//                        extracted.Method, extracted.Value, extracted.Confidence);
//                }
//                else
//                {
//                    result.Fields[fieldName] = new FieldExtractionResult
//                    {
//                        Value = null,
//                        Confidence = 0,
//                        Method = "not_found",
//                        LineIndex = -1,
//                        Found = false
//                    };
//                    _logger.LogWarning("✗ Campo '{FieldName}' não encontrado", fieldName);
//                }
//            }

//            stopwatch.Stop();

//            // Preencher metadados da resposta
//            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
//            result.FieldsFound = fieldsFound;
//            result.TotalConfidence = fieldsFound > 0 ? totalConfidence / fieldsFound : 0f;

//            // ============================================================
//            // DECISÃO DE FALLBACK LLM
//            // ============================================================
//            // TODO: Implementar chamada ao Python /llm/fallback se confidence < threshold
//            // Threshold dinâmico pode ser obtido via endpoint /metrics/threshold/{label}
//            var confidenceThreshold = 0.7f; // Default, pode ser dinâmico

//            if (result.TotalConfidence < confidenceThreshold)
//            {
//                _logger.LogWarning("⚠️ [TraceId: {TraceId}] Confiança média {Confidence:F3} < threshold {Threshold:F3}",
//                    traceId, result.TotalConfidence, confidenceThreshold);
//                _logger.LogWarning("   💡 Considere implementar fallback LLM via Python /llm/fallback");
//                // TODO: var fallbackResult = await _llmFallbackClient.CorrectAsync(label, result.Fields, schema);
//            }

//            _logger.LogInformation(
//                "=== [TraceId: {TraceId}] Extração completa em {TimeMs}ms === (Encontrados: {Found}/{Total}, Confidence média: {Avg:F2})",
//                traceId,
//                stopwatch.ElapsedMilliseconds,
//                fieldsFound,
//                result.FieldsTotal,
//                result.TotalConfidence
//            );

//            return result;
//        }

//        /// <summary>
//        /// Define a prioridade de processamento por tipo de campo
//        /// Menor = maior prioridade (processado primeiro)
//        /// </summary>
//        private static int GetFieldTypePriority(FieldType fieldType)
//        {
//            return fieldType switch
//            {
//                // Prioridade 1: Enums (valores fixos mais confiáveis)
//                FieldType.Enum => 1,

//                // Prioridade 2: Padrões regex específicos (alta precisão)
//                FieldType.CPF => 2,
//                FieldType.CNPJ => 2,
//                FieldType.Date => 2,
//                FieldType.Phone => 2,
//                FieldType.Email => 2,
//                FieldType.CEP => 2,
//                FieldType.Currency => 2,
//                FieldType.Percentage => 2,
//                FieldType.Number => 2,

//                // Prioridade 3: Campos simples genéricos
//                FieldType.Simple => 3,

//                // Prioridade 4: Campos multi-linha (menos precisos, capturam múltiplas linhas)
//                FieldType.MultiLine => 4,

//                _ => 99
//            };
//        }

//        private async Task<FieldExtractionResult?> ExtractEnumFieldAsync(
//            List<(string line, int originalIndex)> availableLines,
//            string fieldName,
//            string fieldDescription)
//        {
//            var enumValues = _enumParser.ExtractEnumValues(fieldDescription);

//            if (enumValues.Count == 0)
//                return null;

//            _logger.LogInformation("Enum values detectados: {Values}", string.Join(", ", enumValues));

//            // Buscar em TODAS as linhas disponíveis (não sequencial)
//            var linesArray = availableLines.Select(l => l.line).ToArray();
//            var enumMatch = _enumParser.FindBestMatch(linesArray, 0, enumValues);

//            if (enumMatch.Found)
//            {
//                // Se NLI disponível, validar enum
//                float confidence = 0.95f; // Base

//                // ⭐ REMOVER apenas o valor extraído da linha
//                var extractedLine = availableLines[enumMatch.LineIndex];

//                // Remover o valor encontrado da linha
//                var lineAfterRemoval = extractedLine.line.Replace(enumMatch.Value, "").Trim();

//                // Se a linha ficou vazia, remover completamente
//                if (string.IsNullOrWhiteSpace(lineAfterRemoval))
//                {
//                    availableLines.RemoveAt(enumMatch.LineIndex);
//                    _logger.LogInformation(
//                        "🗑️ Valor '{Value}' removido. Linha ficou vazia e foi removida. Restam {Count} linhas",
//                        enumMatch.Value,
//                        availableLines.Count);
//                }
//                else
//                {
//                    // Atualizar a linha com o conteúdo restante
//                    availableLines[enumMatch.LineIndex] = (lineAfterRemoval, extractedLine.originalIndex);
//                    _logger.LogInformation(
//                        "✂️ Valor '{Value}' removido. Antes: '{Before}' | Depois: '{After}'",
//                        enumMatch.Value,
//                        extractedLine.line,
//                        lineAfterRemoval);
//                }

//                var result = new FieldExtractionResult
//                {
//                    Value = enumMatch.Value,
//                    Confidence = confidence,
//                    Method = _nliValidator != null ? "enum_match_nli" : "enum_match_heuristic",
//                    LineIndex = extractedLine.originalIndex,
//                    Found = true
//                };

//                return result;
//            }

//            return null;
//        }

//        private FieldExtractionResult? ExtractRegexFieldAsync(
//            List<(string line, int originalIndex)> availableLines,
//            string fieldName,
//            FieldType fieldType)
//        {
//            _logger.LogInformation("Aplicando extração por regex para tipo: {FieldType}", fieldType);

//            // Buscar padrões apropriados para o tipo de campo
//            var patterns = RegexPatternBank.GetPatternsForFieldType(fieldType);

//            if (patterns.Length == 0)
//            {
//                _logger.LogWarning("Nenhum padrão regex disponível para tipo {FieldType}", fieldType);
//                return null;
//            }

//            _logger.LogInformation("Padrões a testar: {Patterns}", string.Join(", ", patterns));

//            // ⭐ Buscar em TODAS as linhas disponíveis (não sequencial)
//            for (int i = 0; i < availableLines.Count; i++)
//            {
//                var lineData = availableLines[i];

//                foreach (var patternName in patterns)
//                {
//                    // ⭐ IMPORTANTE: Recriar array SEMPRE para pegar linhas atualizadas
//                    var linesArray = availableLines.Select(l => l.line).ToArray();
//                    var matches = RegexPatternBank.ApplyPattern(linesArray, i, patternName);

//                    if (matches.Any())
//                    {
//                        var firstMatch = matches.First();

//                        _logger.LogInformation(
//                            "✓ Match encontrado na linha {Line} (original: {OriginalIndex}) com padrão '{Pattern}': '{Value}'",
//                            i,
//                            lineData.originalIndex,
//                            patternName,
//                            firstMatch.Value);

//                        // ⭐ REMOVER apenas o valor extraído da linha
//                        var lineAfterRemoval = lineData.line.Replace(firstMatch.Value, "").Trim();

//                        // Se a linha ficou vazia, remover completamente
//                        if (string.IsNullOrWhiteSpace(lineAfterRemoval))
//                        {
//                            availableLines.RemoveAt(i);
//                            _logger.LogInformation(
//                                "🗑️ Valor '{Value}' removido. Linha ficou vazia e foi removida. Restam {Count} linhas",
//                                firstMatch.Value,
//                                availableLines.Count);
//                        }
//                        else
//                        {
//                            // Atualizar a linha com o conteúdo restante
//                            availableLines[i] = (lineAfterRemoval, lineData.originalIndex);
//                            _logger.LogInformation(
//                                "✂️ Valor '{Value}' removido. Antes: '{Before}' | Depois: '{After}'",
//                                firstMatch.Value,
//                                lineData.line,
//                                lineAfterRemoval);
//                        }

//                        var result = new FieldExtractionResult
//                        {
//                            Value = firstMatch.Value,
//                            Confidence = 0.90f, // Alta confiança para regex match
//                            Method = $"regex_{patternName}",
//                            LineIndex = lineData.originalIndex,
//                            Found = true
//                        };

//                        return result;
//                    }
//                }
//            }

//            _logger.LogWarning("Nenhum match encontrado com padrões regex para campo '{FieldName}'", fieldName);
//            return null;
//        }

//        /// <summary>
//        /// Remove linhas que são labels (usando Zero-Shot NLI via Python API)
//        /// ⭐ OTIMIZADO: Envia todos os blocos de uma vez em vez de linha por linha
//        /// </summary>
//        private async Task RemoveLabelsWithNLIAsync(
//            List<(string line, int originalIndex)> availableLines,
//            string[] pendingFieldNames)
//        {
//            // ⭐ PRIORIDADE 1: Usar novo cliente Python otimizado (batch + cache)
//            if (_pythonClient != null)
//            {
//                await RemoveLabelsWithPythonClientAsync(availableLines, pendingFieldNames);
//                return;
//            }

//            // ⭐ FALLBACK: Usar NLI validator antigo (linha por linha)
//            if (_nliValidator != null)
//            {
//                await RemoveLabelsWithNLIValidatorAsync(availableLines, pendingFieldNames);
//                return;
//            }

//            _logger.LogWarning("⚠️ Nem Python client nem NLI validator disponíveis. Pulando remoção de labels.");
//        }

//        /// <summary>
//        /// Remove labels usando novo Python client (otimizado com batch + cache)
//        /// </summary>
//        private async Task RemoveLabelsWithPythonClientAsync(
//            List<(string line, int originalIndex)> availableLines,
//            string[] pendingFieldNames)
//        {
//            _logger.LogInformation("🚀 Usando Python API otimizada para remoção de labels...");
//            _logger.LogInformation("📦 Enviando {Count} blocos em batch", availableLines.Count);

//            try
//            {
//                // Preparar schema com campos pendentes
//                var schema = pendingFieldNames.ToDictionary(
//                    name => name,
//                    name => $"Campo {name}"
//                );

//                // Extrair texto dos blocos
//                var textBlocks = availableLines.Select(l => l.line).ToList();

//                // Chamar Python API (já validado acima)
//                var result = await _pythonClient!.ClassifyNliAsync(
//                    label: "label_removal",
//                    schema: schema,
//                    textBlocks: textBlocks
//                );

//                // Remover blocos identificados como labels
//                var linesToRemove = new HashSet<string>(result.LabelsDetected);
//                int removedCount = 0;

//                for (int i = availableLines.Count - 1; i >= 0; i--)
//                {
//                    if (linesToRemove.Contains(availableLines[i].line))
//                    {
//                        _logger.LogInformation("🗑️ Removendo label: '{Line}'", availableLines[i].line);
//                        availableLines.RemoveAt(i);
//                        removedCount++;
//                    }
//                }

//                _logger.LogInformation(
//                    "✅ Python API: {Removed} labels removidas de {Total} blocos | Cache hits: {CacheHits} | Tempo: {Time}ms",
//                    removedCount,
//                    result.TotalBlocks,
//                    result.CacheHits,
//                    result.ProcessingTimeMs);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "❌ Erro ao usar Python API. Tentando fallback para NLI validator...");

//                // Fallback para método antigo se Python API falhar
//                if (_nliValidator != null)
//                {
//                    await RemoveLabelsWithNLIValidatorAsync(availableLines, pendingFieldNames);
//                }
//            }
//        }

//        /// <summary>
//        /// Remove labels usando NLI validator antigo (linha por linha)
//        /// </summary>
//        private async Task RemoveLabelsWithNLIValidatorAsync(
//            List<(string line, int originalIndex)> availableLines,
//            string[] pendingFieldNames)
//        {
//            if (_nliValidator == null)
//            {
//                _logger.LogWarning("⚠️ NLI validator não disponível.");
//                return;
//            }

//            _logger.LogInformation("🔍 Usando NLI validator antigo (linha por linha)...");

//            var linesToRemove = new List<int>();

//            _logger.LogInformation("🔍 Analisando {Count} linhas para identificar labels...", availableLines.Count);
//            _logger.LogInformation("📋 Campos pendentes que serão usados como categorias: {Fields}",
//                string.Join(", ", pendingFieldNames));

//            // Analisar cada linha para ver se é uma label ou dado
//            for (int i = 0; i < availableLines.Count; i++)
//            {
//                var lineData = availableLines[i];
//                var line = lineData.line;

//                // Pular linhas muito curtas (provavelmente não são labels problemáticas)
//                if (line.Length < 3)
//                    continue;

//                try
//                {
//                    // ⭐ Usar nomes dos campos pendentes como categorias + categoria genérica de "dado"
//                    var candidateLabels = pendingFieldNames
//                        .Select(name => $"label do campo '{name}'")
//                        .Append("valor ou dado extraído")
//                        .ToArray();

//                    var classificationResult = await _nliValidator.ClassifyTextAsync(line, candidateLabels);

//                    // Se classificou como qualquer label de campo (não como "dado")
//                    var isLabel = classificationResult.PredictedLabel != "valor ou dado extraído"
//                                  && classificationResult.Confidence > 0.30f;

//                    if (isLabel)
//                    {
//                        _logger.LogInformation(
//                            "🏷️ Label detectada: '{Line}' → {BestLabel} (score: {Score:F2})",
//                            line.Length > 50 ? line.Substring(0, 50) + "..." : line,
//                            classificationResult.PredictedLabel,
//                            classificationResult.Confidence);
//                        linesToRemove.Add(i);
//                    }
//                    else
//                    {
//                        _logger.LogDebug(
//                            "✓ Dado mantido: '{Line}' → {BestLabel} (score: {Score:F2})",
//                            line.Length > 50 ? line.Substring(0, 50) + "..." : line,
//                            classificationResult.PredictedLabel,
//                            classificationResult.Confidence);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogWarning(ex, "Erro ao analisar linha {Index} com NLI: '{Line}'", i, line);
//                }
//            }

//            // Remover linhas identificadas como labels (do final para o início para não afetar índices)
//            foreach (var index in linesToRemove.OrderByDescending(x => x))
//            {
//                var removedLine = availableLines[index];
//                availableLines.RemoveAt(index);
//                _logger.LogInformation("🗑️ Label removida: '{Line}'", removedLine.line);
//            }

//            _logger.LogInformation("✅ Total de labels removidas: {Count}", linesToRemove.Count);
//        }

//        /// <summary>
//        /// Remove o valor extraído de todas as linhas disponíveis
//        /// </summary>
//        private void RemoveExtractedValueFromLines(List<(string line, int originalIndex)> availableLines, string extractedValue)
//        {
//            if (string.IsNullOrWhiteSpace(extractedValue))
//                return;

//            var linesToRemove = new List<int>();

//            // Buscar o valor em todas as linhas
//            for (int i = 0; i < availableLines.Count; i++)
//            {
//                var lineData = availableLines[i];

//                // Verificar se a linha contém o valor extraído
//                if (lineData.line.Contains(extractedValue, StringComparison.OrdinalIgnoreCase))
//                {
//                    // Remover o valor da linha
//                    var lineAfterRemoval = lineData.line.Replace(extractedValue, "").Trim();

//                    if (string.IsNullOrWhiteSpace(lineAfterRemoval))
//                    {
//                        // Linha ficou vazia, marcar para remoção
//                        linesToRemove.Add(i);
//                        _logger.LogDebug("🗑️ Linha '{Line}' ficará vazia após remoção, será removida", lineData.line);
//                    }
//                    else
//                    {
//                        // Atualizar linha com conteúdo restante
//                        availableLines[i] = (lineAfterRemoval, lineData.originalIndex);
//                        _logger.LogDebug("✂️ Removido '{Value}' da linha. Antes: '{Before}' | Depois: '{After}'",
//                            extractedValue, lineData.line, lineAfterRemoval);
//                    }
//                }
//            }

//            // Remover linhas vazias (do final para o início)
//            foreach (var index in linesToRemove.OrderByDescending(x => x))
//            {
//                availableLines.RemoveAt(index);
//            }

//            if (linesToRemove.Count > 0)
//            {
//                _logger.LogInformation("🗑️ {Count} linha(s) removida(s) após extração", linesToRemove.Count);
//            }
//        }
//    }
//}
