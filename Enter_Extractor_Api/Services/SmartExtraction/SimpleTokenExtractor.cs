//using Enter_Extractor_Api.Models.SmartExtraction;
//using System.Runtime.CompilerServices;

//namespace Enter_Extractor_Api.Services.SmartExtraction;

//public interface ISimpleTokenExtractor
//{
//    Task<(FieldExtractionResult? result, int newLineIndex)> ExtractSimpleFieldAsync(
//        string[] lines,
//        int currentLine,
//        string fieldName,
//        string fieldDescription);
//}

//public class SimpleTokenExtractor : ISimpleTokenExtractor
//{
//    private const float MIN_CONFIDENCE = 0.80f;
//    private const int MAX_LINES_TO_SEARCH = 5;

//    private readonly ITextTokenizer _tokenizer;
//    private readonly ILabelDetector _labelDetector;
//    private readonly INLIValidator? _nliValidator;

//    public SimpleTokenExtractor(
//        ITextTokenizer tokenizer,
//        ILabelDetector labelDetector,
//        INLIValidator? nliValidator = null)
//    {
//        _tokenizer = tokenizer;
//        _labelDetector = labelDetector;
//        _nliValidator = nliValidator;
//    }

//    public async Task<(FieldExtractionResult? result, int newLineIndex)> ExtractSimpleFieldAsync(
//        string[] lines,
//        int currentLine,
//        string fieldName,
//        string fieldDescription)
//    {
//        // Procurar nas próximas N linhas
//        for (int i = currentLine; i < Math.Min(currentLine + MAX_LINES_TO_SEARCH, lines.Length); i++)
//        {
//            var line = lines[i];

//            // ⭐ FASE 2: Pular labels usando Zero-Shot NLI (async)
//            //bool isLabel = await _labelDetector.IsLabelLineAsync(line);
//            //if (isLabel)
//            //    continue;

//            // Tokenizar linha (pode ter múltiplos valores)
//            var tokens = _tokenizer.TokenizeLine(line.AsSpan());

//            // Testar cada token individualmente
//            foreach (var token in tokens)
//            {
//                // Validação com Zero-Shot (se disponível) ou heurística
//                var confidence = await ValidateTokenAsync(fieldName, fieldDescription, token);

//                if (confidence >= MIN_CONFIDENCE)
//                {
//                    var result = new FieldExtractionResult
//                    {
//                        Value = token,
//                        Confidence = confidence,
//                        Method = _nliValidator != null ? "simple_token_nli" : "simple_token_heuristic",
//                        LineIndex = i,
//                        Found = true
//                    };

//                    return (result, i + 1);
//                }
//            }
//        }

//        return (null, currentLine);
//    }

//    /// <summary>
//    /// Valida token usando Zero-Shot (se disponível) ou heurística
//    /// </summary>
//    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
//    private async Task<float> ValidateTokenAsync(string fieldName, string fieldDescription, string token)
//    {
//        // Se NLI disponível, usar
//        if (_nliValidator != null)
//        {
//            try
//            {
//                return await _nliValidator.ValidateSimpleTokenAsync(fieldName, fieldDescription, token);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[WARN] NLI falhou, usando heurística fallback: {ex.Message}");
//            }
//        }

//        // Fallback: heurística
//        return ValidateTokenHeuristic(fieldName, fieldDescription, token);
//    }

//    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
//    private static float ValidateTokenHeuristic(string fieldName, string fieldDescription, string token)
//    {
//        // Heurística simples baseada em keywords

//        var keywords = ExtractKeywords(fieldName, fieldDescription);

//        // Nome: deve começar com letra maiúscula e ter pelo menos 2 palavras ou D'ARC
//        if (keywords.Contains("nome"))
//        {
//            if (char.IsUpper(token[0]) && (token.Contains(' ') || token.Contains("'")))
//                return 0.85f;
//        }

//        // Inscrição/Número: deve ser número
//        if (keywords.Contains("inscricao") || keywords.Contains("numero"))
//        {
//            if (token.All(char.IsDigit))
//                return 0.85f;
//        }

//        // Seccional/Estado: sigla de 2 letras maiúsculas
//        if (keywords.Contains("seccional") || keywords.Contains("estado") || keywords.Contains("uf"))
//        {
//            if (token.Length == 2 && token.All(char.IsUpper))
//                return 0.85f;
//        }

//        // CPF: formato XXX.XXX.XXX-XX
//        if (keywords.Contains("cpf"))
//        {
//            if (token.Replace(".", "").Replace("-", "").Length == 11)
//                return 0.85f;
//        }

//        // CNPJ: formato XX.XXX.XXX/XXXX-XX
//        if (keywords.Contains("cnpj"))
//        {
//            if (token.Replace(".", "").Replace("/", "").Replace("-", "").Length == 14)
//                return 0.85f;
//        }

//        // CEP: 8 dígitos
//        if (keywords.Contains("cep"))
//        {
//            if (token.Replace("-", "").Length == 8 && token.Replace("-", "").All(char.IsDigit))
//                return 0.85f;
//        }

//        return 0.5f; // Confiança baixa
//    }

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static HashSet<string> ExtractKeywords(string fieldName, string fieldDescription)
//    {
//        var text = $"{fieldName} {fieldDescription}".ToLowerInvariant();
//        var keywords = new HashSet<string>(StringComparer.Ordinal);

//        var terms = new[]
//        {
//            "nome", "inscricao", "numero", "seccional", "estado", "uf",
//            "cpf", "cnpj", "cep", "telefone", "email"
//        };

//        foreach (var term in terms)
//        {
//            if (text.Contains(term, StringComparison.Ordinal))
//                keywords.Add(term);
//        }

//        return keywords;
//    }
//}
