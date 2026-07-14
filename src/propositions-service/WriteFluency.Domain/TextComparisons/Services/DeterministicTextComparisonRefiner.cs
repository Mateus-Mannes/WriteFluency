namespace WriteFluency.TextComparisons;

public sealed class DeterministicTextComparisonRefiner
{
    private const string BoundaryRefinementReasonCode =
        "deterministic_boundary_refinement";
    private const int MaxAnchorWords = 4;
    private const int MaxEquivalentEdgeWords = 6;
    private const int MaxFunctionAnchorPhraseWords = 4;
    private const int MaxSplitCandidateWordPairs = 2500;

    private readonly DeterministicTextEquivalenceService _equivalenceService;

    public DeterministicTextComparisonRefiner(
        DeterministicTextEquivalenceService equivalenceService)
    {
        _equivalenceService = equivalenceService;
    }

    public DeterministicTextComparisonRefinementResult Refine(
        string originalText,
        string userText,
        IReadOnlyList<TextComparison> comparisons)
    {
        var output = new List<TextComparison>();
        var trace = new Dictionary<int, CorrectionTraceEntry>();
        var removedComparisonCount = 0;

        foreach (var comparison in comparisons)
        {
            var initialSource = new ComparisonRange(
                comparison.SourceComparisonIndex,
                comparison.OriginalTextRange,
                comparison.UserTextRange);
            var source = TryTrimAdjacentBoundaryOverlap(
                    originalText,
                    userText,
                    initialSource,
                    out var boundaryTrimmed)
                ? boundaryTrimmed
                : initialSource;

            var equivalence = _equivalenceService.Evaluate(
                TextRangeNavigator.Slice(
                    originalText,
                    source.OriginalTextRange),
                TextRangeNavigator.Slice(
                    userText,
                    source.UserTextRange));
            if (equivalence.IsEquivalent)
            {
                removedComparisonCount++;
                trace[comparison.SourceComparisonIndex] =
                    CreateTrace(
                        comparison,
                        CorrectionRefinementActions.Remove,
                        equivalence.ReasonCode ?? "normalized_equivalence",
                        []);
                continue;
            }

            var refinedRanges = RefineRange(originalText, userText, source);
            var refinedComparisons = refinedRanges
                    .Select(range => ToComparison(
                    originalText,
                    userText,
                    range,
                    isDeterministicallyRefined: !IsSameRange(initialSource, range)))
                .Where(comparison => comparison is not null)
                .Select(comparison => comparison!)
                .ToList();

            if (refinedComparisons.Count == 0)
            {
                output.Add(comparison);
                continue;
            }

            output.AddRange(refinedComparisons);

            if (refinedComparisons.Any(item =>
                    item.IsDeterministicallyRefined))
            {
                trace[comparison.SourceComparisonIndex] =
                    CreateTrace(
                        comparison,
                        CorrectionRefinementActions.Refine,
                        BoundaryRefinementReasonCode,
                        refinedComparisons.Select(ToSnapshot).ToList());
            }
        }

        return new DeterministicTextComparisonRefinementResult(
            output,
            trace,
            removedComparisonCount,
            removedComparisonCount > 0 || trace.Count > 0);
    }

    private bool TryTrimAdjacentBoundaryOverlap(
        string originalText,
        string userText,
        ComparisonRange source,
        out ComparisonRange trimmed)
    {
        trimmed = source;
        var changed = false;
        var textRange = new TextRange(0, originalText.Length - 1);
        var userTextRange = new TextRange(0, userText.Length - 1);

        var keepTrimming = true;
        while (keepTrimming)
        {
            keepTrimming = false;
            if (TryTrimTrailingBoundaryWord(
                    userText,
                    trimmed.UserTextRange,
                    originalText,
                    textRange,
                    trimmed.OriginalTextRange.FinalIndex,
                    out var userTrailingTrimmed))
            {
                trimmed = trimmed with { UserTextRange = userTrailingTrimmed };
                changed = true;
                keepTrimming = true;
                continue;
            }

            if (TryTrimTrailingBoundaryWord(
                    originalText,
                    trimmed.OriginalTextRange,
                    userText,
                    userTextRange,
                    trimmed.UserTextRange.FinalIndex,
                    out var originalTrailingTrimmed))
            {
                trimmed = trimmed with
                {
                    OriginalTextRange = originalTrailingTrimmed
                };
                changed = true;
                keepTrimming = true;
                continue;
            }

            if (TryTrimLeadingBoundaryWord(
                    userText,
                    trimmed.UserTextRange,
                    originalText,
                    textRange,
                    trimmed.OriginalTextRange.InitialIndex,
                    out var userLeadingTrimmed))
            {
                trimmed = trimmed with { UserTextRange = userLeadingTrimmed };
                changed = true;
                keepTrimming = true;
                continue;
            }

            if (TryTrimLeadingBoundaryWord(
                    originalText,
                    trimmed.OriginalTextRange,
                    userText,
                    userTextRange,
                    trimmed.UserTextRange.InitialIndex,
                    out var originalLeadingTrimmed))
            {
                trimmed = trimmed with
                {
                    OriginalTextRange = originalLeadingTrimmed
                };
                changed = true;
                keepTrimming = true;
            }
        }

        return changed;
    }

    private bool TryTrimTrailingBoundaryWord(
        string text,
        TextRange range,
        string oppositeText,
        TextRange oppositeFullRange,
        int oppositeAfterIndex,
        out TextRange trimmed)
    {
        trimmed = range;
        if (!TextRangeNavigator.TryGetLastWord(text, range, out var boundaryWord)
            || !TextRangeNavigator.TryGetNextWord(
                oppositeText,
                oppositeFullRange,
                oppositeAfterIndex,
                out var oppositeNextWord)
            || !AreEquivalent(
                TextRangeNavigator.Slice(text, boundaryWord),
                TextRangeNavigator.Slice(oppositeText, oppositeNextWord)))
        {
            return false;
        }

        var candidate = TextRangeNavigator.TrimTrailingWord(
            text,
            range,
            boundaryWord);
        if (!HasWords(text, candidate))
        {
            return false;
        }

        trimmed = candidate;
        return true;
    }

    private bool TryTrimLeadingBoundaryWord(
        string text,
        TextRange range,
        string oppositeText,
        TextRange oppositeFullRange,
        int oppositeBeforeIndex,
        out TextRange trimmed)
    {
        trimmed = range;
        if (!TextRangeNavigator.TryGetFirstWord(text, range, out var boundaryWord)
            || !TextRangeNavigator.TryGetPreviousWord(
                oppositeText,
                oppositeFullRange,
                oppositeBeforeIndex,
                out var oppositePreviousWord)
            || !AreEquivalent(
                TextRangeNavigator.Slice(text, boundaryWord),
                TextRangeNavigator.Slice(oppositeText, oppositePreviousWord)))
        {
            return false;
        }

        var candidate = TextRangeNavigator.TrimLeadingWord(
            text,
            range,
            boundaryWord);
        if (!HasWords(text, candidate))
        {
            return false;
        }

        trimmed = candidate;
        return true;
    }

    private IReadOnlyList<ComparisonRange> RefineRange(
        string originalText,
        string userText,
        ComparisonRange source)
    {
        if (TrySplit(
                originalText,
                userText,
                source,
                out var sourceSplit))
        {
            return sourceSplit;
        }

        if (!TryShrinkBoundaries(
                originalText,
                userText,
                source,
                out var shrunken))
        {
            return [source];
        }

        return TrySplit(
            originalText,
            userText,
            shrunken,
            out var split)
            ? split
            : [shrunken];
    }

    private bool TryShrinkBoundaries(
        string originalText,
        string userText,
        ComparisonRange source,
        out ComparisonRange shrunken)
    {
        shrunken = source;

        var changed = false;
        while (TryTrimPrefix(
                   originalText,
                   userText,
                   shrunken,
                   out var prefixTrimmed))
        {
            shrunken = prefixTrimmed;
            changed = true;
        }

        while (TryTrimSuffix(
                   originalText,
                   userText,
                   shrunken,
                   out var suffixTrimmed))
        {
            shrunken = suffixTrimmed;
            changed = true;
        }

        return changed || !IsSameRange(source, shrunken);
    }

    private bool TryTrimPrefix(
        string originalText,
        string userText,
        ComparisonRange range,
        out ComparisonRange trimmed)
    {
        trimmed = range;
        var originalWords = TextRangeNavigator.GetWords(
            originalText,
            range.OriginalTextRange);
        var userWords = TextRangeNavigator.GetWords(
            userText,
            range.UserTextRange);

        if (originalWords.Count <= 1 || userWords.Count <= 1)
        {
            return false;
        }

        if (!TryFindEquivalentEdge(
                originalText,
                originalWords,
                userText,
                userWords,
                fromStart: true,
                out var match))
        {
            return false;
        }

        var originalStart = SkipIgnorableForward(
            originalText,
            originalWords[match.OriginalWordCount - 1].FinalIndex + 1,
            range.OriginalTextRange.FinalIndex);
        var userStart = SkipIgnorableForward(
            userText,
            userWords[match.UserWordCount - 1].FinalIndex + 1,
            range.UserTextRange.FinalIndex);

        if (originalStart > range.OriginalTextRange.FinalIndex
            || userStart > range.UserTextRange.FinalIndex)
        {
            return false;
        }

        trimmed = range with
        {
            OriginalTextRange = range.OriginalTextRange
                with { InitialIndex = originalStart },
            UserTextRange = range.UserTextRange
                with { InitialIndex = userStart }
        };
        return HasWords(originalText, trimmed.OriginalTextRange)
            && HasWords(userText, trimmed.UserTextRange);
    }

    private bool TryTrimSuffix(
        string originalText,
        string userText,
        ComparisonRange range,
        out ComparisonRange trimmed)
    {
        trimmed = range;
        var originalWords = TextRangeNavigator.GetWords(
            originalText,
            range.OriginalTextRange);
        var userWords = TextRangeNavigator.GetWords(
            userText,
            range.UserTextRange);

        if (originalWords.Count <= 1 || userWords.Count <= 1)
        {
            return false;
        }

        if (!TryFindEquivalentEdge(
                originalText,
                originalWords,
                userText,
                userWords,
                fromStart: false,
                out var match))
        {
            return false;
        }

        var originalEnd = SkipIgnorableBackward(
            originalText,
            originalWords[originalWords.Count - match.OriginalWordCount]
                .InitialIndex - 1,
            range.OriginalTextRange.InitialIndex);
        var userEnd = SkipIgnorableBackward(
            userText,
            userWords[userWords.Count - match.UserWordCount]
                .InitialIndex - 1,
            range.UserTextRange.InitialIndex);

        if (originalEnd < range.OriginalTextRange.InitialIndex
            || userEnd < range.UserTextRange.InitialIndex)
        {
            return false;
        }

        trimmed = range with
        {
            OriginalTextRange = range.OriginalTextRange
                with { FinalIndex = originalEnd },
            UserTextRange = range.UserTextRange
                with { FinalIndex = userEnd }
        };
        return HasWords(originalText, trimmed.OriginalTextRange)
            && HasWords(userText, trimmed.UserTextRange);
    }

    private bool TryFindEquivalentEdge(
        string originalText,
        IReadOnlyList<TextRange> originalWords,
        string userText,
        IReadOnlyList<TextRange> userWords,
        bool fromStart,
        out EdgeMatch match)
    {
        match = new EdgeMatch(0, 0);
        var bestScore = 0;

        var maxOriginalCount = Math.Min(
            MaxEquivalentEdgeWords,
            originalWords.Count - 1);
        var maxUserCount = Math.Min(
            MaxEquivalentEdgeWords,
            userWords.Count - 1);

        for (var originalCount = 1;
             originalCount <= maxOriginalCount;
             originalCount++)
        {
            for (var userCount = 1;
                 userCount <= maxUserCount;
                 userCount++)
            {
                var originalRange = fromStart
                    ? new TextRange(
                        originalWords[0].InitialIndex,
                        originalWords[originalCount - 1].FinalIndex)
                    : new TextRange(
                        originalWords[originalWords.Count - originalCount]
                            .InitialIndex,
                        originalWords[^1].FinalIndex);
                var userRange = fromStart
                    ? new TextRange(
                        userWords[0].InitialIndex,
                        userWords[userCount - 1].FinalIndex)
                    : new TextRange(
                        userWords[userWords.Count - userCount].InitialIndex,
                        userWords[^1].FinalIndex);

                if (!AreEquivalent(
                        TextRangeNavigator.Slice(originalText, originalRange),
                        TextRangeNavigator.Slice(userText, userRange)))
                {
                    continue;
                }

                var score = originalCount + userCount;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                match = new EdgeMatch(originalCount, userCount);
            }
        }

        return bestScore > 0;
    }

    private bool TrySplit(
        string originalText,
        string userText,
        ComparisonRange range,
        out IReadOnlyList<ComparisonRange> split)
    {
        split = [];
        if (!TryFindSplitAnchor(
                originalText,
                userText,
                range,
                out var anchor))
        {
            return false;
        }

        var left = new ComparisonRange(
            range.SourceComparisonIndex,
            new TextRange(
                range.OriginalTextRange.InitialIndex,
                SkipIgnorableBackward(
                    originalText,
                    anchor.OriginalRange.InitialIndex - 1,
                    range.OriginalTextRange.InitialIndex)),
            new TextRange(
                range.UserTextRange.InitialIndex,
                SkipIgnorableBackward(
                    userText,
                    anchor.UserRange.InitialIndex - 1,
                    range.UserTextRange.InitialIndex)));

        var right = new ComparisonRange(
            range.SourceComparisonIndex,
            new TextRange(
                SkipIgnorableForward(
                    originalText,
                    anchor.OriginalRange.FinalIndex + 1,
                    range.OriginalTextRange.FinalIndex),
                range.OriginalTextRange.FinalIndex),
            new TextRange(
                SkipIgnorableForward(
                    userText,
                    anchor.UserRange.FinalIndex + 1,
                    range.UserTextRange.FinalIndex),
                range.UserTextRange.FinalIndex));

        if (!IsUsefulSplit(originalText, userText, left)
            || !IsUsefulSplit(originalText, userText, right))
        {
            return false;
        }

        var output = new List<ComparisonRange>();
        output.AddRange(RefineRange(originalText, userText, left));
        output.AddRange(RefineRange(originalText, userText, right));
        split = output;
        return output.Count > 1;
    }

    private bool TryFindSplitAnchor(
        string originalText,
        string userText,
        ComparisonRange range,
        out SplitAnchor anchor)
    {
        anchor = new SplitAnchor(new TextRange(0, 0), new TextRange(0, 0));
        var originalWords = TextRangeNavigator.GetWords(
            originalText,
            range.OriginalTextRange);
        var userWords = TextRangeNavigator.GetWords(
            userText,
            range.UserTextRange);

        if ((long)originalWords.Count * userWords.Count
            > MaxSplitCandidateWordPairs)
        {
            return false;
        }

        SplitAnchor? best = null;
        var bestScore = 0;
        var bestCount = 0;
        for (var originalStart = 1;
             originalStart < originalWords.Count - 1;
             originalStart++)
        {
            for (var userStart = 1;
                 userStart < userWords.Count - 1;
                 userStart++)
            {
                var maxOriginalLength = Math.Min(
                    MaxAnchorWords,
                    originalWords.Count - originalStart - 1);
                var maxUserLength = Math.Min(
                    MaxAnchorWords,
                    userWords.Count - userStart - 1);

                for (var originalLength = 1;
                     originalLength <= maxOriginalLength;
                     originalLength++)
                {
                    for (var userLength = 1;
                         userLength <= maxUserLength;
                         userLength++)
                    {
                        var originalRange = new TextRange(
                            originalWords[originalStart].InitialIndex,
                            originalWords[originalStart + originalLength - 1]
                                .FinalIndex);
                        var userRange = new TextRange(
                            userWords[userStart].InitialIndex,
                            userWords[userStart + userLength - 1].FinalIndex);
                        var anchorWords = originalWords
                            .Skip(originalStart)
                            .Take(originalLength)
                            .Select(word => TextRangeNavigator.Slice(
                                originalText,
                                word))
                            .ToList();

                        if (!AreEquivalent(
                                TextRangeNavigator.Slice(
                                    originalText,
                                    originalRange),
                                TextRangeNavigator.Slice(userText, userRange))
                            || !HasCleanAnchorBoundaries(
                                originalText,
                                range.OriginalTextRange,
                                originalRange)
                            || !HasCleanAnchorBoundaries(
                                userText,
                                range.UserTextRange,
                                userRange)
                            || !IsReliableAnchor(
                                anchorWords,
                                originalWords.Count,
                                userWords.Count,
                                originalStart,
                                userStart))
                        {
                            continue;
                        }

                        var score = ((originalLength + userLength) * 50)
                            - Math.Abs(originalStart - userStart);
                        if (score > bestScore)
                        {
                            best = new SplitAnchor(originalRange, userRange);
                            bestScore = score;
                            bestCount = 1;
                        }
                        else if (score == bestScore)
                        {
                            bestCount++;
                        }
                    }
                }
            }
        }

        if (bestScore == 0 || bestCount != 1)
        {
            return false;
        }

        anchor = best!;
        return true;
    }

    private static bool IsReliableAnchor(
        IReadOnlyList<string> words,
        int originalWordCount,
        int userWordCount,
        int originalStartIndex,
        int userStartIndex)
    {
        if (words.Any(word =>
                !TextComparisonAlignmentPolicy.IsFunctionWord(word)))
        {
            return true;
        }

        return words.Count == 1
            && originalWordCount <= MaxFunctionAnchorPhraseWords
            && userWordCount <= MaxFunctionAnchorPhraseWords
            && originalStartIndex == userStartIndex;
    }

    private bool IsUsefulSplit(
        string originalText,
        string userText,
        ComparisonRange range)
    {
        if (!HasWords(originalText, range.OriginalTextRange)
            || !HasWords(userText, range.UserTextRange))
        {
            return false;
        }

        return !AreEquivalent(
            TextRangeNavigator.Slice(originalText, range.OriginalTextRange),
            TextRangeNavigator.Slice(userText, range.UserTextRange));
    }

    private static bool HasCleanAnchorBoundaries(
        string text,
        TextRange source,
        TextRange anchor)
    {
        if (anchor.InitialIndex > source.InitialIndex
            && IsCompoundJoiner(text[anchor.InitialIndex - 1]))
        {
            return false;
        }

        if (anchor.FinalIndex < source.FinalIndex
            && IsCompoundJoiner(text[anchor.FinalIndex + 1]))
        {
            return false;
        }

        return true;
    }

    private static bool IsCompoundJoiner(char value) =>
        value is '-' or '\'' or '\u2018' or '\u2019';

    private bool AreEquivalent(string originalText, string userText) =>
        string.Equals(
            originalText.Trim(),
            userText.Trim(),
            StringComparison.OrdinalIgnoreCase)
        || _equivalenceService.AreEquivalent(originalText, userText);

    private static bool HasWords(string text, TextRange range) =>
        range.InitialIndex <= range.FinalIndex
        && TextRangeNavigator.GetWords(text, range).Count > 0;

    private static int SkipIgnorableForward(
        string text,
        int start,
        int max)
    {
        var index = start;
        while (index <= max
               && TextRangeNavigator.IsIgnorableCharacter(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int SkipIgnorableBackward(
        string text,
        int start,
        int min)
    {
        var index = start;
        while (index >= min
               && TextRangeNavigator.IsIgnorableCharacter(text[index]))
        {
            index--;
        }

        return index;
    }

    private static TextComparison? ToComparison(
        string originalText,
        string userText,
        ComparisonRange range,
        bool isDeterministicallyRefined)
    {
        if (!TextRangeNavigator.TrySlice(
                originalText,
                range.OriginalTextRange,
                out var originalSnippet)
            || !TextRangeNavigator.TrySlice(
                userText,
                range.UserTextRange,
                out var userSnippet))
        {
            return null;
        }

        return new TextComparison(
            range.OriginalTextRange,
            originalSnippet,
            range.UserTextRange,
            userSnippet,
            range.SourceComparisonIndex,
            isDeterministicallyRefined);
    }

    private static CorrectionTraceEntry CreateTrace(
        TextComparison comparison,
        string action,
        string reasonCode,
        IReadOnlyList<ComparisonSnapshot> output) =>
        new(
            comparison.SourceComparisonIndex,
            ToSnapshot(comparison),
            new CorrectionStageTrace(action, reasonCode, output));

    private static ComparisonSnapshot ToSnapshot(TextComparison comparison) =>
        new(
            comparison.OriginalTextRange,
            comparison.OriginalText ?? string.Empty,
            comparison.UserTextRange,
            comparison.UserText ?? string.Empty);

    private static bool IsSameRange(
        ComparisonRange left,
        ComparisonRange right) =>
        left.OriginalTextRange == right.OriginalTextRange
        && left.UserTextRange == right.UserTextRange;

    private sealed record EdgeMatch(
        int OriginalWordCount,
        int UserWordCount);

    private sealed record SplitAnchor(
        TextRange OriginalRange,
        TextRange UserRange);

    private sealed record ComparisonRange(
        int SourceComparisonIndex,
        TextRange OriginalTextRange,
        TextRange UserTextRange);
}

public sealed record DeterministicTextComparisonRefinementResult(
    IReadOnlyList<TextComparison> Comparisons,
    IReadOnlyDictionary<int, CorrectionTraceEntry> Trace,
    int RemovedComparisonCount,
    bool HasChanges);
