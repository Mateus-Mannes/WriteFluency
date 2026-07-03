using System.Text.RegularExpressions;
using WriteFluency.TextComparisons;

namespace WriteFluency.MistakePatternClassification.Evals;

public static partial class MistakePatternClassificationScorer
{
    private const int MaxStudentPhraseLength = 2500;

    public static EvaluationComparisonResult Score(
        EvaluationCase evaluationCase,
        EvaluationComparison expected,
        MistakePatternAnnotation? actual)
    {
        var actualTags = (actual?.Tags ?? [])
            .Select(NormalizeTag)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var expectedTags = expected.ExpectedTags
            .Select(NormalizeTag)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var acceptedTags = (expected.AcceptedTags is { Count: > 0 }
                ? expected.AcceptedTags
                : expected.ExpectedTags)
            .Select(NormalizeTag)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var forbiddenTags = (expected.ForbiddenTags ?? [])
            .Select(NormalizeTag)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var failures = new List<string>();
        var actualTagSet = actualTags.ToHashSet(StringComparer.Ordinal);
        var acceptedTagSet = ExpandTagSet(acceptedTags);

        var missingRequiredTags = expectedTags
            .Where(tag => !TagMatches(tag, actualTagSet))
            .ToArray();
        if (missingRequiredTags.Length > 0)
        {
            failures.Add($"missing_tags:{string.Join(",", missingRequiredTags)}");
        }

        var forbiddenActualTags = forbiddenTags
            .Where(actualTagSet.Contains)
            .ToArray();
        if (forbiddenActualTags.Length > 0)
        {
            failures.Add($"forbidden_tags:{string.Join(",", forbiddenActualTags)}");
        }

        var tagTruePositiveCount = actualTags.Count(acceptedTagSet.Contains);
        var tagsPassed = missingRequiredTags.Length == 0
                         && forbiddenActualTags.Length == 0
                         && actualTags.Length > 0;

        var phrasePassed = ScorePhrase(
            expected,
            actual?.StudentPhrase,
            out var phraseTokenF1,
            out var phraseEditSimilarity,
            failures);

        return new EvaluationComparisonResult(
            expected.ComparisonIndex,
            expected.SourceComparisonIndex,
            expected.OriginalText,
            expected.UserText,
            CreateContextSnippet(
                evaluationCase.OriginalText,
                expected.OriginalTextRange.InitialIndex,
                expected.OriginalTextRange.FinalIndex),
            CreateContextSnippet(
                evaluationCase.UserText,
                expected.UserTextRange.InitialIndex,
                expected.UserTextRange.FinalIndex),
            expectedTags,
            acceptedTags,
            forbiddenTags,
            actualTags,
            actual?.StudentPhrase,
            expected.ReferenceStudentPhrase,
            phraseTokenF1,
            phraseEditSimilarity,
            PhraseAiSimilarityScore: null,
            PhraseAiSimilarityReason: null,
            tagsPassed,
            phrasePassed,
            tagsPassed && phrasePassed,
            failures,
            tagTruePositiveCount,
            actualTags.Length,
            acceptedTags.Length);
    }

    public static string NormalizeTag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = NonTagCharactersRegex()
            .Replace(value.Trim().ToLowerInvariant(), "_");
        normalized = RepeatedUnderscoreRegex().Replace(normalized, "_");
        return normalized.Trim('_');
    }

    private static bool ScorePhrase(
        EvaluationComparison expected,
        string? actualPhrase,
        out double tokenF1,
        out double editSimilarity,
        ICollection<string> failures)
    {
        tokenF1 = 0;
        editSimilarity = 0;

        if (string.IsNullOrWhiteSpace(actualPhrase))
        {
            failures.Add("missing_phrase");
            return false;
        }

        var trimmedPhrase = actualPhrase.Trim();
        var normalizedPhrase = trimmedPhrase.ToLowerInvariant();
        var passed = true;
        tokenF1 = PhraseTokenF1(
            expected.ReferenceStudentPhrase,
            trimmedPhrase);
        editSimilarity = PhraseEditSimilarity(
            expected.ReferenceStudentPhrase,
            trimmedPhrase);

        if (trimmedPhrase.Length > MaxStudentPhraseLength)
        {
            failures.Add("phrase_too_long");
            passed = false;
        }

        if (DiffRestatementRegex().IsMatch(normalizedPhrase))
        {
            failures.Add("phrase_restates_diff");
            passed = false;
        }

        if (TargetWordRegex().IsMatch(normalizedPhrase))
        {
            failures.Add("forbidden_phrase:target");
            passed = false;
        }

        var matchedForbidden = GenericForbiddenPhrasePatterns()
            .Where(pattern => NormalizePhraseForComparison(normalizedPhrase)
                .Contains(NormalizePhraseForComparison(pattern)))
            .ToArray();
        if (matchedForbidden.Length > 0)
        {
            failures.Add($"forbidden_phrase:{string.Join("|", matchedForbidden)}");
            passed = false;
        }

        if (SentenceEndRegex().Count(trimmedPhrase) > 1)
        {
            failures.Add("multiple_sentences");
            passed = false;
        }

        return passed;
    }

    private static EvaluationContextSnippet CreateContextSnippet(
        string text,
        int initialIndex,
        int finalIndex)
    {
        const int contextCharacters = 70;
        var start = Math.Clamp(initialIndex, 0, text.Length - 1);
        var end = Math.Clamp(finalIndex, start, text.Length - 1);
        var expandedStart = Math.Max(0, start - contextCharacters);
        var expandedEnd = Math.Min(text.Length - 1, end + contextCharacters);
        return new EvaluationContextSnippet(
            text[expandedStart..start],
            text[start..(end + 1)],
            text[(end + 1)..(expandedEnd + 1)]);
    }

    private static bool TagMatches(
        string expectedTag,
        IReadOnlySet<string> actualTags) =>
        ExpandTagSet([expectedTag]).Overlaps(actualTags);

    private static HashSet<string> ExpandTagSet(IEnumerable<string> tags)
    {
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            expanded.Add(tag);
            foreach (var alias in TagAliases(tag))
            {
                expanded.Add(alias);
            }
        }

        return expanded;
    }

    private static string[] TagAliases(string tag) =>
        tag switch
        {
            "article" => ["articles_and_small_words"],
            "articles_and_small_words" => ["article", "preposition", "pronoun"],
            "extra_word" => ["missing_or_extra_word"],
            "missing_word" => ["missing_or_extra_word"],
            "missing_or_extra_word" => ["extra_word", "missing_word"],
            "phrase_confusion" => ["phrase_heard_incorrectly"],
            "phrase_heard_incorrectly" => ["phrase_confusion"],
            "modal_verb" => ["verb_form"],
            "verb_form" => ["modal_verb"],
            _ => []
        };

    private static double PhraseTokenF1(
        string expectedPhrase,
        string actualPhrase)
    {
        var expectedTokens = TokenizePhrase(expectedPhrase);
        var actualTokens = TokenizePhrase(actualPhrase);
        if (expectedTokens.Length == 0 || actualTokens.Length == 0)
        {
            return 0;
        }

        var actualCounts = actualTokens
            .GroupBy(token => token, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);
        var truePositives = 0;
        foreach (var token in expectedTokens)
        {
            if (!actualCounts.TryGetValue(token, out var count) || count == 0)
            {
                continue;
            }

            actualCounts[token] = count - 1;
            truePositives++;
        }

        var precision = (double)truePositives / actualTokens.Length;
        var recall = (double)truePositives / expectedTokens.Length;
        return precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
    }

    private static double PhraseEditSimilarity(
        string expectedPhrase,
        string actualPhrase)
    {
        var expected = NormalizePhraseForComparison(expectedPhrase);
        var actual = NormalizePhraseForComparison(actualPhrase);
        if (expected.Length == 0 || actual.Length == 0)
        {
            return 0;
        }

        var distance = LevenshteinDistance(expected, actual);
        return 1 - (double)distance / Math.Max(expected.Length, actual.Length);
    }

    private static string[] TokenizePhrase(string phrase) =>
        PhraseTokenRegex()
            .Matches(NormalizePhraseForComparison(phrase))
            .Select(match => match.Value)
            .ToArray();

    private static string NormalizePhraseForComparison(string phrase)
    {
        var normalized = PhraseComparisonPunctuationRegex()
            .Replace(phrase.ToLowerInvariant(), " ");
        normalized = RepeatedWhitespaceRegex().Replace(normalized, " ");
        return normalized.Trim();
    }

    private static int LevenshteinDistance(string left, string right)
    {
        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var index = 0; index <= right.Length; index++)
        {
            previous[index] = index;
        }

        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;
            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var cost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(
                        current[rightIndex - 1] + 1,
                        previous[rightIndex] + 1),
                    previous[rightIndex - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static string[] GenericForbiddenPhrasePatterns() =>
    [
        "listen for the exact phrase",
        "listen for small words",
        "they shape the phrase",
        "listen for verb endings and tense cues",
        "small function words are easy to confuse",
        "the word is close, but the spelling or sound pattern changes",
        "is a different word",
        "is a different verb phrase"
    ];

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonTagCharactersRegex();

    [GeneratedRegex("_+")]
    private static partial Regex RepeatedUnderscoreRegex();

    [GeneratedRegex(@"you wrote.+(expected|original|should be)")]
    private static partial Regex DiffRestatementRegex();

    [GeneratedRegex(@"\btarget\b")]
    private static partial Regex TargetWordRegex();

    [GeneratedRegex(@"[.!?]")]
    private static partial Regex SentenceEndRegex();

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex PhraseTokenRegex();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex PhraseComparisonPunctuationRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RepeatedWhitespaceRegex();
}
