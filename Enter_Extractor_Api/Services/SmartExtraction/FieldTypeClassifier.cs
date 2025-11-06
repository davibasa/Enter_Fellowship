using Enter_Extractor_Api.Models.SmartExtraction;
using System.Text.RegularExpressions;

namespace Enter_Extractor_Api.Services.SmartExtraction;

public interface IFieldTypeClassifier
{
    FieldType ClassifyField(string fieldName, string fieldDescription);
}

public class FieldTypeClassifier : IFieldTypeClassifier
{
    // Keywords para classificação de tipos específicos
    private static readonly Dictionary<FieldType, string[]> TypeKeywords = new()
    {
        [FieldType.Date] = new[] { "data", "date", "nascimento", "validade", "vencimento", "emissão", "emissao" },
        [FieldType.Currency] = new[] { "valor", "price", "preco", "preço", "salario", "salário", "custo", "receita" },
        [FieldType.Percentage] = new[] { "percentual", "percent", "taxa", "juros", "desconto", "alíquota", "aliquota" },
        [FieldType.Phone] = new[] { "telefone", "phone", "celular", "fone", "contato" },
        [FieldType.CPF] = new[] { "cpf" },
        [FieldType.CNPJ] = new[] { "cnpj" },
        [FieldType.Email] = new[] { "email", "e-mail", "mail" },
        [FieldType.CEP] = new[] { "cep", "codigo postal", "código postal", "postal code" },
        [FieldType.Number] = new[] { "numero", "número", "number", "quantidade", "qtd", "idade", "ano" },
        [FieldType.MultiLine] = new[] { "endereço", "endereco", "descrição", "descricao", "observação", "observacao", "histórico", "historico", "comentário", "comentario" }
    };

    private static readonly string[] EnumIndicators =
    {
        "pode ser", "valores:", "pode conter", "pode incluir",
        "opções:", "opcoes:", "escolha entre"
    };

    public FieldType ClassifyField(string fieldName, string fieldDescription)
    {
        var fullText = $"{fieldName} {fieldDescription}".ToLowerInvariant();

        // 1. Verificar se é campo enum (tem valores CAPSLOCK na descrição)
        if (ContainsEnumValues(fieldDescription))
        {
            return FieldType.Enum;
        }

        // 2. Verificar tipos específicos por ordem de prioridade
        // (mais específico primeiro, genérico depois)
        foreach (var kvp in TypeKeywords)
        {
            var fieldType = kvp.Key;
            var keywords = kvp.Value;

            if (keywords.Any(keyword => fullText.Contains(keyword)))
            {
                return fieldType;
            }
        }

        // 3. Padrão: campo simples genérico
        return FieldType.Simple;
    }

    private bool ContainsEnumValues(string description)
    {
        // Verificar se tem indicadores de enum
        var hasIndicator = EnumIndicators.Any(indicator =>
            description.Contains(indicator, StringComparison.OrdinalIgnoreCase));

        if (!hasIndicator)
            return false;

        // Verificar se tem palavras em CAPSLOCK (possíveis valores do enum)
        var capsLockPattern = @"\b[A-ZÀÁÂÃÇÉÊÍÓÔÕÚ]{3,}\b";
        var matches = Regex.Matches(description, capsLockPattern);

        return matches.Count >= 2; // Pelo menos 2 valores em CAPSLOCK
    }
}
