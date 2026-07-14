using System.Net;
using System.Text;

namespace WriteFluency.MistakePatternClassification.Evals;

public static class EvaluationReportWriter
{
    public static async Task<string> WriteAsync(
        EvaluationRunSummary summary,
        CancellationToken cancellationToken)
    {
        var timestamp = summary.ExecutedAtUtc
            .ToLocalTime()
            .ToString("yyyyMMdd-HHmmss");
        var outputDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "artifacts",
            "mistake-pattern-evals",
            timestamp);
        Directory.CreateDirectory(outputDirectory);

        var reportPath = Path.Combine(outputDirectory, "report.html");
        await File.WriteAllTextAsync(
            reportPath,
            BuildHtml(summary),
            Encoding.UTF8,
            cancellationToken);
        return reportPath;
    }

    private static string BuildHtml(EvaluationRunSummary summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine("<title>Mistake Pattern Classification Eval</title>");
        builder.AppendLine("<style>");
        builder.AppendLine(Styles());
        builder.AppendLine("</style></head><body>");
        builder.AppendLine("<main>");
        builder.AppendLine("<header class=\"page-header\">");
        builder.AppendLine("<div>");
        builder.AppendLine("<h1>Mistake Pattern Classification Eval</h1>");
        builder.AppendLine($"<p>{Encode(summary.ExecutedAtUtc.ToString("u"))} · {Encode(summary.Model)} · temperature {summary.Temperature:0.###} · batch {summary.MaxComparisonsPerRequest}</p>");
        builder.AppendLine("</div>");
        builder.AppendLine(Badge(summary.Passed ? "Passed" : "Failed", summary.Passed ? "pass" : "fail"));
        builder.AppendLine("</header>");

        builder.AppendLine("<section class=\"metrics\">");
        builder.AppendLine(Metric("Case runs", $"{summary.PassingCaseRunCount}/{summary.CaseRunCount}"));
        builder.AppendLine(Metric("Comparisons", $"{summary.PassingComparisonCount}/{summary.ComparisonCount}"));
        builder.AppendLine(Metric("Tag precision", summary.TagPrecision.ToString("P1")));
        builder.AppendLine(Metric("Tag recall", summary.TagRecall.ToString("P1")));
        builder.AppendLine(Metric("Tag F1", summary.TagF1.ToString("0.000")));
        builder.AppendLine(Metric("Phrase pass", summary.PhrasePassRate.ToString("P1")));
        builder.AppendLine(Metric("Requests", summary.RequestCount.ToString()));
        builder.AppendLine(Metric("Request time", $"{summary.TotalRequestDurationMilliseconds} ms"));
        builder.AppendLine(Metric("Tokens", TokenSummary(summary.InputTokenCount, summary.OutputTokenCount, summary.TotalTokenCount)));
        builder.AppendLine(Metric("Estimated spend", CostSummary(summary.EstimatedCostUsd, summary.Pricing)));
        builder.AppendLine("</section>");

        builder.AppendLine("<section class=\"filters\">");
        builder.AppendLine("<label>Search <input id=\"search\" type=\"search\" placeholder=\"case, category, tag, phrase\"></label>");
        builder.AppendLine("<label>Status <select id=\"status\"><option value=\"all\">All</option><option value=\"failed\">Failed only</option><option value=\"passed\">Passed only</option></select></label>");
        builder.AppendLine("<label>Category <select id=\"category\"><option value=\"all\">All categories</option>");
        foreach (var category in summary.Runs.Select(run => run.Category).Distinct().Order())
        {
            builder.AppendLine($"<option value=\"{EncodeAttribute(category)}\">{Encode(category)}</option>");
        }
        builder.AppendLine("</select></label>");
        builder.AppendLine("</section>");

        foreach (var run in summary.Runs)
        {
            builder.AppendLine($"<section class=\"case\" data-status=\"{(run.Passed ? "passed" : "failed")}\" data-category=\"{EncodeAttribute(run.Category)}\" data-search=\"{EncodeAttribute(SearchText(run))}\">");
            builder.AppendLine("<details open>");
            builder.AppendLine("<summary>");
            builder.AppendLine("<div>");
            builder.AppendLine($"<h2>{Encode(run.CaseId)}</h2>");
            builder.AppendLine($"<p>{Encode(run.Category)} · Run {run.RunNumber} · {run.DurationMilliseconds} ms</p>");
            if (!string.IsNullOrWhiteSpace(run.Error))
            {
                builder.AppendLine($"<p class=\"error\">{Encode(run.Error)}</p>");
            }
            builder.AppendLine("</div>");
            builder.AppendLine(Badge(run.Passed ? "Passed" : "Failed", run.Passed ? "pass" : "fail"));
            builder.AppendLine("</summary>");
            if (run.Requests.Count > 0)
            {
                builder.AppendLine("<div class=\"requests\">");
                builder.AppendLine("<strong>Classifier and phrase-grader requests</strong>");
                builder.AppendLine("<div class=\"request-grid\">");
                foreach (var request in run.Requests)
                {
                    builder.AppendLine($"""
                    <div class="request-card">
                      <span>{Encode(request.Stage)} · Batch {request.BatchNumber}</span>
                      <strong>{request.ComparisonCount} comparisons · {request.DurationMilliseconds} ms</strong>
                      <small>Start index {request.StartIndex} · {Encode(TokenSummary(request.InputTokenCount, request.OutputTokenCount, request.TotalTokenCount))} · {Encode(RequestCostSummary(request, summary.Pricing))}</small>
                    </div>
                    """);
                }
                builder.AppendLine("</div>");
                builder.AppendLine("</div>");
            }

            foreach (var comparison in run.Comparisons)
            {
                builder.AppendLine($"<article class=\"comparison {(comparison.Passed ? "passed" : "failed")}\">");
                builder.AppendLine("<div class=\"comparison-header\">");
                builder.AppendLine($"<h3>Comparison {comparison.ComparisonIndex} <span>Source {comparison.SourceComparisonIndex}</span></h3>");
                builder.AppendLine(Badge(comparison.Passed ? "Passed" : "Failed", comparison.Passed ? "pass" : "fail"));
                builder.AppendLine("</div>");
                builder.AppendLine("<div class=\"grid\">");
                builder.AppendLine(Panel("Original", comparison.OriginalText, comparison.OriginalContext));
                builder.AppendLine(Panel("User", comparison.UserText, comparison.UserContext));
                builder.AppendLine("</div>");
                builder.AppendLine("<div class=\"grid\">");
                builder.AppendLine(TagPanel(
                    "Expected tags",
                    comparison.ExpectedTags,
                    comparison.AcceptedTags,
                    comparison.ForbiddenTags));
                builder.AppendLine(TagPanel(
                    "Actual tags",
                    comparison.ActualTags,
                    [],
                    []));
                builder.AppendLine("</div>");
                builder.AppendLine("<div class=\"phrase-panel\">");
                builder.AppendLine("<strong>Actual phrase</strong>");
                builder.AppendLine($"<p>{Encode(comparison.ActualPhrase ?? "(missing)")}</p>");
                builder.AppendLine("<strong>Reference phrase</strong>");
                builder.AppendLine($"<p>{Encode(comparison.ReferenceStudentPhrase)}</p>");
                builder.AppendLine($"<small>Token F1 {comparison.PhraseTokenF1:0.000} · Edit similarity {comparison.PhraseEditSimilarity:0.000} · AI similarity {FormatNullable(comparison.PhraseAiSimilarityScore)}</small>");
                if (!string.IsNullOrWhiteSpace(comparison.PhraseAiSimilarityReason))
                {
                    builder.AppendLine("<strong>AI phrase-grade reason</strong>");
                    builder.AppendLine($"<p>{Encode(comparison.PhraseAiSimilarityReason)}</p>");
                }
                builder.AppendLine("</div>");
                if (comparison.Failures.Count > 0)
                {
                    builder.AppendLine("<div class=\"failures\"><strong>Failures</strong>");
                    builder.AppendLine("<ul>");
                    foreach (var failure in comparison.Failures)
                    {
                        builder.AppendLine($"<li>{Encode(failure)}</li>");
                    }
                    builder.AppendLine("</ul></div>");
                }
                builder.AppendLine("</article>");
            }

            builder.AppendLine("</details>");
            builder.AppendLine("</section>");
        }

        builder.AppendLine("</main>");
        builder.AppendLine("<script>");
        builder.AppendLine(Script());
        builder.AppendLine("</script>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static string Metric(string label, string value) =>
        $"<div class=\"metric\"><span>{Encode(label)}</span><strong>{Encode(value)}</strong></div>";

    private static string Panel(
        string title,
        string snippet,
        EvaluationContextSnippet context) =>
        $"""
        <div class="panel">
          <strong>{Encode(title)}</strong>
          <pre>{Encode(snippet)}</pre>
          <small class="context-label">{Encode(title)} phrase context</small>
          <p class="context">{Encode(context.Before)}<mark>{Encode(context.Highlight)}</mark>{Encode(context.After)}</p>
        </div>
        """;

    private static string TokenSummary(long? inputTokens, long? outputTokens, long? totalTokens)
    {
        if (inputTokens is null && outputTokens is null && totalTokens is null)
        {
            return "tokens unavailable";
        }

        return $"in {FormatNullable(inputTokens)} · out {FormatNullable(outputTokens)} · total {FormatNullable(totalTokens)}";
    }

    private static string CostSummary(
        decimal? estimatedCostUsd,
        EvaluationPricing? pricing)
    {
        if (estimatedCostUsd is null || pricing is null)
        {
            return "unavailable";
        }

        return $"{FormatCost(estimatedCostUsd.Value)} (in ${pricing.InputUsdPerMillionTokens:0.####}/1M · out ${pricing.OutputUsdPerMillionTokens:0.####}/1M)";
    }

    private static string RequestCostSummary(
        EvaluationRequestResult request,
        EvaluationPricing? pricing)
    {
        var cost = EvaluationRunSummary.EstimateCost(
            request.InputTokenCount,
            request.OutputTokenCount,
            pricing);
        return cost is null ? "cost unavailable" : $"cost {FormatCost(cost.Value)}";
    }

    private static string FormatNullable(long? value) =>
        value?.ToString() ?? "-";

    private static string FormatNullable(double? value) =>
        value?.ToString("0.000") ?? "-";

    private static string FormatCost(decimal value) =>
        $"${value:0.000000}";

    private static string TagPanel(
        string title,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> acceptedTags,
        IReadOnlyList<string> forbiddenTags)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<div class=\"panel\">");
        builder.AppendLine($"<strong>{Encode(title)}</strong>");
        builder.AppendLine("<div class=\"tags\">");
        foreach (var tag in tags)
        {
            builder.AppendLine($"<span class=\"tag\">{Encode(tag)}</span>");
        }
        builder.AppendLine("</div>");
        if (acceptedTags.Count > 0 && !acceptedTags.SequenceEqual(tags))
        {
            builder.AppendLine($"<small>Accepted: {Encode(string.Join(", ", acceptedTags))}</small>");
        }
        if (forbiddenTags.Count > 0)
        {
            builder.AppendLine($"<small>Forbidden: {Encode(string.Join(", ", forbiddenTags))}</small>");
        }
        builder.AppendLine("</div>");
        return builder.ToString();
    }

    private static string Badge(string text, string kind) =>
        $"<span class=\"badge {kind}\">{Encode(text)}</span>";

    private static string SearchText(EvaluationCaseRunResult run) =>
        string.Join(
            " ",
            run.CaseId,
            run.Category,
            string.Join(" ", run.Comparisons.SelectMany(comparison => comparison.ExpectedTags)),
            string.Join(" ", run.Comparisons.SelectMany(comparison => comparison.ActualTags)),
            string.Join(" ", run.Comparisons.Select(comparison => comparison.ReferenceStudentPhrase)),
            string.Join(" ", run.Comparisons.Select(comparison => comparison.ActualPhrase ?? string.Empty)),
            string.Join(" ", run.Comparisons.Select(comparison => comparison.PhraseAiSimilarityReason ?? string.Empty)));

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string EncodeAttribute(string value) =>
        WebUtility.HtmlEncode(value).Replace("\"", "&quot;");

    private static string Styles() =>
        """
        :root { color-scheme: light; --border:#d8dee9; --text:#172033; --muted:#68758c; --pass:#0f8a4b; --fail:#c92a2a; --bg:#f6f8fb; --panel:#fff; --blue:#2f6fed; }
        * { box-sizing: border-box; }
        body { margin:0; font:14px/1.45 system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif; color:var(--text); background:var(--bg); }
        main { max-width: 1440px; margin: 0 auto; padding: 24px; }
        .page-header, summary, .comparison-header, .filters { display:flex; gap:16px; align-items:center; justify-content:space-between; }
        .page-header { margin-bottom: 18px; }
        h1, h2, h3, p { margin: 0; }
        h1 { font-size: 26px; }
        h2 { font-size: 18px; }
        h3 { font-size: 16px; }
        h3 span { color: var(--muted); font-weight: 600; margin-left: 8px; }
        .page-header p, summary p, small { color: var(--muted); }
        .metrics { display:grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap:10px; margin-bottom:16px; }
        .metric, .case, .comparison, .panel, .phrase-panel, .failures, .requests, .request-card { background:var(--panel); border:1px solid var(--border); border-radius:8px; }
        .metric { padding:12px; }
        .metric span { color:var(--muted); display:block; font-size:12px; text-transform:uppercase; font-weight:700; }
        .metric strong { font-size:20px; }
        .filters { position:sticky; top:0; z-index:2; padding:12px; background:rgba(246,248,251,.96); border:1px solid var(--border); border-radius:8px; margin-bottom:16px; }
        label { display:grid; gap:4px; color:var(--muted); font-size:12px; font-weight:700; text-transform:uppercase; flex:1; }
        input, select { width:100%; padding:10px; border:1px solid #bcc7d8; border-radius:6px; font:inherit; color:var(--text); background:#fff; }
        .case { margin-bottom:14px; overflow:hidden; }
        summary { cursor:pointer; padding:16px; border-bottom:1px solid var(--border); }
        details:not([open]) summary { border-bottom:0; }
        .comparison { margin:14px; padding:14px; }
        .requests { margin:14px; padding:14px; }
        .request-grid { display:grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap:10px; margin-top:10px; }
        .request-card { padding:10px; }
        .request-card span, .context-label { color:var(--muted); display:block; font-size:12px; text-transform:uppercase; font-weight:700; }
        .request-card strong { display:block; margin:4px 0; }
        .comparison.failed { border-left:5px solid var(--fail); }
        .comparison.passed { border-left:5px solid var(--pass); }
        .grid { display:grid; grid-template-columns: 1fr 1fr; gap:12px; margin-top:12px; }
        .panel, .phrase-panel, .failures { padding:12px; }
        pre { white-space:pre-wrap; word-break:break-word; background:#f1f4f8; padding:10px; border-radius:6px; margin:8px 0; font:13px/1.4 ui-monospace,SFMono-Regular,Menlo,monospace; }
        .context { margin:6px 0 0; color:var(--muted); }
        mark { background:#fff3bf; color:var(--text); border-radius:4px; padding:1px 3px; }
        .tags { display:flex; flex-wrap:wrap; gap:6px; margin-top:8px; }
        .tag, .badge { display:inline-flex; align-items:center; border-radius:6px; padding:4px 8px; font-weight:700; }
        .tag { background:#eaf0ff; color:#2457c5; }
        .badge.pass { background:#e7f6ee; color:var(--pass); }
        .badge.fail { background:#fff0f0; color:var(--fail); }
        .error, .failures { color:var(--fail); }
        .hidden { display:none; }
        @media (max-width: 900px) { .metrics, .grid { grid-template-columns:1fr; } .filters { position:static; flex-direction:column; align-items:stretch; } }
        """;

    private static string Script() =>
        """
        const search = document.getElementById('search');
        const status = document.getElementById('status');
        const category = document.getElementById('category');
        const cases = Array.from(document.querySelectorAll('.case'));
        function applyFilters() {
          const query = search.value.trim().toLowerCase();
          const selectedStatus = status.value;
          const selectedCategory = category.value;
          for (const item of cases) {
            const matchesSearch = !query || item.dataset.search.toLowerCase().includes(query);
            const matchesStatus = selectedStatus === 'all' || item.dataset.status === selectedStatus;
            const matchesCategory = selectedCategory === 'all' || item.dataset.category === selectedCategory;
            item.classList.toggle('hidden', !(matchesSearch && matchesStatus && matchesCategory));
          }
        }
        search.addEventListener('input', applyFilters);
        status.addEventListener('change', applyFilters);
        category.addEventListener('change', applyFilters);
        """;
}
