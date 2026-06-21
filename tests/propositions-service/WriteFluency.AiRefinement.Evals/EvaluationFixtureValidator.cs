namespace WriteFluency.AiRefinement.Evals;

public static class EvaluationFixtureValidator
{
    public static void Validate(IReadOnlyList<EvaluationCase> cases)
    {
        foreach (var evaluationCase in cases)
        {
            Validate(evaluationCase);
        }
    }

    private static void Validate(EvaluationCase evaluationCase)
    {
        if (string.IsNullOrWhiteSpace(evaluationCase.Expectation))
        {
            throw new InvalidOperationException(
                $"Evaluation case '{evaluationCase.CaseId}' must explain its expected behavior.");
        }

        var sources = evaluationCase.GetSourceComparisons();
        var expectedDecisions = evaluationCase.GetExpectedDecisions();
        if (sources.Count == 0)
        {
            throw new InvalidOperationException(
                $"Evaluation case '{evaluationCase.CaseId}' must contain at least one source comparison.");
        }

        if (sources.Select(source => source.SourceComparisonIndex).Distinct().Count()
            != sources.Count)
        {
            throw new InvalidOperationException(
                $"Evaluation case '{evaluationCase.CaseId}' contains duplicate source indexes.");
        }

        var focusSourceComparisonIndex =
            evaluationCase.GetFocusSourceComparisonIndex();
        if (sources.All(source =>
                source.SourceComparisonIndex != focusSourceComparisonIndex))
        {
            throw new InvalidOperationException(
                $"Evaluation case '{evaluationCase.CaseId}' references an unknown focus source comparison.");
        }

        foreach (var source in sources)
        {
            var originalSnippet = Slice(
                evaluationCase.OriginalText,
                source.OriginalTextRange,
                evaluationCase.CaseId);
            var userSnippet = Slice(
                evaluationCase.UserText,
                source.UserTextRange,
                evaluationCase.CaseId);

            if (originalSnippet != source.OriginalText
                || userSnippet != source.UserText)
            {
                throw new InvalidOperationException(
                    $"Evaluation case '{evaluationCase.CaseId}' contains source snippets that do not match their ranges.");
            }
        }

        var sourceIndexes = sources
            .Select(source => source.SourceComparisonIndex)
            .Order()
            .ToArray();
        var expectedIndexes = expectedDecisions
            .Select(decision => decision.SourceComparisonIndex)
            .Order()
            .ToArray();
        if (!sourceIndexes.SequenceEqual(expectedIndexes))
        {
            throw new InvalidOperationException(
                $"Evaluation case '{evaluationCase.CaseId}' must define one expected decision for every source comparison.");
        }

        foreach (var decision in expectedDecisions)
        {
            if (decision.ExpectedRanges.Any(range =>
                    range.SourceComparisonIndex != decision.SourceComparisonIndex))
            {
                throw new InvalidOperationException(
                    $"Evaluation case '{evaluationCase.CaseId}' contains an expected range for the wrong source.");
            }
        }
    }

    private static string Slice(
        string text,
        EvaluationTextRange range,
        string caseId)
    {
        if (range.InitialIndex < 0
            || range.FinalIndex < range.InitialIndex
            || range.FinalIndex >= text.Length)
        {
            throw new InvalidOperationException(
                $"Evaluation case '{caseId}' contains an invalid source range.");
        }

        return text.Substring(
            range.InitialIndex,
            range.FinalIndex - range.InitialIndex + 1);
    }
}
