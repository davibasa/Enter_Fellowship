using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace Enter_Extractor_Api.Services.SmartExtraction
{
    /// <summary>
    /// Banco de padrões Regex categorizados por tipo de dado
    /// </summary>
    public static partial class RegexPatternBank
    {
        // Cache de regex compiladas para evitar recompilação
        private static readonly Dictionary<string, Regex> _compiledRegexCache = new();
        private static readonly object _cacheLock = new();
        /// <summary>
        /// Padrões regex disponíveis, categorizados por tipo
        /// </summary>
        public static readonly Dictionary<string, string> Patterns = new()
        {
            // Nomes e texto
            ["nome_proprio"] = @"^[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ][A-ZÀ-Úa-zà-ú\s']+$",
            ["texto_livre"] = @".+",

            // Números
            ["numero_simples"] = @"\d+",
            ["numero_com_letra"] = @"(\d+)\s*([A-Z]{2,})",
            ["numero_decimal"] = @"\d+[.,]\d+",

            // Documentos
            ["cpf"] = @"\d{3}\.?\d{3}\.?\d{3}-?\d{2}",
            ["cnpj"] = @"\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}",
            ["rg"] = @"\d{1,2}\.?\d{3}\.?\d{3}-?[0-9X]",

            // Endereço e localização
            ["endereco"] = @"[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ][A-ZÀ-Úa-zà-ú\s]+,\s*[NnºNo°]\s*\d+.*",
            ["cep"] = @"\d{5}-?\d{3}",
            ["sigla_estado"] = @"\b[A-Z]{2}\b",
            ["cidade_estado"] = @"[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ][A-ZÀ-Úa-zà-ú\s]+-\s*[A-Z]{2}",

            // Contato
            ["telefone"] = @"\(?\d{2}\)?\s*\d{4,5}-?\d{4}",
            ["email"] = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",

            // Data e hora
            ["data_br"] = @"\d{2}/\d{2}/\d{4}",
            ["data_iso"] = @"\d{4}-\d{2}-\d{2}",
            ["data_compacta"] = @"\d{2}/\d{2}/\d{2}",
            ["hora"] = @"\d{2}:\d{2}(?::\d{2})?",

            // Valores monetários
            ["valor_br"] = @"R\$\s*\d{1,3}(?:\.\d{3})*(?:,\d{2})?",
            ["valor_numerico"] = @"\d{1,3}(?:\.\d{3})*(?:,\d{2})?",

            // Percentuais
            ["percentual_simbolo"] = @"\d{1,3}(?:[.,]\d{1,2})?%",
            ["percentual_decimal"] = @"0[.,]\d{1,4}",

            // Status e situação
            ["situacao"] = @"SITUA[ÇC][ÃA]O\s+(REGULAR|IRREGULAR|SUSPENS[OA]|ATIVA?|INATIVA?)",
            ["status"] = @"\b(ATIVO|INATIVO|PENDENTE|APROVADO|REJEITADO|CANCELADO)\b",
        };

        /// <summary>
        /// Mapeamento de FieldType para padrões regex aplicáveis
        /// </summary>
        public static readonly Dictionary<Enter_Extractor_Api.Models.SmartExtraction.FieldType, string[]> TypePatterns = new()
        {
            [Enter_Extractor_Api.Models.SmartExtraction.FieldType.Date] = new[] { "data_br", "data_iso", "data_compacta" },
            [Enter_Extractor_Api.Models.SmartExtraction.FieldType.Currency] = new[] { "valor_br", "valor_numerico" },
            [Enter_Extractor_Api.Models.SmartExtraction.FieldType.Percentage] = new[] { "percentual_simbolo", "percentual_decimal" },
            [Enter_Extractor_Api.Models.SmartExtraction.FieldType.Phone] = new[] { "telefone" },
            [Enter_Extractor_Api.Models.SmartExtraction.FieldType.CPF] = new[] { "cpf" },
            [Enter_Extractor_Api.Models.SmartExtraction.FieldType.CNPJ] = new[] { "cnpj" },
            [Enter_Extractor_Api.Models.SmartExtraction.FieldType.Email] = new[] { "email" },
            [Enter_Extractor_Api.Models.SmartExtraction.FieldType.CEP] = new[] { "cep" },
            [Enter_Extractor_Api.Models.SmartExtraction.FieldType.Number] = new[] { "numero_simples", "numero_decimal" }
        };

        /// <summary>
        /// Metadados sobre cada padrão (para classificação futura com ML)
        /// </summary>
        public static readonly Dictionary<string, PatternMetadata> Metadata = new()
        {
            ["nome_proprio"] = new PatternMetadata
            {
                Description = "Captura nomes de pessoas com letras maiúsculas e minúsculas",
                Keywords = new[] { "nome", "person", "profissional", "titular", "responsável" },
                Examples = new[] { "JOANA D'ARC", "José da Silva", "Maria Oliveira" }
            },
            ["numero_simples"] = new PatternMetadata
            {
                Description = "Captura apenas números",
                Keywords = new[] { "número", "codigo", "id", "inscrição", "registro" },
                Examples = new[] { "123456", "101943", "42" }
            },
            ["numero_com_letra"] = new PatternMetadata
            {
                Description = "Captura números seguidos de letras/siglas",
                Keywords = new[] { "inscrição", "registro", "seccional", "código" },
                Examples = new[] { "101943 PR", "12345 SP", "999 RJ" }
            },
            ["cpf"] = new PatternMetadata
            {
                Description = "Captura CPF (com ou sem formatação)",
                Keywords = new[] { "cpf", "documento", "identidade" },
                Examples = new[] { "123.456.789-00", "12345678900" }
            },
            ["endereco"] = new PatternMetadata
            {
                Description = "Captura endereços (rua, número)",
                Keywords = new[] { "endereço", "rua", "avenida", "logradouro" },
                Examples = new[] { "Avenida Paulista, Nº 1000", "Rua das Flores, N° 42" }
            },
            ["telefone"] = new PatternMetadata
            {
                Description = "Captura telefones brasileiros",
                Keywords = new[] { "telefone", "celular", "contato", "fone" },
                Examples = new[] { "(11) 98765-4321", "11987654321", "(21) 3333-4444" }
            },
            ["cep"] = new PatternMetadata
            {
                Description = "Captura CEP (com ou sem hífen)",
                Keywords = new[] { "cep", "código postal" },
                Examples = new[] { "01310-300", "12345678" }
            },
            ["email"] = new PatternMetadata
            {
                Description = "Captura endereços de email",
                Keywords = new[] { "email", "e-mail", "correio eletrônico" },
                Examples = new[] { "contato@example.com", "user.name@domain.com.br" }
            },
            ["data_br"] = new PatternMetadata
            {
                Description = "Captura datas no formato brasileiro (DD/MM/AAAA)",
                Keywords = new[] { "data", "nascimento", "emissão", "validade" },
                Examples = new[] { "03/11/2025", "31/12/1990" }
            },
            ["data_compacta"] = new PatternMetadata
            {
                Description = "Captura datas compactas (DD/MM/AA)",
                Keywords = new[] { "data", "nascimento" },
                Examples = new[] { "03/11/25", "31/12/90" }
            },
            ["valor_br"] = new PatternMetadata
            {
                Description = "Captura valores monetários em reais",
                Keywords = new[] { "valor", "preço", "salário", "custo" },
                Examples = new[] { "R$ 1.000,00", "R$ 500,50", "R$100" }
            },
            ["percentual_simbolo"] = new PatternMetadata
            {
                Description = "Captura percentuais com símbolo %",
                Keywords = new[] { "percentual", "taxa", "juros", "desconto" },
                Examples = new[] { "10%", "15.5%", "0.5%" }
            },
            ["situacao"] = new PatternMetadata
            {
                Description = "Captura situação/status (REGULAR, IRREGULAR, etc)",
                Keywords = new[] { "situação", "status", "condição" },
                Examples = new[] { "SITUAÇÃO REGULAR", "SITUACAO IRREGULAR" }
            }
        };

        /// <summary>
        /// Busca os padrões regex apropriados para um tipo de campo
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string[] GetPatternsForFieldType(Enter_Extractor_Api.Models.SmartExtraction.FieldType fieldType)
        {
            return TypePatterns.TryGetValue(fieldType, out var patterns) ? patterns : Array.Empty<string>();
        }

        /// <summary>
        /// Aplica um padrão regex em uma única linha - OTIMIZADO
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static (bool success, string value) ApplyPatternToLine(string line, string patternType)
        {
            if (string.IsNullOrWhiteSpace(line) || !Patterns.ContainsKey(patternType))
                return (false, string.Empty);

            var regex = GetOrCreateCompiledRegex(patternType);
            var match = regex.Match(line);

            if (!match.Success)
                return (false, string.Empty);

            var value = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : match.Value.Trim();
            return (true, value);
        }

        /// <summary>
        /// Obtém ou cria uma regex compilada (com cache)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Regex GetOrCreateCompiledRegex(string patternType)
        {
            // Fast path: read lock-free
            if (_compiledRegexCache.TryGetValue(patternType, out var cached))
                return cached;

            // Slow path: compile and cache
            lock (_cacheLock)
            {
                // Double-check
                if (_compiledRegexCache.TryGetValue(patternType, out cached))
                    return cached;

                var pattern = Patterns[patternType];
                var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
                _compiledRegexCache[patternType] = regex;
                return regex;
            }
        }

        /// <summary>
        /// Aplica um padrão regex no texto e retorna candidatos encontrados
        /// </summary>
        public static List<Models.SmartExtraction.RegexMatchResult> ApplyPattern(
            string[] textLines,
            int startLine,
            string patternType)
        {
            var results = new List<Models.SmartExtraction.RegexMatchResult>();

            if (!Patterns.ContainsKey(patternType))
                return results;

            var pattern = Patterns[patternType];
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            for (int i = startLine; i < textLines.Length; i++)
            {
                var line = textLines[i].Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var match = regex.Match(line);
                if (match.Success)
                {
                    var value = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : match.Value.Trim();

                    results.Add(new Models.SmartExtraction.RegexMatchResult
                    {
                        Value = value,
                        LineIndex = i,
                        PatternType = patternType
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Tenta múltiplos padrões em ordem de prioridade
        /// </summary>
        public static List<Models.SmartExtraction.RegexMatchResult> TryMultiplePatterns(
            string[] textLines,
            int startLine,
            params string[] patternTypes)
        {
            foreach (var patternType in patternTypes)
            {
                var results = ApplyPattern(textLines, startLine, patternType);
                if (results.Any())
                    return results;
            }

            return new List<Models.SmartExtraction.RegexMatchResult>();
        }

        /// <summary>
        /// Obtém uma regex compilada que combina múltiplos padrões em um único match
        /// Reduz complexidade de O(n*m) para O(n) ao testar todos os padrões de uma vez
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Regex GetCompiledPatternsForFieldType(Enter_Extractor_Api.Models.SmartExtraction.FieldType fieldType)
        {
            var patterns = GetPatternsForFieldType(fieldType);
            if (patterns.Length == 0)
                return new Regex("(?!)", RegexOptions.Compiled); // Never matches

            // Criar cache key único para esse tipo
            var cacheKey = $"compiled_{fieldType}";

            // Fast path: read lock-free
            if (_compiledRegexCache.TryGetValue(cacheKey, out var cached))
                return cached;

            // Slow path: combinar padrões e compilar
            lock (_cacheLock)
            {
                // Double-check
                if (_compiledRegexCache.TryGetValue(cacheKey, out cached))
                    return cached;

                // Combinar todos os padrões com grupos nomeados: (?<pattern_name>pattern)
                var combinedPattern = string.Join("|", patterns.Select(p => $"(?<{p}>{Patterns[p]})"));
                var regex = new Regex(combinedPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

                _compiledRegexCache[cacheKey] = regex;
                return regex;
            }
        }

        /// <summary>
        /// Aplica todos os padrões compilados em uma única linha - O(1) por linha
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static (bool success, string value, string patternName) ApplyCompiledPatternsToLine(
            string line,
            Regex compiledPatterns)
        {
            if (string.IsNullOrWhiteSpace(line))
                return (false, string.Empty, string.Empty);

            var match = compiledPatterns.Match(line);
            if (!match.Success)
                return (false, string.Empty, string.Empty);

            // Iterar apenas pelos grupos que deram match (muito mais eficiente)
            // Skip(1) para pular o grupo 0 que é o match completo
            foreach (Group group in match.Groups)
            {
                // Pular grupo 0 (match completo) e grupos sem sucesso
                if (group.Success && !string.IsNullOrEmpty(group.Name) && group.Name != "0")
                {
                    return (true, group.Value.Trim(), group.Name);
                }
            }

            return (false, string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Metadados sobre um padrão regex
    /// </summary>
    public class PatternMetadata
    {
        public string Description { get; set; } = string.Empty;
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public string[] Examples { get; set; } = Array.Empty<string>();
    }
}
