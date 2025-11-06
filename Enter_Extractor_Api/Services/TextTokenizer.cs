using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace Enter_Extractor_Api.Services;

public interface ITextTokenizer
{
    List<string> TokenizeLine(ReadOnlySpan<char> line);
}

public class TextTokenizer : ITextTokenizer
{
    // Cache compiled regexes (reuso)
    private static readonly Regex[] CompiledPatterns = CompilePatterns();

    private static Regex[] CompilePatterns()
    {
        return new[]
        {
            new Regex(@"[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ][A-ZÀ-Úa-zà-ú\s]+,\s*N[ºo°]\s*\d+[^,\n]*(?:,\s*[^,\n]+)*", RegexOptions.Compiled),
            new Regex(@"\d{2,3}[\.\-\s]?\d{3}[\.\-\s]?\d{3}[\.\-/]?\d{2,4}", RegexOptions.Compiled),
            new Regex(@"\d+", RegexOptions.Compiled),
            new Regex(@"\b[A-Z]{2,3}\b", RegexOptions.Compiled),
            new Regex(@"[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ][a-zà-ú]+(?:\s[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ][a-zà-ú]+)+", RegexOptions.Compiled),
            new Regex(@"\b[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ]{3,}\b", RegexOptions.Compiled),
            new Regex(@"[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ][a-zà-ú]+", RegexOptions.Compiled)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public List<string> TokenizeLine(ReadOnlySpan<char> lineSpan)
    {
        // Converter span para string apenas uma vez
        var line = lineSpan.ToString();

        if (string.IsNullOrWhiteSpace(line))
            return new List<string>();

        var tokens = new List<string>(10); // Pré-alocar capacidade esperada
        var usedPositions = new HashSet<int>();

        // Aplicar cada padrão compilado em ordem
        for (int patternIndex = 0; patternIndex < CompiledPatterns.Length; patternIndex++)
        {
            var matches = CompiledPatterns[patternIndex].Matches(line);

            foreach (Match match in matches)
            {
                // Verificar se posição já foi capturada por padrão anterior
                if (!IsOverlapping(match.Index, match.Length, usedPositions))
                {
                    tokens.Add(match.Value.Trim());

                    // Marcar posições como usadas
                    MarkPositionsAsUsed(match.Index, match.Length, usedPositions);
                }
            }
        }

        // Usar StringComparer.Ordinal para melhor performance
        return tokens.Distinct(StringComparer.Ordinal)
                     .OrderBy(t => line.IndexOf(t, StringComparison.Ordinal))
                     .ToList();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOverlapping(int startIndex, int length, HashSet<int> usedPositions)
    {
        for (int i = startIndex; i < startIndex + length; i++)
        {
            if (usedPositions.Contains(i))
                return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MarkPositionsAsUsed(int startIndex, int length, HashSet<int> usedPositions)
    {
        for (int i = startIndex; i < startIndex + length; i++)
        {
            usedPositions.Add(i);
        }
    }
}
