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
    public EvaluationSourceComparison? SourceComparison { get; init; }
    public string? ExpectedAction { get; init; }
    public List<AiRefinedComparison>? ExpectedRanges { get; init; }
    public List<EvaluationSourceComparison>? SourceComparisons { get; init; }
    public List<EvaluationExpectedDecision>? ExpectedDecisions { get; init; }
    public List<EvaluationExpectedFinalComparison>? ExpectedFinalComparisons { get; init; }
    public List<EvaluationExpectedTraceEntry>? ExpectedTrace { get; init; }
    public int? FocusSourceComparisonIndex { get; init; }

    public bool UsesOrchestrationContract =>
        ExpectedFinalComparisons is not null || ExpectedTrace is not null;

    public IReadOnlyList<EvaluationSourceComparison> GetSourceComparisons() =>
        SourceComparisons is { Count: > 0 }
            ? SourceComparisons
            : SourceComparison is not null
                ? [SourceComparison]
                : ExpectedTrace is { Count: > 0 }
                    ? ExpectedTrace
                        .Select(trace => trace.Initial.ToSourceComparison(
                            trace.SourceComparisonIndex))
                        .ToList()
                    : [];

    public IReadOnlyList<EvaluationExpectedDecision> GetExpectedDecisions() =>
        ExpectedDecisions is { Count: > 0 }
            ? ExpectedDecisions
            : SourceComparison is not null && ExpectedAction is not null
                ?
                [
                    new EvaluationExpectedDecision
                    {
                        SourceComparisonIndex = SourceComparison.SourceComparisonIndex,
                        ExpectedAction = ExpectedAction,
                        ExpectedRanges = ExpectedRanges ?? []
                    }
                ]
                : ExpectedFinalComparisons is not null
                    ? CreateExpectedDecisionsFromFinalComparisons()
                    : [];

    public int GetFocusSourceComparisonIndex() =>
        FocusSourceComparisonIndex
        ?? SourceComparison?.SourceComparisonIndex
        ?? GetSourceComparisons().First().SourceComparisonIndex;

    private IReadOnlyList<EvaluationExpectedDecision>
        CreateExpectedDecisionsFromFinalComparisons()
    {
        var sources = GetSourceComparisons()
            .ToDictionary(source => source.SourceComparisonIndex);
        var finalBySource = (ExpectedFinalComparisons ?? [])
            .GroupBy(comparison => comparison.SourceComparisonIndex)
            .ToDictionary(group => group.Key, group => group.ToList());

        return sources
            .Select(source =>
            {
                finalBySource.TryGetValue(source.Key, out var final);
                var expectedRanges = final?
                    .Select(comparison => comparison.ToRange())
                    .OrderBy(range => range.OriginalTextInitialIndex)
                    .ThenBy(range => range.UserTextInitialIndex)
                    .ToList()
                    ?? [];

                return new EvaluationExpectedDecision
                {
                    SourceComparisonIndex = source.Key,
                    ExpectedAction = DetermineAction(expectedRanges, source.Value),
                    ExpectedRanges = expectedRanges
                };
            })
            .OrderBy(decision => decision.SourceComparisonIndex)
            .ToList();
    }

    private static string DetermineAction(
        IReadOnlyList<AiRefinedComparison> ranges,
        EvaluationSourceComparison source)
    {
        if (ranges.Count == 0)
        {
            return AiRefinementActions.Remove;
        }

        if (ranges.Count > 1)
        {
            return "split";
        }

        var range = ranges[0];
        return range.OriginalTextInitialIndex == source.OriginalTextRange.InitialIndex
            && range.OriginalTextFinalIndex == source.OriginalTextRange.FinalIndex
            && range.UserTextInitialIndex == source.UserTextRange.InitialIndex
            && range.UserTextFinalIndex == source.UserTextRange.FinalIndex
                ? AiRefinementActions.Keep
                : "shrink";
    }
}

public sealed class EvaluationExpectedDecision
{
    public required int SourceComparisonIndex { get; init; }
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
}

public sealed class EvaluationExpectedFinalComparison
{
    public required int SourceComparisonIndex { get; init; }
    public required EvaluationTextRange OriginalTextRange { get; init; }
    public required string OriginalText { get; init; }
    public required EvaluationTextRange UserTextRange { get; init; }
    public required string UserText { get; init; }
    public required bool IsDeterministicallyRefined { get; init; }
    public required bool IsAiRefined { get; init; }

    public AiRefinedComparison ToRange() =>
        new(
            SourceComparisonIndex,
            OriginalTextRange.InitialIndex,
            OriginalTextRange.FinalIndex,
            UserTextRange.InitialIndex,
            UserTextRange.FinalIndex);

    public EvaluationFinalComparison ToFinalComparison() =>
        new()
        {
            SourceComparisonIndex = SourceComparisonIndex,
            OriginalTextRange = OriginalTextRange,
            OriginalText = OriginalText,
            UserTextRange = UserTextRange,
            UserText = UserText,
            IsDeterministicallyRefined = IsDeterministicallyRefined,
            IsAiRefined = IsAiRefined
        };
}

public sealed class EvaluationFinalComparison
{
    public required int SourceComparisonIndex { get; init; }
    public required EvaluationTextRange OriginalTextRange { get; init; }
    public required string OriginalText { get; init; }
    public required EvaluationTextRange UserTextRange { get; init; }
    public required string UserText { get; init; }
    public required bool IsDeterministicallyRefined { get; init; }
    public required bool IsAiRefined { get; init; }
}

public sealed class EvaluationExpectedTraceEntry
{
    public required int SourceComparisonIndex { get; init; }
    public required EvaluationComparisonSnapshot Initial { get; init; }
    public EvaluationExpectedStageTrace? Deterministic { get; init; }
    public EvaluationExpectedStageTrace? Ai { get; init; }
}

public sealed class EvaluationExpectedStageTrace
{
    public required string Action { get; init; }
    public string? ReasonCode { get; init; }
    public required List<EvaluationComparisonSnapshot> Output { get; init; }
    public string? ValidationStatus { get; init; }
    public List<EvaluationComparisonSnapshot>? ProposedOutput { get; init; }
    public string? ValidationFailureReason { get; init; }
}

public sealed class EvaluationComparisonSnapshot
{
    public required EvaluationTextRange OriginalTextRange { get; init; }
    public required string OriginalText { get; init; }
    public required EvaluationTextRange UserTextRange { get; init; }
    public required string UserText { get; init; }

    public EvaluationSourceComparison ToSourceComparison(
        int sourceComparisonIndex) =>
        new()
        {
            SourceComparisonIndex = sourceComparisonIndex,
            OriginalTextRange = OriginalTextRange,
            OriginalText = OriginalText,
            UserTextRange = UserTextRange,
            UserText = UserText
        };
}

public sealed record EvaluationTextRange(int InitialIndex, int FinalIndex)
{
    public TextRange ToDomain() => new(InitialIndex, FinalIndex);
}

public sealed record EvaluationCaseResult(
    string CaseId,
    string Category,
    int RunNumber,
    int FocusSourceComparisonIndex,
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
    IReadOnlyList<AiRefinedComparison> ActualRanges,
    IReadOnlyList<EvaluationSourceResult> Sources);

public sealed record EvaluationSourceResult(
    int SourceComparisonIndex,
    string ExpectedAction,
    string ActualAction,
    bool IsSafe,
    bool IsExactMatch,
    double SpanF1,
    string? Error,
    IReadOnlyList<AiRefinedComparison> ExpectedRanges,
    IReadOnlyList<AiRefinedComparison> ActualRanges,
    IReadOnlyList<EvaluationFinalComparison>? ExpectedFinalComparisons = null,
    IReadOnlyList<EvaluationFinalComparison>? ActualFinalComparisons = null,
    EvaluationExpectedTraceEntry? ExpectedTrace = null,
    EvaluationExpectedTraceEntry? ActualTrace = null);

public sealed record EvaluationSummary(
    string Model,
    string PromptVersion,
    DateTimeOffset ExecutedAtUtc,
    int RunCount,
    int DefinitionCount,
    int CaseCount,
    int ExactPassCount,
    double ExactPassRate,
    int ComparisonCount,
    int ExactComparisonCount,
    double ExactComparisonRate,
    int FocusComparisonCount,
    int ExactFocusComparisonCount,
    double FocusExactRate,
    int SafeComparisonCount,
    double MeanComparisonSpanF1,
    int FlakyCaseCount,
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
    int RunNumber,
    string Expectation,
    int FocusSourceComparisonIndex,
    string OriginalText,
    string UserText,
    IReadOnlyList<EvaluationHighlight> SourceComparisons,
    string ExpectedAction,
    string ActualAction,
    bool IsExactMatch,
    string? Error,
    IReadOnlyList<EvaluationHighlight>? ExpectedHighlights,
    IReadOnlyList<EvaluationHighlight>? AiHighlights,
    IReadOnlyList<EvaluationSourceHighlights> Sources);

public sealed record EvaluationSourceHighlights(
    int SourceComparisonIndex,
    bool IsFocus,
    EvaluationHighlight SourceComparison,
    string ExpectedAction,
    string ActualAction,
    bool IsSafe,
    bool IsExactMatch,
    double SpanF1,
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
    public int Concurrency { get; private set; } = 1;
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
                case "--concurrency":
                    result.Concurrency = int.Parse(
                        NextValue(args, ref index, "--concurrency"));
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

        if (result.Concurrency <= 0)
        {
            throw new ArgumentException(
                "--concurrency must be greater than zero.");
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
