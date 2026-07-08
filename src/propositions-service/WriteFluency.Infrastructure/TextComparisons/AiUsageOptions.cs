using System.ComponentModel.DataAnnotations;
using WriteFluency.TextComparisons;

namespace WriteFluency.Infrastructure.TextComparisons;

public sealed class AiUsageOptions
{
    public const string Section = "TextComparison:AiUsage";

    public bool Enabled { get; set; } = true;

    [Range(1, 10000)]
    public int DailySubmissionLimit { get; set; } = 40;

    [Range(1, 100000)]
    public int MonthlySubmissionLimit { get; set; } = 300;

    [Range(typeof(decimal), "0", "100000")]
    public decimal MonthlyEstimatedCostLimitUsd { get; set; } = 0.50m;

    [Range(typeof(decimal), "0", "100000")]
    public decimal InputUsdPerMillionTokens { get; set; } = 0.05m;

    [Range(typeof(decimal), "0", "100000")]
    public decimal OutputUsdPerMillionTokens { get; set; } = 0.40m;

    public AiUsageLimitPolicy CreateDefaultPolicy() =>
        new(
            DailySubmissionLimit,
            MonthlySubmissionLimit,
            LifetimeSubmissionLimit: null,
            MonthlyEstimatedCostLimitUsd);
}
