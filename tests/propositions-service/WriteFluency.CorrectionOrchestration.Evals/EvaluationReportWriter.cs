using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using WriteFluency.TextComparisons;

namespace WriteFluency.CorrectionOrchestration.Evals;

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
            "correction-evals",
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

        var htmlPath = Path.Combine(outputDirectory, "report.html");
        await File.WriteAllTextAsync(
            htmlPath,
            CreateHtml(summary, evaluationCases),
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
            htmlPath,
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
        var actualHighlights = CreateHighlights(
            result.ActualRanges,
            evaluationCase);
        var sourceComparisons = CreateSourceComparisons(evaluationCase);
        var sourceHighlights = CreateSourceHighlights(
            result,
            evaluationCase,
            sourceComparisons);

        return new EvaluationCaseHighlights(
            result.CaseId,
            result.RunNumber,
            evaluationCase.Expectation,
            evaluationCase.GetFocusSourceComparisonIndex(),
            evaluationCase.OriginalText,
            evaluationCase.UserText,
            sourceComparisons,
            result.ExpectedAction,
            result.ActualAction,
            result.IsExactMatch,
            result.Error,
            expectedHighlights,
            actualHighlights,
            sourceHighlights);
    }

    private static IReadOnlyList<EvaluationSourceHighlights> CreateSourceHighlights(
        EvaluationCaseResult result,
        EvaluationCase evaluationCase,
        IReadOnlyList<EvaluationHighlight> sourceComparisons)
    {
        var sourceByIndex = sourceComparisons.ToDictionary(
            source => source.SourceComparisonIndex);
        var focusSourceComparisonIndex =
            evaluationCase.GetFocusSourceComparisonIndex();

        return result.Sources
            .Select(source => new EvaluationSourceHighlights(
                source.SourceComparisonIndex,
                source.SourceComparisonIndex == focusSourceComparisonIndex,
                sourceByIndex[source.SourceComparisonIndex],
                source.ExpectedAction,
                source.ActualAction,
                source.IsSafe,
                source.IsExactMatch,
                source.SpanF1,
                source.Error,
                CreateHighlights(source.ExpectedRanges, evaluationCase),
                CreateHighlights(source.ActualRanges, evaluationCase)))
            .ToList();
    }

    private static IReadOnlyList<EvaluationHighlight> CreateSourceComparisons(
        EvaluationCase evaluationCase) =>
        evaluationCase.GetSourceComparisons()
            .Select(source => new EvaluationHighlight(
                source.SourceComparisonIndex,
                CreateHighlightedText(
                    evaluationCase.OriginalText,
                    source.OriginalTextRange.InitialIndex,
                    source.OriginalTextRange.FinalIndex),
                CreateHighlightedText(
                    evaluationCase.UserText,
                    source.UserTextRange.InitialIndex,
                    source.UserTextRange.FinalIndex)))
            .ToList();

    private static IReadOnlyList<EvaluationHighlight>? CreateHighlights(
        IReadOnlyList<CorrectionComparisonRange> ranges,
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

    private static string CreateHtml(
        EvaluationSummary summary,
        IReadOnlyList<EvaluationCase> evaluationCases)
    {
        var casesById = evaluationCases.ToDictionary(item => item.CaseId);
        var groupedResults = summary.Cases
            .GroupBy(result => result.CaseId)
            .OrderBy(group => group.All(result => result.IsExactMatch))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToList();
        var failedSources = summary.Cases
            .SelectMany(result => result.Sources
                .Where(source => !source.IsExactMatch)
                .Select(source => (Result: result, Source: source)))
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine("<title>Deterministic Orchestration Evaluation</title>");
        builder.AppendLine("<style>");
        builder.AppendLine(HtmlStyles);
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("<header class=\"page-header\">");
        builder.AppendLine("<div>");
        builder.AppendLine("<p class=\"eyebrow\">WriteFluency evaluation</p>");
        builder.AppendLine("<h1>Deterministic Orchestration Report</h1>");
        builder.AppendLine($"<p class=\"subtitle\">{Encode(summary.Model)} · {Encode(summary.PromptVersion)} · {summary.ExecutedAtUtc:yyyy-MM-dd HH:mm:ss} UTC</p>");
        builder.AppendLine("</div>");
        builder.AppendLine($"<span class=\"run-status {(summary.Passed ? "pass" : "fail")}\">{(summary.Passed ? "Passed" : "Failed")}</span>");
        builder.AppendLine("</header>");

        builder.AppendLine("<main>");
        builder.AppendLine("<section class=\"metrics\" aria-label=\"Evaluation summary\">");
        AppendMetric(builder, "Runs", summary.RunCount.ToString(CultureInfo.InvariantCulture));
        AppendMetric(builder, "Case runs exact", $"{summary.ExactPassCount}/{summary.CaseCount}", summary.ExactPassRate);
        AppendMetric(builder, "Case definitions", summary.DefinitionCount.ToString(CultureInfo.InvariantCulture));
        AppendMetric(builder, "Comparisons exact", $"{summary.ExactComparisonCount}/{summary.ComparisonCount}", summary.ExactComparisonRate);
        AppendMetric(builder, "Focus exact", $"{summary.ExactFocusComparisonCount}/{summary.FocusComparisonCount}", summary.FocusExactRate);
        AppendMetric(builder, "Safe outputs", $"{summary.SafeComparisonCount}/{summary.ComparisonCount}", CalculateRate(summary.SafeComparisonCount, summary.ComparisonCount));
        AppendMetric(builder, "Mean span F1", summary.MeanComparisonSpanF1.ToString("F3", CultureInfo.InvariantCulture), summary.MeanComparisonSpanF1);
        AppendMetric(builder, "Flaky cases", summary.FlakyCaseCount.ToString(CultureInfo.InvariantCulture));
        AppendMetric(builder, "Tokens", $"{summary.TotalInputTokens:N0} in / {summary.TotalOutputTokens:N0} out");
        AppendMetric(builder, "Duration", $"{summary.TotalDurationMilliseconds:N0} ms");
        builder.AppendLine("</section>");

        builder.AppendLine("<section class=\"toolbar\" aria-label=\"Report filters\">");
        builder.AppendLine("<label><span>Search</span><input id=\"case-search\" type=\"search\" placeholder=\"Case, category, expectation\"></label>");
        builder.AppendLine("<label><span>Status</span><select id=\"status-filter\"><option value=\"all\">All cases</option><option value=\"failed\">Failed</option><option value=\"flaky\">Flaky</option><option value=\"passed\">Passed</option></select></label>");
        builder.AppendLine("<label><span>Category</span><select id=\"category-filter\"><option value=\"all\">All categories</option>");
        foreach (var category in evaluationCases
                     .Select(item => item.Category)
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            builder.AppendLine($"<option value=\"{EncodeAttribute(category)}\">{Encode(category)}</option>");
        }

        builder.AppendLine("</select></label>");
        builder.AppendLine("<button id=\"expand-failures\" type=\"button\">Expand failures</button>");
        builder.AppendLine("<button id=\"collapse-all\" type=\"button\" class=\"secondary\">Collapse all</button>");
        builder.AppendLine("</section>");

        builder.AppendLine("<section class=\"panel failures\">");
        builder.AppendLine("<div class=\"section-heading\">");
        builder.AppendLine("<div><p class=\"eyebrow\">Debug queue</p><h2>Failed comparisons</h2></div>");
        builder.AppendLine($"<span class=\"count\">{failedSources.Count}</span>");
        builder.AppendLine("</div>");
        if (failedSources.Count == 0)
        {
            builder.AppendLine("<p class=\"empty-state\">No failed comparisons in this run.</p>");
        }
        else
        {
            builder.AppendLine("<div class=\"table-wrap\"><table><thead><tr><th>Case</th><th>Run</th><th>Source</th><th>Expected</th><th>Actual</th><th>F1</th><th>Error</th></tr></thead><tbody>");
            foreach (var failure in failedSources)
            {
                builder.AppendLine(
                    $"<tr><td><a href=\"#case-{EncodeAttribute(failure.Result.CaseId)}\">{Encode(failure.Result.CaseId)}</a></td><td>{failure.Result.RunNumber}</td><td>{failure.Source.SourceComparisonIndex}{(failure.Source.SourceComparisonIndex == failure.Result.FocusSourceComparisonIndex ? " <span class=\"focus-tag\">Focus</span>" : string.Empty)}</td><td>{ActionBadge(failure.Source.ExpectedAction)}</td><td>{ActionBadge(failure.Source.ActualAction)}</td><td>{failure.Source.SpanF1:F3}</td><td>{Encode(failure.Source.Error ?? string.Empty)}</td></tr>");
            }

            builder.AppendLine("</tbody></table></div>");
        }

        builder.AppendLine("</section>");

        builder.AppendLine("<section class=\"case-section\">");
        builder.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">Case analysis</p><h2>Grouped results</h2></div><span id=\"visible-count\" class=\"count\"></span></div>");
        foreach (var group in groupedResults)
        {
            var evaluationCase = casesById[group.Key];
            AppendCaseCard(builder, evaluationCase, group.ToList());
        }

        builder.AppendLine("</section>");
        builder.AppendLine("</main>");
        builder.AppendLine("<script>");
        builder.AppendLine(HtmlScript);
        builder.AppendLine("</script>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static void AppendCaseCard(
        StringBuilder builder,
        EvaluationCase evaluationCase,
        IReadOnlyList<EvaluationCaseResult> results)
    {
        var allExact = results.All(result => result.IsExactMatch);
        var anyExact = results.Any(result => result.IsExactMatch);
        var status = allExact ? "passed" : anyExact ? "flaky" : "failed";
        var sourceResults = results.SelectMany(result => result.Sources).ToList();
        var exactSources = sourceResults.Count(source => source.IsExactMatch);
        var focusSourceComparisonIndex =
            evaluationCase.GetFocusSourceComparisonIndex();
        var focusResults = results
            .Select(result => result.Sources.Single(source =>
                source.SourceComparisonIndex == focusSourceComparisonIndex))
            .ToList();
        var exactFocus = focusResults.Count(source => source.IsExactMatch);
        var meanF1 = sourceResults.Average(source => source.SpanF1);
        var searchText = string.Join(
            " ",
            evaluationCase.CaseId,
            evaluationCase.Category,
            evaluationCase.Expectation);

        builder.AppendLine(
            $"<article id=\"case-{EncodeAttribute(evaluationCase.CaseId)}\" class=\"case-card {status}\" data-status=\"{status}\" data-category=\"{EncodeAttribute(evaluationCase.Category)}\" data-search=\"{EncodeAttribute(searchText.ToLowerInvariant())}\">");
        builder.AppendLine("<details>");
        builder.AppendLine("<summary>");
        builder.AppendLine("<div class=\"case-title\">");
        builder.AppendLine($"<span class=\"status-dot\" aria-hidden=\"true\"></span><div><h3>{Encode(evaluationCase.CaseId)}</h3><p>{Encode(evaluationCase.Category)}</p></div>");
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"case-stats\">");
        builder.AppendLine($"<span>Runs <strong>{results.Count(result => result.IsExactMatch)}/{results.Count}</strong></span>");
        builder.AppendLine($"<span>Comparisons <strong>{exactSources}/{sourceResults.Count}</strong></span>");
        builder.AppendLine($"<span>Focus <strong>{exactFocus}/{focusResults.Count}</strong></span>");
        builder.AppendLine($"<span>F1 <strong>{meanF1:F3}</strong></span>");
        builder.AppendLine($"<span class=\"status-label\">{UpperFirst(status)}</span>");
        builder.AppendLine("</div>");
        builder.AppendLine("</summary>");
        builder.AppendLine("<div class=\"case-body\">");
        builder.AppendLine($"<p class=\"expectation\"><strong>Focus expectation:</strong> {Encode(evaluationCase.Expectation)}</p>");

        foreach (var result in results.OrderBy(item => item.RunNumber))
        {
            AppendRunDetails(builder, evaluationCase, result);
        }

        builder.AppendLine("</div>");
        builder.AppendLine("</details>");
        builder.AppendLine("</article>");
    }

    private static void AppendRunDetails(
        StringBuilder builder,
        EvaluationCase evaluationCase,
        EvaluationCaseResult result)
    {
        var failedCount = result.Sources.Count(source => !source.IsExactMatch);
        builder.AppendLine($"<details class=\"run-detail\"{(failedCount > 0 ? " open" : string.Empty)}>");
        builder.AppendLine("<summary>");
        builder.AppendLine($"<span>Run {result.RunNumber}</span>");
        builder.AppendLine($"<span class=\"run-summary\">{result.Sources.Count(source => source.IsExactMatch)}/{result.Sources.Count} exact · {result.SpanF1:F3} F1 · {FormatTokens(result.InputTokenCount)} / {FormatTokens(result.OutputTokenCount)} tokens · {result.DurationMilliseconds} ms</span>");
        builder.AppendLine("</summary>");
        builder.AppendLine("<div class=\"source-list\">");

        var sourcesByIndex = evaluationCase.GetSourceComparisons()
            .ToDictionary(source => source.SourceComparisonIndex);
        foreach (var sourceResult in result.Sources
                     .OrderBy(source => source.IsExactMatch)
                     .ThenBy(source => source.SourceComparisonIndex))
        {
            var source = sourcesByIndex[sourceResult.SourceComparisonIndex];
            AppendSourceDetails(
                builder,
                evaluationCase,
                result,
                source,
                sourceResult);
        }

        builder.AppendLine("</div>");
        builder.AppendLine("</details>");
    }

    private static void AppendSourceDetails(
        StringBuilder builder,
        EvaluationCase evaluationCase,
        EvaluationCaseResult caseResult,
        EvaluationSourceComparison source,
        EvaluationSourceResult sourceResult)
    {
        var isFocus = source.SourceComparisonIndex
            == caseResult.FocusSourceComparisonIndex;
        var state = sourceResult.IsExactMatch ? "exact" : "failed";
        builder.AppendLine($"<details class=\"source-detail {state}\"{(!sourceResult.IsExactMatch ? " open" : string.Empty)}>");
        builder.AppendLine("<summary>");
        builder.AppendLine($"<span>Source {source.SourceComparisonIndex}{(isFocus ? " <span class=\"focus-tag\">Focus</span>" : string.Empty)}</span>");
        builder.AppendLine($"<span class=\"source-result\">{ActionBadge(sourceResult.ExpectedAction)} <span aria-hidden=\"true\">→</span> {ActionBadge(sourceResult.ActualAction)} · F1 {sourceResult.SpanF1:F3}{(sourceResult.IsExactMatch ? string.Empty : " · Mismatch")}</span>");
        builder.AppendLine("</summary>");
        builder.AppendLine("<div class=\"source-body\">");
        builder.AppendLine("<div class=\"comparison-grid\">");
        AppendTextPair(
            builder,
            "Static source",
            source.OriginalText,
            source.UserText,
            source.OriginalTextRange.InitialIndex,
            source.OriginalTextRange.FinalIndex,
            source.UserTextRange.InitialIndex,
            source.UserTextRange.FinalIndex);
        AppendRangePair(
            builder,
            "Expected",
            evaluationCase,
            sourceResult.ExpectedRanges);
        AppendRangePair(
            builder,
            "Actual result",
            evaluationCase,
            sourceResult.ActualRanges);
        builder.AppendLine("</div>");
        if (!string.IsNullOrWhiteSpace(sourceResult.Error))
        {
            builder.AppendLine($"<p class=\"error-message\"><strong>Validation error:</strong> {Encode(sourceResult.Error)}</p>");
        }

        AppendFinalComparisonDiagnostics(builder, sourceResult);
        AppendTraceDiagnostics(builder, sourceResult);

        builder.AppendLine("</div>");
        builder.AppendLine("</details>");
    }

    private static void AppendFinalComparisonDiagnostics(
        StringBuilder builder,
        EvaluationSourceResult sourceResult)
    {
        if (sourceResult.ExpectedFinalComparisons is null
            && sourceResult.ActualFinalComparisons is null)
        {
            return;
        }

        builder.AppendLine("<section class=\"diagnostic-section\">");
        builder.AppendLine("<div class=\"diagnostic-heading\"><h4>Final output contract</h4>");
        builder.AppendLine($"<span class=\"match-pill {(FinalComparisonsMatch(sourceResult) ? "pass" : "fail")}\">{(FinalComparisonsMatch(sourceResult) ? "Match" : "Mismatch")}</span></div>");
        builder.AppendLine("<div class=\"diagnostic-grid two\">");
        AppendFinalComparisonColumn(
            builder,
            "Expected final",
            sourceResult.ExpectedFinalComparisons ?? []);
        AppendFinalComparisonColumn(
            builder,
            "Actual final",
            sourceResult.ActualFinalComparisons ?? []);
        builder.AppendLine("</div>");
        builder.AppendLine("</section>");
    }

    private static void AppendFinalComparisonColumn(
        StringBuilder builder,
        string title,
        IReadOnlyList<EvaluationFinalComparison> comparisons)
    {
        builder.AppendLine("<section class=\"diagnostic-card\">");
        builder.AppendLine($"<h4>{Encode(title)}</h4>");
        if (comparisons.Count == 0)
        {
            builder.AppendLine("<p class=\"removed-value\">No final comparison</p>");
        }
        else
        {
            foreach (var comparison in comparisons)
            {
                AppendFlagRow(builder, comparison);
                AppendTextValue(
                    builder,
                    "Original",
                    comparison.OriginalText,
                    comparison.OriginalTextRange.InitialIndex,
                    comparison.OriginalTextRange.FinalIndex);
                AppendTextValue(
                    builder,
                    "User",
                    comparison.UserText,
                    comparison.UserTextRange.InitialIndex,
                    comparison.UserTextRange.FinalIndex);
            }
        }

        builder.AppendLine("</section>");
    }

    private static void AppendFlagRow(
        StringBuilder builder,
        EvaluationFinalComparison comparison)
    {
        builder.AppendLine("<div class=\"flag-row\">");
        builder.AppendLine($"<span>Source {comparison.SourceComparisonIndex}</span>");
        builder.AppendLine($"<span class=\"flag {(comparison.IsDeterministicallyRefined ? "on" : "off")}\">Deterministic {comparison.IsDeterministicallyRefined}</span>");
        builder.AppendLine("</div>");
    }

    private static void AppendTraceDiagnostics(
        StringBuilder builder,
        EvaluationSourceResult sourceResult)
    {
        if (sourceResult.ExpectedTrace is null && sourceResult.ActualTrace is null)
        {
            return;
        }

        builder.AppendLine("<section class=\"diagnostic-section\">");
        builder.AppendLine("<div class=\"diagnostic-heading\"><h4>Decision trace contract</h4>");
        builder.AppendLine($"<span class=\"match-pill {(TraceMatches(sourceResult.ExpectedTrace, sourceResult.ActualTrace) ? "pass" : "fail")}\">{(TraceMatches(sourceResult.ExpectedTrace, sourceResult.ActualTrace) ? "Match" : "Mismatch")}</span></div>");
        builder.AppendLine("<div class=\"diagnostic-grid two\">");
        AppendTraceColumn(builder, "Expected trace", sourceResult.ExpectedTrace);
        AppendTraceColumn(builder, "Actual trace", sourceResult.ActualTrace);
        builder.AppendLine("</div>");
        builder.AppendLine("</section>");
    }

    private static void AppendTraceColumn(
        StringBuilder builder,
        string title,
        EvaluationExpectedTraceEntry? trace)
    {
        builder.AppendLine("<section class=\"diagnostic-card\">");
        builder.AppendLine($"<h4>{Encode(title)}</h4>");
        if (trace is null)
        {
            builder.AppendLine("<p class=\"removed-value neutral\">No trace entry</p>");
            builder.AppendLine("</section>");
            return;
        }

        builder.AppendLine($"<p class=\"trace-source\">Source {trace.SourceComparisonIndex}</p>");
        AppendSnapshot(builder, "Initial", trace.Initial);
        AppendStage(builder, "Deterministic", trace.Deterministic);
        builder.AppendLine("</section>");
    }

    private static void AppendStage(
        StringBuilder builder,
        string title,
        EvaluationExpectedStageTrace? stage)
    {
        builder.AppendLine("<div class=\"trace-stage\">");
        builder.AppendLine($"<h5>{Encode(title)}</h5>");
        if (stage is null)
        {
            builder.AppendLine("<p class=\"empty-state\">No stage</p>");
            builder.AppendLine("</div>");
            return;
        }

        builder.AppendLine("<dl class=\"trace-meta\">");
        builder.AppendLine($"<dt>Action</dt><dd>{ActionBadge(stage.Action)}</dd>");
        builder.AppendLine($"<dt>Reason</dt><dd><code>{Encode(stage.ReasonCode ?? string.Empty)}</code></dd>");
        if (!string.IsNullOrWhiteSpace(stage.ValidationStatus))
        {
            builder.AppendLine($"<dt>Validation</dt><dd><code>{Encode(stage.ValidationStatus)}</code></dd>");
        }

        if (!string.IsNullOrWhiteSpace(stage.ValidationFailureReason))
        {
            builder.AppendLine($"<dt>Failure</dt><dd><code>{Encode(stage.ValidationFailureReason)}</code></dd>");
        }

        builder.AppendLine("</dl>");
        AppendSnapshots(builder, "Output", stage.Output);
        if (stage.ProposedOutput is not null)
        {
            AppendSnapshots(builder, "Proposed", stage.ProposedOutput);
        }

        builder.AppendLine("</div>");
    }

    private static void AppendSnapshots(
        StringBuilder builder,
        string title,
        IReadOnlyList<EvaluationComparisonSnapshot> snapshots)
    {
        builder.AppendLine($"<div class=\"snapshot-list\"><p>{Encode(title)}</p>");
        if (snapshots.Count == 0)
        {
            builder.AppendLine("<p class=\"removed-value\">[]</p>");
        }
        else
        {
            foreach (var snapshot in snapshots)
            {
                AppendSnapshot(builder, string.Empty, snapshot);
            }
        }

        builder.AppendLine("</div>");
    }

    private static void AppendSnapshot(
        StringBuilder builder,
        string title,
        EvaluationComparisonSnapshot snapshot)
    {
        builder.AppendLine("<div class=\"snapshot-card\">");
        if (!string.IsNullOrWhiteSpace(title))
        {
            builder.AppendLine($"<p>{Encode(title)}</p>");
        }

        AppendTextValue(
            builder,
            "Original",
            snapshot.OriginalText,
            snapshot.OriginalTextRange.InitialIndex,
            snapshot.OriginalTextRange.FinalIndex);
        AppendTextValue(
            builder,
            "User",
            snapshot.UserText,
            snapshot.UserTextRange.InitialIndex,
            snapshot.UserTextRange.FinalIndex);
        builder.AppendLine("</div>");
    }

    private static void AppendTextPair(
        StringBuilder builder,
        string title,
        string originalText,
        string userText,
        int originalStart,
        int originalEnd,
        int userStart,
        int userEnd)
    {
        builder.AppendLine("<section class=\"comparison-block\">");
        builder.AppendLine($"<h4>{Encode(title)}</h4>");
        AppendTextValue(builder, "Original", originalText, originalStart, originalEnd);
        AppendTextValue(builder, "User", userText, userStart, userEnd);
        builder.AppendLine("</section>");
    }

    private static void AppendRangePair(
        StringBuilder builder,
        string title,
        EvaluationCase evaluationCase,
        IReadOnlyList<CorrectionComparisonRange> ranges)
    {
        builder.AppendLine("<section class=\"comparison-block\">");
        builder.AppendLine($"<h4>{Encode(title)}</h4>");
        if (ranges.Count == 0)
        {
            builder.AppendLine("<p class=\"removed-value\">Comparison removed</p>");
        }
        else
        {
            foreach (var range in ranges)
            {
                AppendTextValue(
                    builder,
                    "Original",
                    SliceOrInvalid(
                        evaluationCase.OriginalText,
                        range.OriginalTextInitialIndex,
                        range.OriginalTextFinalIndex),
                    range.OriginalTextInitialIndex,
                    range.OriginalTextFinalIndex);
                AppendTextValue(
                    builder,
                    "User",
                    SliceOrInvalid(
                        evaluationCase.UserText,
                        range.UserTextInitialIndex,
                        range.UserTextFinalIndex),
                    range.UserTextInitialIndex,
                    range.UserTextFinalIndex);
            }
        }

        builder.AppendLine("</section>");
    }

    private static void AppendTextValue(
        StringBuilder builder,
        string label,
        string text,
        int start,
        int end)
    {
        builder.AppendLine("<div class=\"text-value\">");
        builder.AppendLine($"<div><span>{Encode(label)}</span><code>{start}..{end}</code></div>");
        builder.AppendLine($"<pre>{Encode(text)}</pre>");
        builder.AppendLine("</div>");
    }

    private static bool FinalComparisonsMatch(
        EvaluationSourceResult sourceResult)
    {
        var expected = sourceResult.ExpectedFinalComparisons ?? [];
        var actual = sourceResult.ActualFinalComparisons ?? [];
        if (expected.Count != actual.Count)
        {
            return false;
        }

        for (var index = 0; index < expected.Count; index++)
        {
            if (!FinalComparisonMatches(expected[index], actual[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool FinalComparisonMatches(
        EvaluationFinalComparison expected,
        EvaluationFinalComparison actual) =>
        expected.SourceComparisonIndex == actual.SourceComparisonIndex
        && expected.OriginalTextRange == actual.OriginalTextRange
        && expected.OriginalText == actual.OriginalText
        && expected.UserTextRange == actual.UserTextRange
        && expected.UserText == actual.UserText
        && expected.IsDeterministicallyRefined
            == actual.IsDeterministicallyRefined;

    private static bool TraceMatches(
        EvaluationExpectedTraceEntry? expected,
        EvaluationExpectedTraceEntry? actual)
    {
        if (expected is null || actual is null)
        {
            return expected is null && actual is null;
        }

        return expected.SourceComparisonIndex == actual.SourceComparisonIndex
            && SnapshotMatches(expected.Initial, actual.Initial)
            && StageMatches(expected.Deterministic, actual.Deterministic);
    }

    private static bool StageMatches(
        EvaluationExpectedStageTrace? expected,
        EvaluationExpectedStageTrace? actual)
    {
        if (expected is null || actual is null)
        {
            return expected is null && actual is null;
        }

        return expected.Action == actual.Action
            && (expected.ReasonCode is null
                || expected.ReasonCode == actual.ReasonCode)
            && expected.ValidationStatus == actual.ValidationStatus
            && expected.ValidationFailureReason
                == actual.ValidationFailureReason
            && SnapshotsMatch(expected.Output, actual.Output)
            && SnapshotsMatchNullable(
                expected.ProposedOutput,
                actual.ProposedOutput);
    }

    private static bool SnapshotsMatchNullable(
        IReadOnlyList<EvaluationComparisonSnapshot>? expected,
        IReadOnlyList<EvaluationComparisonSnapshot>? actual)
    {
        if (expected is null || actual is null)
        {
            return expected is null;
        }

        return SnapshotsMatch(expected, actual);
    }

    private static bool SnapshotsMatch(
        IReadOnlyList<EvaluationComparisonSnapshot> expected,
        IReadOnlyList<EvaluationComparisonSnapshot> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        for (var index = 0; index < expected.Count; index++)
        {
            if (!SnapshotMatches(expected[index], actual[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SnapshotMatches(
        EvaluationComparisonSnapshot expected,
        EvaluationComparisonSnapshot actual) =>
        expected.OriginalTextRange == actual.OriginalTextRange
        && expected.OriginalText == actual.OriginalText
        && expected.UserTextRange == actual.UserTextRange
        && expected.UserText == actual.UserText;

    private static void AppendMetric(
        StringBuilder builder,
        string label,
        string value,
        double? ratio = null)
    {
        builder.AppendLine("<div class=\"metric\">");
        builder.AppendLine($"<span>{Encode(label)}</span><strong>{Encode(value)}</strong>");
        if (ratio.HasValue)
        {
            var width = (Math.Clamp(ratio.Value, 0, 1) * 100)
                .ToString("F1", CultureInfo.InvariantCulture);
            builder.AppendLine($"<div class=\"meter\"><i style=\"width:{width}%\"></i></div>");
        }

        builder.AppendLine("</div>");
    }

    private static string ActionBadge(string action) =>
        $"<span class=\"action {EncodeAttribute(action)}\">{Encode(action)}</span>";

    private static string SliceOrInvalid(string text, int start, int end) =>
        start >= 0 && end >= start && end < text.Length
            ? text.Substring(start, end - start + 1)
            : "[invalid range]";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string EncodeAttribute(string value) =>
        WebUtility.HtmlEncode(value).Replace("'", "&#39;", StringComparison.Ordinal);

    private static string UpperFirst(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];

    private static double CalculateRate(int numerator, int denominator) =>
        denominator == 0 ? 0 : (double)numerator / denominator;

    private static string CreateMarkdown(EvaluationSummary summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Deterministic Orchestration Evaluation");
        builder.AppendLine();
        builder.AppendLine($"- Model: `{summary.Model}`");
        builder.AppendLine($"- Prompt: `{summary.PromptVersion}`");
        builder.AppendLine($"- Passed: `{summary.Passed}`");
        builder.AppendLine($"- Runs: `{summary.RunCount}`");
        builder.AppendLine($"- Exact case runs: `{summary.ExactPassCount}/{summary.CaseCount}` ({summary.ExactPassRate:P1})");
        builder.AppendLine($"- Exact comparisons: `{summary.ExactComparisonCount}/{summary.ComparisonCount}` ({summary.ExactComparisonRate:P1})");
        builder.AppendLine($"- Exact focus comparisons: `{summary.ExactFocusComparisonCount}/{summary.FocusComparisonCount}` ({summary.FocusExactRate:P1})");
        builder.AppendLine($"- Safe comparisons: `{summary.SafeComparisonCount}/{summary.ComparisonCount}`");
        builder.AppendLine($"- Flaky cases: `{summary.FlakyCaseCount}`");
        builder.AppendLine($"- Equivalent removal precision: `{summary.EquivalentRemovalPrecision:P1}`");
        builder.AppendLine($"- Equivalent removal recall: `{summary.EquivalentRemovalRecall:P1}`");
        builder.AppendLine($"- Mean span F1: `{summary.MeanSpanF1:F3}`");
        builder.AppendLine($"- Mean comparison span F1: `{summary.MeanComparisonSpanF1:F3}`");
        builder.AppendLine($"- Invalid outputs: `{summary.InvalidOutputCount}`");
        builder.AppendLine($"- Model failures: `{summary.ModelFailureCount}`");
        builder.AppendLine($"- Genuine errors removed: `{summary.GenuineErrorRemovalCount}`");
        builder.AppendLine($"- Tokens: `{summary.TotalInputTokens}` input / `{summary.TotalOutputTokens}` output");
        builder.AppendLine($"- Total duration: `{summary.TotalDurationMilliseconds} ms`");
        builder.AppendLine();
        builder.AppendLine("| Case | Run | Expected | Actual | Safe | Exact | Comparisons | Span F1 | Input tokens | Output tokens | Total tokens | Duration | Error |");
        builder.AppendLine("| --- | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |");

        foreach (var result in summary.Cases)
        {
            var totalTokens = result.InputTokenCount.HasValue
                && result.OutputTokenCount.HasValue
                    ? result.InputTokenCount + result.OutputTokenCount
                    : null;

            builder.AppendLine(
                $"| `{result.CaseId}` | {result.RunNumber} | {result.ExpectedAction} | {result.ActualAction} | {result.IsSafe} | {result.IsExactMatch} | {result.Sources.Count(source => source.IsExactMatch)}/{result.Sources.Count} | {result.SpanF1:F3} | {FormatTokens(result.InputTokenCount)} | {FormatTokens(result.OutputTokenCount)} | {FormatTokens(totalTokens)} | {result.DurationMilliseconds} ms | {result.Error ?? string.Empty} |");
        }

        var multiSourceCases = summary.Cases
            .Where(result => result.Sources.Count > 1)
            .ToList();
        if (multiSourceCases.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Multi-Comparison Cases");

            foreach (var result in multiSourceCases)
            {
                builder.AppendLine();
                builder.AppendLine($"### `{result.CaseId}` · Run {result.RunNumber}");
                builder.AppendLine();
                builder.AppendLine("| Source | Expected | Actual | Safe | Exact | Span F1 | Error |");
                builder.AppendLine("| ---: | --- | --- | --- | --- | ---: | --- |");

                foreach (var source in result.Sources)
                {
                    builder.AppendLine(
                        $"| {source.SourceComparisonIndex} | {source.ExpectedAction} | {source.ActualAction} | {source.IsSafe} | {source.IsExactMatch} | {source.SpanF1:F3} | {source.Error ?? string.Empty} |");
                }
            }
        }

        return builder.ToString();
    }

    private static string FormatTokens(long? tokenCount) =>
        tokenCount?.ToString() ?? "n/a";

    private const string HtmlStyles = """
        :root {
          color-scheme: light;
          --page: #f4f6f8;
          --surface: #ffffff;
          --surface-muted: #f8fafc;
          --text: #172033;
          --muted: #667085;
          --border: #d8dee8;
          --blue: #2563eb;
          --green: #087f5b;
          --green-bg: #e8f7f0;
          --red: #c92a2a;
          --red-bg: #fff0f0;
          --amber: #a15c00;
          --amber-bg: #fff5db;
          --shadow: 0 8px 24px rgba(23, 32, 51, 0.08);
        }
        * { box-sizing: border-box; }
        body {
          margin: 0;
          background: var(--page);
          color: var(--text);
          font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
          line-height: 1.5;
        }
        .page-header {
          display: flex;
          justify-content: space-between;
          align-items: flex-start;
          gap: 24px;
          padding: 32px max(24px, calc((100vw - 1440px) / 2));
          background: #18212f;
          color: #fff;
        }
        h1, h2, h3, h4, p { margin-top: 0; }
        h1 { margin-bottom: 6px; font-size: 32px; letter-spacing: 0; }
        h2 { margin-bottom: 0; font-size: 22px; letter-spacing: 0; }
        h3 { margin-bottom: 2px; font-size: 16px; letter-spacing: 0; }
        h4 { margin-bottom: 10px; font-size: 13px; text-transform: uppercase; color: var(--muted); letter-spacing: 0; }
        h5 { margin: 0 0 8px; font-size: 12px; text-transform: uppercase; color: var(--muted); letter-spacing: 0; }
        .eyebrow { margin-bottom: 5px; color: #aeb9ca; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0; }
        .subtitle { margin: 0; color: #cbd3df; }
        .run-status {
          padding: 7px 12px;
          border-radius: 6px;
          font-weight: 700;
          border: 1px solid currentColor;
        }
        .run-status.pass { color: #91e6c4; }
        .run-status.fail { color: #ffb1b1; }
        main { width: min(1440px, calc(100% - 48px)); margin: 24px auto 56px; }
        .metrics {
          display: grid;
          grid-template-columns: repeat(4, minmax(150px, 1fr));
          gap: 12px;
          margin-bottom: 18px;
        }
        .metric {
          min-height: 92px;
          padding: 15px;
          background: var(--surface);
          border: 1px solid var(--border);
          border-radius: 8px;
          box-shadow: 0 2px 8px rgba(23, 32, 51, 0.04);
        }
        .metric span { display: block; color: var(--muted); font-size: 12px; font-weight: 600; }
        .metric strong { display: block; margin-top: 5px; font-size: 20px; }
        .meter { height: 4px; margin-top: 11px; overflow: hidden; background: #e9edf3; border-radius: 2px; }
        .meter i { display: block; height: 100%; background: var(--blue); }
        .toolbar {
          position: sticky;
          top: 0;
          z-index: 10;
          display: grid;
          grid-template-columns: minmax(240px, 1fr) 170px 220px auto auto;
          gap: 10px;
          align-items: end;
          padding: 12px;
          margin-bottom: 18px;
          background: rgba(244, 246, 248, 0.96);
          border: 1px solid var(--border);
          border-radius: 8px;
          backdrop-filter: blur(8px);
        }
        label span { display: block; margin-bottom: 4px; color: var(--muted); font-size: 11px; font-weight: 700; text-transform: uppercase; }
        input, select, button {
          width: 100%;
          height: 38px;
          border: 1px solid #b9c2d0;
          border-radius: 6px;
          background: #fff;
          color: var(--text);
          font: inherit;
        }
        input, select { padding: 0 10px; }
        button { width: auto; padding: 0 14px; border-color: var(--blue); background: var(--blue); color: #fff; cursor: pointer; font-weight: 700; }
        button.secondary { border-color: #aeb7c5; background: #fff; color: var(--text); }
        .panel, .case-card {
          background: var(--surface);
          border: 1px solid var(--border);
          border-radius: 8px;
          box-shadow: var(--shadow);
        }
        .panel { padding: 18px; margin-bottom: 24px; }
        .section-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; margin-bottom: 14px; }
        .section-heading .eyebrow { color: var(--muted); }
        .count { min-width: 32px; padding: 4px 8px; border-radius: 5px; background: #e8edf5; text-align: center; font-weight: 700; }
        .table-wrap { overflow-x: auto; }
        table { width: 100%; border-collapse: collapse; font-size: 13px; }
        th, td { padding: 9px 10px; border-bottom: 1px solid #e6eaf0; text-align: left; vertical-align: top; }
        th { color: var(--muted); font-size: 11px; text-transform: uppercase; }
        a { color: var(--blue); font-weight: 600; text-decoration: none; }
        .case-section { display: grid; gap: 10px; }
        .case-section > .section-heading { margin-top: 4px; }
        .case-card { overflow: clip; scroll-margin-top: 92px; }
        .case-card.hidden { display: none; }
        details > summary { cursor: pointer; list-style: none; }
        details > summary::-webkit-details-marker { display: none; }
        .case-card > details > summary {
          display: flex;
          justify-content: space-between;
          align-items: center;
          gap: 20px;
          min-height: 72px;
          padding: 14px 16px;
        }
        .case-card > details > summary::after, .run-detail > summary::after, .source-detail > summary::after {
          content: "›";
          margin-left: 8px;
          color: var(--muted);
          font-size: 22px;
          transform: rotate(90deg);
        }
        details[open] > summary::after { transform: rotate(-90deg); }
        .case-title { display: flex; align-items: center; gap: 10px; min-width: 0; }
        .case-title p { margin: 0; color: var(--muted); font-size: 12px; }
        .status-dot { width: 9px; height: 9px; flex: 0 0 auto; border-radius: 50%; background: var(--red); }
        .case-card.passed .status-dot { background: var(--green); }
        .case-card.flaky .status-dot { background: var(--amber); }
        .case-stats { display: flex; align-items: center; gap: 16px; margin-left: auto; color: var(--muted); font-size: 12px; white-space: nowrap; }
        .case-stats strong { color: var(--text); }
        .status-label { min-width: 55px; padding: 4px 7px; border-radius: 5px; background: var(--red-bg); color: var(--red); text-align: center; font-weight: 700; }
        .passed .status-label { background: var(--green-bg); color: var(--green); }
        .flaky .status-label { background: var(--amber-bg); color: var(--amber); }
        .case-body { padding: 0 16px 16px; border-top: 1px solid var(--border); }
        .expectation { padding: 12px; margin: 14px 0; border-left: 3px solid var(--blue); background: #f1f5ff; }
        .run-detail { margin-top: 10px; border: 1px solid var(--border); border-radius: 6px; }
        .run-detail > summary, .source-detail > summary {
          display: flex;
          align-items: center;
          gap: 12px;
          padding: 10px 12px;
          font-weight: 700;
        }
        .run-summary, .source-result { margin-left: auto; color: var(--muted); font-size: 12px; font-weight: 500; }
        .source-list { padding: 0 10px 10px; border-top: 1px solid var(--border); }
        .source-detail { margin-top: 8px; border: 1px solid #dfe4eb; border-left: 4px solid var(--green); border-radius: 5px; }
        .source-detail.failed { border-left-color: var(--red); }
        .source-body { padding: 12px; border-top: 1px solid var(--border); background: var(--surface-muted); }
        .comparison-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 10px; }
        .comparison-block { padding: 12px; border: 1px solid var(--border); border-radius: 6px; background: #fff; }
        .diagnostic-section { margin-top: 12px; padding: 12px; border: 1px solid var(--border); border-radius: 6px; background: #fff; }
        .diagnostic-heading { display: flex; justify-content: space-between; align-items: center; gap: 10px; margin-bottom: 10px; }
        .diagnostic-heading h4 { margin: 0; }
        .diagnostic-grid { display: grid; gap: 10px; }
        .diagnostic-grid.two { grid-template-columns: repeat(2, minmax(0, 1fr)); }
        .diagnostic-card { min-width: 0; padding: 12px; border: 1px solid var(--border); border-radius: 6px; background: var(--surface-muted); }
        .match-pill { padding: 3px 7px; border-radius: 5px; font-size: 11px; font-weight: 800; }
        .match-pill.pass { background: var(--green-bg); color: var(--green); }
        .match-pill.fail { background: var(--red-bg); color: var(--red); }
        .flag-row { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; margin-bottom: 10px; color: var(--muted); font-size: 11px; font-weight: 700; }
        .flag { padding: 2px 6px; border-radius: 4px; background: #e9edf3; color: var(--muted); }
        .flag.on { background: #e8efff; color: #1d4ed8; }
        .trace-source { margin: 0 0 10px; color: var(--muted); font-size: 12px; font-weight: 700; }
        .trace-stage { margin-top: 12px; padding-top: 10px; border-top: 1px solid var(--border); }
        .trace-meta { display: grid; grid-template-columns: minmax(70px, auto) 1fr; gap: 5px 8px; margin: 0 0 10px; font-size: 12px; }
        .trace-meta dt { color: var(--muted); font-weight: 700; }
        .trace-meta dd { margin: 0; min-width: 0; }
        .snapshot-list > p, .snapshot-card > p { margin: 0 0 6px; color: var(--muted); font-size: 11px; font-weight: 800; text-transform: uppercase; }
        .snapshot-card { margin-top: 8px; padding: 8px; border: 1px dashed #cbd3df; border-radius: 5px; background: #fff; }
        .text-value + .text-value { margin-top: 10px; }
        .text-value > div { display: flex; justify-content: space-between; gap: 8px; color: var(--muted); font-size: 11px; font-weight: 700; text-transform: uppercase; }
        code { color: #475467; font-size: 11px; }
        pre { min-height: 46px; margin: 5px 0 0; padding: 9px; overflow-wrap: anywhere; white-space: pre-wrap; border-radius: 4px; background: #f3f5f8; color: #202939; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; }
        .removed-value { padding: 10px; margin: 0; border-radius: 4px; background: var(--green-bg); color: var(--green); font-weight: 700; }
        .removed-value.neutral { background: #e9edf3; color: var(--muted); }
        .error-message { margin: 10px 0 0; color: var(--red); }
        .focus-tag { padding: 2px 5px; border-radius: 4px; background: #e8efff; color: #1d4ed8; font-size: 10px; font-weight: 800; text-transform: uppercase; }
        .action { display: inline-block; padding: 2px 6px; border-radius: 4px; background: #e9edf3; font-size: 11px; font-weight: 700; }
        .action.remove { background: var(--green-bg); color: var(--green); }
        .action.error { background: var(--red-bg); color: var(--red); }
        .action.shrink, .action.split { background: #e8efff; color: #1d4ed8; }
        .empty-state { margin: 0; color: var(--muted); }
        @media (max-width: 1000px) {
          .metrics { grid-template-columns: repeat(2, minmax(140px, 1fr)); }
          .toolbar { grid-template-columns: 1fr 1fr; }
          .comparison-grid { grid-template-columns: 1fr; }
          .diagnostic-grid.two { grid-template-columns: 1fr; }
          .case-stats span:not(.status-label) { display: none; }
        }
        @media (max-width: 620px) {
          .page-header { padding: 24px 18px; }
          main { width: calc(100% - 24px); margin-top: 12px; }
          .metrics { grid-template-columns: 1fr 1fr; }
          .toolbar { position: static; grid-template-columns: 1fr; }
          .case-card > details > summary { align-items: flex-start; }
          .run-summary, .source-result { display: none; }
        }
        """;

    private const string HtmlScript = """
        const search = document.querySelector('#case-search');
        const statusFilter = document.querySelector('#status-filter');
        const categoryFilter = document.querySelector('#category-filter');
        const cards = [...document.querySelectorAll('.case-card')];
        const visibleCount = document.querySelector('#visible-count');

        function applyFilters() {
          const query = search.value.trim().toLowerCase();
          const status = statusFilter.value;
          const category = categoryFilter.value;
          let visible = 0;

          for (const card of cards) {
            const matches = (!query || card.dataset.search.includes(query))
              && (status === 'all' || card.dataset.status === status)
              && (category === 'all' || card.dataset.category === category);
            card.classList.toggle('hidden', !matches);
            if (matches) visible++;
          }

          visibleCount.textContent = `${visible}/${cards.length}`;
        }

        search.addEventListener('input', applyFilters);
        statusFilter.addEventListener('change', applyFilters);
        categoryFilter.addEventListener('change', applyFilters);

        document.querySelector('#expand-failures').addEventListener('click', () => {
          for (const card of cards) {
            if (card.dataset.status !== 'passed' && !card.classList.contains('hidden')) {
              card.querySelector(':scope > details').open = true;
              card.querySelectorAll('.run-detail, .source-detail.failed').forEach(item => item.open = true);
            }
          }
        });

        document.querySelector('#collapse-all').addEventListener('click', () => {
          document.querySelectorAll('details').forEach(item => item.open = false);
        });

        applyFilters();
        """;

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
    string Html,
    string Json,
    string Highlights);
