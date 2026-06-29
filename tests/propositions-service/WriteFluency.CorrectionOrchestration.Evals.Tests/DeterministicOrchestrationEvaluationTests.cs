using Shouldly;
using WriteFluency.CorrectionOrchestration.Evals;

namespace WriteFluency.CorrectionOrchestration.Evals.Tests;

public sealed class DeterministicOrchestrationEvaluationTests
{
    [Fact]
    public async Task EvaluationSuite_ShouldMatchExpectedDeterministicOutput()
    {
        var cases = await EvaluationRuntime.LoadCasesAsync(
            caseId: null,
            CancellationToken.None);
        EvaluationFixtureValidator.Validate(cases);

        var summary = await EvaluationRuntime
            .CreateEvaluator()
            .EvaluateAsync(
                cases,
                runs: 1,
                concurrency: 8,
                CancellationToken.None);
        var reportPaths = await EvaluationReportWriter.WriteAsync(
            summary,
            cases,
            CancellationToken.None);

        summary.Passed.ShouldBeTrue(reportPaths.Html);
        summary.ExactPassCount.ShouldBe(summary.CaseCount, reportPaths.Html);
        summary.ExactPassCount.ShouldBe(26, reportPaths.Html);
        summary.ExactComparisonCount.ShouldBe(
            summary.ComparisonCount,
            reportPaths.Html);
        summary.ExactComparisonCount.ShouldBe(301, reportPaths.Html);
        summary.ExactFocusComparisonCount.ShouldBe(
            summary.FocusComparisonCount,
            reportPaths.Html);
        summary.ExactFocusComparisonCount.ShouldBe(26, reportPaths.Html);
        summary.EquivalentRemovalPrecision.ShouldBe(1.0, reportPaths.Html);
        summary.EquivalentRemovalRecall.ShouldBe(1.0, reportPaths.Html);
        summary.MeanComparisonSpanF1.ShouldBe(1.0, reportPaths.Html);
    }
}
