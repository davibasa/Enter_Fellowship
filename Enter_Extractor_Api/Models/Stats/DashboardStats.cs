using System.Text.Json.Serialization;

namespace Enter_Extractor_Api.Models.Stats;

/// <summary>
/// Dados do dashboard do usuário
/// </summary>
public class DashboardStats
{
    /// <summary>
    /// Período analisado
    /// </summary>
    [JsonPropertyName("period")]
    public PeriodInfo Period { get; set; } = new();

    /// <summary>
    /// Resumo das estatísticas
    /// </summary>
    [JsonPropertyName("summary")]
    public SummaryStats Summary { get; set; } = new();

    /// <summary>
    /// Quota do usuário
    /// </summary>
    [JsonPropertyName("quota")]
    public QuotaInfo Quota { get; set; } = new();

    /// <summary>
    /// Linha do tempo (série temporal)
    /// </summary>
    [JsonPropertyName("timeline")]
    public List<TimelineDataPoint> Timeline { get; set; } = new();

    /// <summary>
    /// Top labels mais usados
    /// </summary>
    [JsonPropertyName("top_labels")]
    public List<LabelStats> TopLabels { get; set; } = new();
}

public class PeriodInfo
{
    [JsonPropertyName("start")]
    public string Start { get; set; } = string.Empty;

    [JsonPropertyName("end")]
    public string End { get; set; } = string.Empty;

    [JsonPropertyName("days")]
    public int Days { get; set; }
}

public class SummaryStats
{
    [JsonPropertyName("total_extractions")]
    public int TotalExtractions { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("total_cost_usd")]
    public double TotalCostUsd { get; set; }

    [JsonPropertyName("avg_success_rate")]
    public double AvgSuccessRate { get; set; }

    [JsonPropertyName("avg_processing_time_ms")]
    public long AvgProcessingTimeMs { get; set; }
}

public class QuotaInfo
{
    [JsonPropertyName("plan")]
    public string Plan { get; set; } = "free";

    [JsonPropertyName("extractions_remaining")]
    public int ExtractionsRemaining { get; set; }

    [JsonPropertyName("extractions_limit")]
    public int ExtractionsLimit { get; set; }

    [JsonPropertyName("usage_percentage")]
    public double UsagePercentage { get; set; }
}

public class TimelineDataPoint
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("extractions")]
    public int Extractions { get; set; }

    [JsonPropertyName("tokens")]
    public int Tokens { get; set; }

    [JsonPropertyName("cost_usd")]
    public double CostUsd { get; set; }
}

public class LabelStats
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("percentage")]
    public double Percentage { get; set; }
}
