namespace WriteFluency.TextComparisons;

public static class MistakePatternAnnotationSanitizer
{
    private const int MaxTagsPerComparison = 3;
    private const int MaxTagLength = 100;
    private const int MaxStudentPhraseLength = 2500;
    private const string FallbackTag = "uncategorized";
    private const string FallbackStudentPhrase =
        "Review this correction carefully and compare the word form, meaning, and surrounding words.";

    public static IReadOnlyList<MistakePatternAnnotation>? Sanitize(
        IReadOnlyList<MistakePatternAnnotation>? annotations,
        IReadOnlyList<TextComparison> comparisons,
        bool includeFallbackAnnotations = false)
    {
        if (comparisons.Count == 0)
        {
            return null;
        }

        var sourceIndexByComparisonIndex = comparisons
            .Select((comparison, index) => new
            {
                ComparisonIndex = index,
                comparison.SourceComparisonIndex
            })
            .ToDictionary(item => item.ComparisonIndex, item => item.SourceComparisonIndex);
        var sanitized = new List<MistakePatternAnnotation>();
        var seen = new HashSet<int>();

        foreach (var annotation in annotations ?? [])
        {
            if (!sourceIndexByComparisonIndex.TryGetValue(
                    annotation.ComparisonIndex,
                    out var sourceComparisonIndex)
                || sourceComparisonIndex != annotation.SourceComparisonIndex
                || seen.Contains(annotation.ComparisonIndex))
            {
                continue;
            }

            var tags = SanitizeTags(annotation.Tags);
            var phrase = SanitizePhrase(annotation.StudentPhrase);
            if (tags.Count == 0 || string.IsNullOrWhiteSpace(phrase))
            {
                continue;
            }

            sanitized.Add(new MistakePatternAnnotation(
                annotation.ComparisonIndex,
                annotation.SourceComparisonIndex,
                tags,
                phrase));
            seen.Add(annotation.ComparisonIndex);
        }

        if (includeFallbackAnnotations)
        {
            for (var comparisonIndex = 0; comparisonIndex < comparisons.Count; comparisonIndex++)
            {
                if (seen.Contains(comparisonIndex))
                {
                    continue;
                }

                sanitized.Add(new MistakePatternAnnotation(
                    comparisonIndex,
                    comparisons[comparisonIndex].SourceComparisonIndex,
                    [FallbackTag],
                    FallbackStudentPhrase));
            }
        }

        if (sanitized.Count == 0)
        {
            return null;
        }

        return sanitized
            .OrderBy(annotation => annotation.ComparisonIndex)
            .ToArray();
    }

    private static IReadOnlyList<string> SanitizeTags(
        IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            var trimmed = tag.Trim().ToLowerInvariant();
            if (trimmed.Length > MaxTagLength)
            {
                continue;
            }

            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }

            if (normalized.Count == MaxTagsPerComparison)
            {
                break;
            }
        }

        return normalized;
    }

    private static string? SanitizePhrase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MaxStudentPhraseLength
            ? trimmed
            : null;
    }
}
