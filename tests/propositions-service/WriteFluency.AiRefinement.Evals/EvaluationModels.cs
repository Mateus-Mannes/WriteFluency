using System.Text.Json.Serialization;
using WriteFluency.TextComparisons;

namespace WriteFluency.AiRefinement.Evals;

public sealed class EvaluationCase
{
    public required string CaseId { get; init; }
    public required string Category { get; init; }
    public required string Expectation { get; init; }
    public required string OriginalText { get; init; }
    public required string UserText { get; init; }
    public required EvaluationSourceComparison SourceComparison { get; init; }
    public required string ExpectedAction { get; init; }
    public required List<AiRefinedComparison> ExpectedRanges { get; init; }
}

public sealed class EvaluationSourceComparison
{
    public required int SourceComparisonIndex { get; init; }
    public required EvaluationTextRange OriginalTextRange { get; init; }
    public required string OriginalText { get; init; }
    public required EvaluationTextRange UserTextRange { get; init; }
    public required string UserText { get; init; }

    public AiRefinementSourceComparison ToDomain() =>
        new(
            SourceComparisonIndex,
            OriginalTextRange.ToDomain(),
            OriginalText,
            UserTextRange.ToDomain(),
            UserText);
}

public sealed record EvaluationTextRange(int InitialIndex, int FinalIndex)
{
    public TextRange ToDomain() => new(InitialIndex, FinalIndex);
}

public sealed record EvaluationCaseResult(
    string CaseId,
    string Category,
    string ExpectedAction,
    string ActualAction,
    bool IsSafe,
    bool IsExactMatch,
    double SpanF1,
    long DurationMilliseconds,
    long? InputTokenCount,
    long? OutputTokenCount,
    string? Error,
    IReadOnlyList<AiRefinedComparison> ExpectedRanges,
    IReadOnlyList<AiRefinedComparison> ActualRanges);

public sealed record EvaluationSummary(
    string Model,
    string PromptVersion,
    DateTimeOffset ExecutedAtUtc,
    int CaseCount,
    int ExactPassCount,
    double ExactPassRate,
    int InvalidOutputCount,
    int ModelFailureCount,
    int GenuineErrorRemovalCount,
    double EquivalentRemovalPrecision,
    double EquivalentRemovalRecall,
    double MeanSpanF1,
    long TotalInputTokens,
    long TotalOutputTokens,
    long TotalDurationMilliseconds,
    bool Passed,
    IReadOnlyList<EvaluationCaseResult> Cases);

public sealed record EvaluationHighlightsReport(
    string Model,
    string PromptVersion,
    DateTimeOffset ExecutedAtUtc,
    IReadOnlyList<EvaluationCaseHighlights> Cases);

public sealed record EvaluationCaseHighlights(
    string CaseId,
    string Expectation,
    string OriginalText,
    string UserText,
    EvaluationHighlight SourceComparison,
    string ExpectedAction,
    string ActualAction,
    bool IsExactMatch,
    string? Error,
    IReadOnlyList<EvaluationHighlight>? ExpectedHighlights,
    IReadOnlyList<EvaluationHighlight>? AiHighlights);

public sealed record EvaluationHighlight(
    int SourceComparisonIndex,
    EvaluationHighlightedText OriginalText,
    EvaluationHighlightedText UserText);

public sealed record EvaluationHighlightedText(
    int InitialIndex,
    int FinalIndex,
    string? Text);

public sealed class EvaluationArguments
{
    public string? Model { get; private set; }
    public int Runs { get; private set; } = 1;
    public string? CaseId { get; private set; }
    public bool ReportOnly { get; private set; }
    public bool ValidateOnly { get; private set; }

    public static EvaluationArguments Parse(string[] args)
    {
        var result = new EvaluationArguments();

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--model":
                    result.Model = NextValue(args, ref index, "--model");
                    break;
                case "--runs":
                    result.Runs = int.Parse(NextValue(args, ref index, "--runs"));
                    break;
                case "--case":
                    result.CaseId = NextValue(args, ref index, "--case");
                    break;
                case "--report-only":
                    result.ReportOnly = true;
                    break;
                case "--validate-only":
                    result.ValidateOnly = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[index]}'.");
            }
        }

        if (result.Runs <= 0)
        {
            throw new ArgumentException("--runs must be greater than zero.");
        }

        return result;
    }

    private static string NextValue(string[] args, ref int index, string argument)
    {
        if (++index >= args.Length)
        {
            throw new ArgumentException($"{argument} requires a value.");
        }

        return args[index];
    }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(List<EvaluationCase>))]
[JsonSerializable(typeof(EvaluationSummary))]
[JsonSerializable(typeof(EvaluationHighlightsReport))]
public partial class EvaluationJsonContext : JsonSerializerContext;
