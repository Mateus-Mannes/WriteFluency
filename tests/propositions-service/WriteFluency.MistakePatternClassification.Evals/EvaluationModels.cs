using WriteFluency.TextComparisons;

namespace WriteFluency.MistakePatternClassification.Evals;

public sealed class EvaluationCase
{
    public required string CaseId { get; init; }
    public required string Category { get; init; }
    public required string OriginalText { get; init; }
    public required string UserText { get; init; }
    public required List<EvaluationComparison> Comparisons { get; init; }
}

public sealed class SourceEvaluationCase
{
    public required string CaseId { get; init; }
    public required string Category { get; init; }
    public required string OriginalText { get; init; }
    public required string UserText { get; init; }
    public required List<SourceEvaluationComparison> ExpectedFinalComparisons { get; init; }

    public EvaluationCase ToEvaluationCase() =>
        new()
        {
            CaseId = CaseId,
            Category = Category,
            OriginalText = OriginalText,
            UserText = UserText,
            Comparisons = ExpectedFinalComparisons
                .Select((comparison, index) => comparison.ToEvaluationComparison(index))
                .ToList()
        };
}

public sealed record SourceEvaluationComparison
{
    public required int SourceComparisonIndex { get; init; }
    public required EvaluationTextRange OriginalTextRange { get; init; }
    public required string OriginalText { get; init; }
    public required EvaluationTextRange UserTextRange { get; init; }
    public required string UserText { get; init; }
    public required List<string> Tags { get; init; }
    public required string StudentPhrase { get; init; }

    public EvaluationComparison ToEvaluationComparison(int comparisonIndex) =>
        new()
        {
            ComparisonIndex = comparisonIndex,
            SourceComparisonIndex = SourceComparisonIndex,
            OriginalTextRange = OriginalTextRange,
            OriginalText = OriginalText,
            UserTextRange = UserTextRange,
            UserText = UserText,
            ExpectedTags = Tags,
            ReferenceStudentPhrase = StudentPhrase
        };
}

public sealed record EvaluationComparison
{
    public required int ComparisonIndex { get; init; }
    public required int SourceComparisonIndex { get; init; }
    public required EvaluationTextRange OriginalTextRange { get; init; }
    public required string OriginalText { get; init; }
    public required EvaluationTextRange UserTextRange { get; init; }
    public required string UserText { get; init; }
    public required List<string> ExpectedTags { get; init; }
    public required string ReferenceStudentPhrase { get; init; }
    public List<string>? AcceptedTags { get; init; }
    public List<string>? ForbiddenTags { get; init; }

    public TextComparison ToDomain() =>
        new(
            OriginalTextRange.ToDomain(),
            OriginalText,
            UserTextRange.ToDomain(),
            UserText,
            sourceComparisonIndex: SourceComparisonIndex);
}

public sealed record EvaluationTextRange(int InitialIndex, int FinalIndex)
{
    public TextRange ToDomain() => new(InitialIndex, FinalIndex);
}

public sealed record EvaluationArguments(
    string? CaseId,
    int Runs,
    int Concurrency,
    string? Model,
    float? Temperature,
    int? MaxComparisonsPerRequest,
    decimal? InputUsdPerMillionTokens,
    decimal? OutputUsdPerMillionTokens,
    bool ValidateOnly,
    bool ReportOnly)
{
    public static EvaluationArguments Parse(string[] args)
    {
        string? caseId = null;
        var runs = 1;
        var concurrency = 1;
        string? model = null;
        float? temperature = null;
        int? maxComparisonsPerRequest = null;
        decimal? inputUsdPerMillionTokens = EvaluationPricing.DefaultInputUsdPerMillionTokens;
        decimal? outputUsdPerMillionTokens = EvaluationPricing.DefaultOutputUsdPerMillionTokens;
        var validateOnly = false;
        var reportOnly = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--case":
                    caseId = ReadValue(args, ref index, arg);
                    break;
                case "--runs":
                    runs = int.Parse(ReadValue(args, ref index, arg));
                    break;
                case "--concurrency":
                    concurrency = int.Parse(ReadValue(args, ref index, arg));
                    break;
                case "--model":
                    model = ReadValue(args, ref index, arg);
                    break;
                case "--temperature":
                    temperature = float.Parse(
                        ReadValue(args, ref index, arg),
                        System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--max-comparisons-per-request":
                    maxComparisonsPerRequest = int.Parse(
                        ReadValue(args, ref index, arg));
                    break;
                case "--input-usd-per-million-tokens":
                    inputUsdPerMillionTokens = decimal.Parse(
                        ReadValue(args, ref index, arg),
                        System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--output-usd-per-million-tokens":
                    outputUsdPerMillionTokens = decimal.Parse(
                        ReadValue(args, ref index, arg),
                        System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--validate-only":
                    validateOnly = true;
                    break;
                case "--report-only":
                    reportOnly = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        return new(
            caseId,
            Math.Max(1, runs),
            Math.Max(1, concurrency),
            model,
            temperature,
            maxComparisonsPerRequest,
            inputUsdPerMillionTokens,
            outputUsdPerMillionTokens,
            validateOnly,
            reportOnly);
    }

    private static string ReadValue(
        string[] args,
        ref int index,
        string argumentName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for '{argumentName}'.");
        }

        index++;
        return args[index];
    }
}

public sealed record EvaluationRunSummary(
    string Model,
    float Temperature,
    int MaxComparisonsPerRequest,
    EvaluationPricing? Pricing,
    DateTimeOffset ExecutedAtUtc,
    IReadOnlyList<EvaluationCaseRunResult> Runs)
{
    private const double TagQualityThreshold = 0.70;
    private const double PhraseQualityThreshold = 0.70;

    public int CaseRunCount => Runs.Count;
    public int PassingCaseRunCount => Runs.Count(run => run.Passed);
    public int ComparisonCount => Runs.Sum(run => run.Comparisons.Count);
    public int PassingComparisonCount =>
        Runs.Sum(run => run.Comparisons.Count(comparison => comparison.Passed));
    public double TagPrecision =>
        SafeDivide(
            Runs.Sum(run => run.Comparisons.Sum(comparison => comparison.TagTruePositiveCount)),
            Runs.Sum(run => run.Comparisons.Sum(comparison => comparison.TagPredictedCount)));
    public double TagRecall =>
        SafeDivide(
            Runs.Sum(run => run.Comparisons.Sum(comparison => comparison.TagTruePositiveCount)),
            Runs.Sum(run => run.Comparisons.Sum(comparison => comparison.TagExpectedCount)));
    public double TagF1
    {
        get
        {
            var precision = TagPrecision;
            var recall = TagRecall;
            return precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
        }
    }
    public double PhrasePassRate =>
        SafeDivide(
            Runs.Sum(run => run.Comparisons.Count(comparison => comparison.PhrasePassed)),
            ComparisonCount);
    public int RequestCount => Runs.Sum(run => run.Requests.Count);
    public long TotalRequestDurationMilliseconds =>
        Runs.Sum(run => run.Requests.Sum(request => request.DurationMilliseconds));
    public long? InputTokenCount => SumNullableTokens(request => request.InputTokenCount);
    public long? OutputTokenCount => SumNullableTokens(request => request.OutputTokenCount);
    public long? TotalTokenCount => SumNullableTokens(request => request.TotalTokenCount);
    public decimal? EstimatedCostUsd =>
        EstimateCost(InputTokenCount, OutputTokenCount, Pricing);
    public bool Passed =>
        Runs.All(run => run.Error is null)
        && TagPrecision >= TagQualityThreshold
        && TagRecall >= TagQualityThreshold
        && PhrasePassRate >= PhraseQualityThreshold;

    private static double SafeDivide(double numerator, double denominator) =>
        denominator == 0 ? 0 : numerator / denominator;

    public static decimal? EstimateCost(
        long? inputTokens,
        long? outputTokens,
        EvaluationPricing? pricing)
    {
        if (inputTokens is null || outputTokens is null || pricing is null)
        {
            return null;
        }

        return (inputTokens.Value / 1_000_000m * pricing.InputUsdPerMillionTokens)
               + (outputTokens.Value / 1_000_000m * pricing.OutputUsdPerMillionTokens);
    }

    private long? SumNullableTokens(Func<EvaluationRequestResult, long?> selector)
    {
        var values = Runs
            .SelectMany(run => run.Requests)
            .Select(selector)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return values.Length == 0 ? null : values.Sum();
    }
}

public sealed record EvaluationCaseRunResult(
    string CaseId,
    string Category,
    int RunNumber,
    bool Passed,
    long DurationMilliseconds,
    IReadOnlyList<EvaluationRequestResult> Requests,
    string? Error,
    IReadOnlyList<EvaluationComparisonResult> Comparisons);

public sealed record EvaluationRequestResult(
    string Stage,
    int BatchNumber,
    int StartIndex,
    int ComparisonCount,
    long DurationMilliseconds,
    long? InputTokenCount,
    long? OutputTokenCount,
    long? TotalTokenCount);

public sealed record EvaluationPricing(
    decimal InputUsdPerMillionTokens,
    decimal OutputUsdPerMillionTokens)
{
    public const decimal DefaultInputUsdPerMillionTokens = 0.05m;
    public const decimal DefaultOutputUsdPerMillionTokens = 0.40m;
}

public sealed record EvaluationContextSnippet(
    string Before,
    string Highlight,
    string After)
{
    public string Text => Before + Highlight + After;
}

public sealed record EvaluationComparisonResult(
    int ComparisonIndex,
    int SourceComparisonIndex,
    string OriginalText,
    string UserText,
    EvaluationContextSnippet OriginalContext,
    EvaluationContextSnippet UserContext,
    IReadOnlyList<string> ExpectedTags,
    IReadOnlyList<string> AcceptedTags,
    IReadOnlyList<string> ForbiddenTags,
    IReadOnlyList<string> ActualTags,
    string? ActualPhrase,
    string ReferenceStudentPhrase,
    double PhraseTokenF1,
    double PhraseEditSimilarity,
    double? PhraseAiSimilarityScore,
    string? PhraseAiSimilarityReason,
    bool TagsPassed,
    bool PhrasePassed,
    bool Passed,
    IReadOnlyList<string> Failures,
    int TagTruePositiveCount,
    int TagPredictedCount,
    int TagExpectedCount);

public sealed record EvaluationPhraseSimilarityGrade(
    int ComparisonIndex,
    double Score,
    bool Passed,
    string Reason);

public sealed record EvaluationPhraseSimilarityRun(
    IReadOnlyList<EvaluationPhraseSimilarityGrade> Grades,
    IReadOnlyList<EvaluationRequestResult> Requests);
