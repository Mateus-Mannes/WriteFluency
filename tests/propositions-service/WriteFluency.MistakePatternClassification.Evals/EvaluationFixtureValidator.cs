using System.Text.RegularExpressions;

namespace WriteFluency.MistakePatternClassification.Evals;

public static partial class EvaluationFixtureValidator
{
    public static void Validate(IReadOnlyList<EvaluationCase> cases)
    {
        if (cases.Count == 0)
        {
            throw new InvalidOperationException("The evaluation fixture has no cases.");
        }

        foreach (var evaluationCase in cases)
        {
            ValidateCase(evaluationCase);
        }
    }

    private static void ValidateCase(EvaluationCase evaluationCase)
    {
        if (string.IsNullOrWhiteSpace(evaluationCase.CaseId))
        {
            throw new InvalidOperationException("Case id is required.");
        }

        if (evaluationCase.Comparisons.Count == 0)
        {
            throw new InvalidOperationException(
                $"Case '{evaluationCase.CaseId}' has no comparisons.");
        }

        var comparisonIndexes = new HashSet<int>();
        foreach (var comparison in evaluationCase.Comparisons)
        {
            if (!comparisonIndexes.Add(comparison.ComparisonIndex))
            {
                throw new InvalidOperationException(
                    $"Case '{evaluationCase.CaseId}' has duplicate comparison index {comparison.ComparisonIndex}.");
            }

            ValidateRange(
                evaluationCase.CaseId,
                comparison.ComparisonIndex,
                "original",
                evaluationCase.OriginalText,
                comparison.OriginalTextRange.InitialIndex,
                comparison.OriginalTextRange.FinalIndex,
                comparison.OriginalText);
            ValidateRange(
                evaluationCase.CaseId,
                comparison.ComparisonIndex,
                "user",
                evaluationCase.UserText,
                comparison.UserTextRange.InitialIndex,
                comparison.UserTextRange.FinalIndex,
                comparison.UserText);
            ValidateTags(
                evaluationCase.CaseId,
                comparison.ComparisonIndex,
                comparison.ExpectedTags,
                nameof(comparison.ExpectedTags),
                requireValue: true);
            ValidateTags(
                evaluationCase.CaseId,
                comparison.ComparisonIndex,
                comparison.AcceptedTags ?? [],
                nameof(comparison.AcceptedTags),
                requireValue: false);
            ValidateTags(
                evaluationCase.CaseId,
                comparison.ComparisonIndex,
                comparison.ForbiddenTags ?? [],
                nameof(comparison.ForbiddenTags),
                requireValue: false);
            if (string.IsNullOrWhiteSpace(comparison.ReferenceStudentPhrase))
            {
                throw new InvalidOperationException(
                    $"Case '{evaluationCase.CaseId}' comparison {comparison.ComparisonIndex} must define a reference student phrase.");
            }
        }
    }

    private static void ValidateRange(
        string caseId,
        int comparisonIndex,
        string side,
        string fullText,
        int initialIndex,
        int finalIndex,
        string expectedText)
    {
        if (initialIndex < 0
            || finalIndex < initialIndex
            || finalIndex >= fullText.Length)
        {
            throw new InvalidOperationException(
                $"Case '{caseId}' comparison {comparisonIndex} has invalid {side} range.");
        }

        var actualText = fullText[initialIndex..(finalIndex + 1)];
        if (actualText != expectedText)
        {
            throw new InvalidOperationException(
                $"Case '{caseId}' comparison {comparisonIndex} {side} range text mismatch. Expected '{expectedText}', actual '{actualText}'.");
        }
    }

    private static void ValidateTags(
        string caseId,
        int comparisonIndex,
        IReadOnlyList<string> tags,
        string fieldName,
        bool requireValue)
    {
        if (requireValue && tags.Count == 0)
        {
            throw new InvalidOperationException(
                $"Case '{caseId}' comparison {comparisonIndex} must define expected tags.");
        }

        if (tags.Count > 3)
        {
            throw new InvalidOperationException(
                $"Case '{caseId}' comparison {comparisonIndex} {fieldName} has more than 3 tags.");
        }

        foreach (var tag in tags)
        {
            if (!NormalizedTagRegex().IsMatch(tag))
            {
                throw new InvalidOperationException(
                    $"Case '{caseId}' comparison {comparisonIndex} {fieldName} contains non-normalized tag '{tag}'.");
            }
        }
    }

    [GeneratedRegex("^[a-z0-9]+(?:_[a-z0-9]+)*$")]
    private static partial Regex NormalizedTagRegex();
}
