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
        var source = evaluationCase.SourceComparison;
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
