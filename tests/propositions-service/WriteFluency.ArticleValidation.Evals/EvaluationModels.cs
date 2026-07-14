using System.Globalization;

namespace WriteFluency.ArticleValidation.Evals;

public sealed record EvaluationCase
{
    public required string CaseId { get; init; }
    public required string Category { get; init; }
    public required bool ExpectedValid { get; init; }
    public required string SourceType { get; init; }
    public string? SourceUrl { get; init; }
    public required string Reason { get; init; }
    public required string ArticleContent { get; init; }
}

public sealed record EvaluationArguments(
    string? CaseId,
    int Runs,
    string? Model,
    bool ValidateOnly,
    bool ReportOnly)
{
    public static EvaluationArguments Parse(string[] args)
    {
        string? caseId = null;
        var runs = 1;
        string? model = null;
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
                    runs = int.Parse(ReadValue(args, ref index, arg), CultureInfo.InvariantCulture);
                    break;
                case "--model":
                    model = ReadValue(args, ref index, arg);
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
            model,
            validateOnly,
            reportOnly);
    }

    private static string ReadValue(string[] args, ref int index, string argumentName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for '{argumentName}'.");
        }

        index++;
        return args[index];
    }
}

public sealed record EvaluationRunResult(
    string CaseId,
    string Category,
    bool ExpectedValid,
    bool ActualValid,
    bool Passed,
    bool DeterministicPolicyPassed,
    IReadOnlyList<string> Errors,
    string Reason,
    string? SourceUrl);

public sealed record EvaluationRunSummary(
    string Model,
    DateTimeOffset ExecutedAtUtc,
    IReadOnlyList<EvaluationRunResult> Results)
{
    public int CaseCount => Results.Count;
    public int PassingCaseCount => Results.Count(result => result.Passed);
    public bool Passed => PassingCaseCount == CaseCount;
    public double Accuracy => CaseCount == 0 ? 0 : (double)PassingCaseCount / CaseCount;
}
