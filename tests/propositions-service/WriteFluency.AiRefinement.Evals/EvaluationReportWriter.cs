using System.Text;
using System.Text.Json;
using WriteFluency.TextComparisons;

namespace WriteFluency.AiRefinement.Evals;

public static class EvaluationReportWriter
{
    public static async Task<EvaluationReportPaths> WriteAsync(
        EvaluationSummary summary,
        IReadOnlyList<EvaluationCase> evaluationCases,
        CancellationToken cancellationToken)
    {
        var outputDirectory = Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "ai-evals",
            summary.ExecutedAtUtc.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(outputDirectory);

        var jsonPath = Path.Combine(outputDirectory, "report.json");
        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(
                summary,
                EvaluationJsonContext.Default.EvaluationSummary),
            cancellationToken);

        var markdownPath = Path.Combine(outputDirectory, "report.md");
        await File.WriteAllTextAsync(
            markdownPath,
            CreateMarkdown(summary),
            cancellationToken);

        var highlightsPath = Path.Combine(outputDirectory, "highlights.json");
        var indentedJsonContext = new EvaluationJsonContext(
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(
            highlightsPath,
            JsonSerializer.Serialize(
                CreateHighlightsReport(summary, evaluationCases),
                indentedJsonContext.EvaluationHighlightsReport),
            cancellationToken);

        return new EvaluationReportPaths(
            markdownPath,
            jsonPath,
            highlightsPath);
    }

    private static EvaluationHighlightsReport CreateHighlightsReport(
        EvaluationSummary summary,
        IReadOnlyList<EvaluationCase> evaluationCases)
    {
        var casesById = evaluationCases.ToDictionary(
            evaluationCase => evaluationCase.CaseId);

        var results = summary.Cases
            .Select(result => CreateCaseHighlights(
                result,
                casesById[result.CaseId]))
            .ToList();

        return new EvaluationHighlightsReport(
            summary.Model,
            summary.PromptVersion,
            summary.ExecutedAtUtc,
            results);
    }

    private static EvaluationCaseHighlights CreateCaseHighlights(
        EvaluationCaseResult result,
        EvaluationCase evaluationCase)
    {
        var expectedHighlights = CreateHighlights(
            result.ExpectedRanges,
            evaluationCase);
        var aiHighlights = CreateHighlights(
            result.ActualRanges,
            evaluationCase);
        var sourceComparison = CreateSourceComparison(evaluationCase);

        return new EvaluationCaseHighlights(
            result.CaseId,
            evaluationCase.Expectation,
            evaluationCase.OriginalText,
            evaluationCase.UserText,
            sourceComparison,
            result.ExpectedAction,
            result.ActualAction,
            result.IsExactMatch,
            result.Error,
            expectedHighlights,
            aiHighlights);
    }

    private static EvaluationHighlight CreateSourceComparison(
        EvaluationCase evaluationCase) =>
        new(
            evaluationCase.SourceComparison.SourceComparisonIndex,
            CreateHighlightedText(
                evaluationCase.OriginalText,
                evaluationCase.SourceComparison.OriginalTextRange.InitialIndex,
                evaluationCase.SourceComparison.OriginalTextRange.FinalIndex),
            CreateHighlightedText(
                evaluationCase.UserText,
                evaluationCase.SourceComparison.UserTextRange.InitialIndex,
                evaluationCase.SourceComparison.UserTextRange.FinalIndex));

    private static IReadOnlyList<EvaluationHighlight>? CreateHighlights(
        IReadOnlyList<AiRefinedComparison> ranges,
        EvaluationCase evaluationCase) =>
        ranges.Count == 0
            ? null
            : ranges
                .Select(range => new EvaluationHighlight(
                    range.SourceComparisonIndex,
                    CreateHighlightedText(
                        evaluationCase.OriginalText,
                        range.OriginalTextInitialIndex,
                        range.OriginalTextFinalIndex),
                    CreateHighlightedText(
                        evaluationCase.UserText,
                        range.UserTextInitialIndex,
                        range.UserTextFinalIndex)))
                .ToList();

    private static EvaluationHighlightedText CreateHighlightedText(
        string fullText,
        int initialIndex,
        int finalIndex)
    {
        var highlightedText = initialIndex >= 0
            && finalIndex >= initialIndex
            && finalIndex < fullText.Length
                ? fullText.Substring(initialIndex, finalIndex - initialIndex + 1)
                : null;

        return new EvaluationHighlightedText(
            initialIndex,
            finalIndex,
            highlightedText);
    }

    private static string CreateMarkdown(EvaluationSummary summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# AI Refinement Evaluation");
        builder.AppendLine();
        builder.AppendLine($"- Model: `{summary.Model}`");
        builder.AppendLine($"- Prompt: `{summary.PromptVersion}`");
        builder.AppendLine($"- Passed: `{summary.Passed}`");
        builder.AppendLine($"- Exact cases: `{summary.ExactPassCount}/{summary.CaseCount}` ({summary.ExactPassRate:P1})");
        builder.AppendLine($"- Equivalent removal precision: `{summary.EquivalentRemovalPrecision:P1}`");
        builder.AppendLine($"- Equivalent removal recall: `{summary.EquivalentRemovalRecall:P1}`");
        builder.AppendLine($"- Mean span F1: `{summary.MeanSpanF1:F3}`");
        builder.AppendLine($"- Invalid outputs: `{summary.InvalidOutputCount}`");
        builder.AppendLine($"- Model failures: `{summary.ModelFailureCount}`");
        builder.AppendLine($"- Genuine errors removed: `{summary.GenuineErrorRemovalCount}`");
        builder.AppendLine($"- Tokens: `{summary.TotalInputTokens}` input / `{summary.TotalOutputTokens}` output");
        builder.AppendLine($"- Total duration: `{summary.TotalDurationMilliseconds} ms`");
        builder.AppendLine();
        builder.AppendLine("| Case | Expected | Actual | Safe | Exact | Span F1 | Input tokens | Output tokens | Total tokens | Duration | Error |");
        builder.AppendLine("| --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | --- |");

        foreach (var result in summary.Cases)
        {
            var totalTokens = result.InputTokenCount.HasValue
                && result.OutputTokenCount.HasValue
                    ? result.InputTokenCount + result.OutputTokenCount
                    : null;

            builder.AppendLine(
                $"| `{result.CaseId}` | {result.ExpectedAction} | {result.ActualAction} | {result.IsSafe} | {result.IsExactMatch} | {result.SpanF1:F3} | {FormatTokens(result.InputTokenCount)} | {FormatTokens(result.OutputTokenCount)} | {FormatTokens(totalTokens)} | {result.DurationMilliseconds} ms | {result.Error ?? string.Empty} |");
        }

        return builder.ToString();
    }

    private static string FormatTokens(long? tokenCount) =>
        tokenCount?.ToString() ?? "n/a";

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WriteFluency.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}

public sealed record EvaluationReportPaths(
    string Markdown,
    string Json,
    string Highlights);
