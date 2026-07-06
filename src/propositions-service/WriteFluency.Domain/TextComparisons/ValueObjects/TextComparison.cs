namespace WriteFluency.TextComparisons;

public class TextComparison
{
    public int SourceComparisonIndex { get; set; }
    public bool IsDeterministicallyRefined { get; set; }
    public TextRange OriginalTextRange { get; set; }
    public string? OriginalText { get; set; }
    public TextRange UserTextRange { get; set; }
    public string? UserText { get; set; }
    public IReadOnlyList<string>? MistakePatternTags { get; set; }
    public string? MistakePatternPhrase { get; set; }

    public TextComparison(
        TextRange originalTextRange,
        string originalText,
        TextRange userTextRange,
        string userText,
        int sourceComparisonIndex = -1,
        bool isDeterministicallyRefined = false,
        IReadOnlyList<string>? mistakePatternTags = null,
        string? mistakePatternPhrase = null)
    {
        SourceComparisonIndex = sourceComparisonIndex;
        IsDeterministicallyRefined = isDeterministicallyRefined;
        OriginalTextRange = originalTextRange;
        OriginalText = originalText;
        UserTextRange = userTextRange;
        UserText = userText;
        MistakePatternTags = mistakePatternTags;
        MistakePatternPhrase = mistakePatternPhrase;
    }

    public TextComparison(TextRange originalTextRange, TextRange userTextRange)
    {
        OriginalTextRange = originalTextRange;
        UserTextRange = userTextRange;
    }

    /// <summary>
    /// Generates a full Comparison between the original text and the user text.
    /// </summary>
    public TextComparison(string originalText, string userText)
    : this(
        new TextRange(0, originalText.Length - 1),
        originalText,
        new TextRange(0, userText.Length - 1),
        userText)
    {
    }

    public void IncrementComparison(TextRange originalTextRange, TextRange userTextRange)
    {
        UserTextRange = UserTextRange with { FinalIndex = userTextRange.FinalIndex };
        OriginalTextRange = OriginalTextRange with { FinalIndex = originalTextRange.FinalIndex };
    }

}

public class TextComparisonResult
{
    public string OriginalText { get; set; }
    public string UserText { get; set; }
    public List<TextComparison> Comparisons { get; set; }
    public double AccuracyPercentage { get; set; }
    public string CorrectionMode { get; set; }
    public IReadOnlyList<CorrectionTraceEntry>? CorrectionTrace { get; set; }
    public string MistakePatternStatus { get; set; }
    public string? MistakePatternMessage { get; set; }

    public TextComparisonResult(
        string originalText,
        string userText,
        double accuracyPercentage,
        List<TextComparison> comparisons,
        string correctionMode = CorrectionModes.Static,
        IReadOnlyList<CorrectionTraceEntry>? correctionTrace = null,
        string mistakePatternStatus = MistakePatternStatuses.NotApplicable,
        string? mistakePatternMessage = null)
    {
        OriginalText = originalText;
        UserText = userText;
        AccuracyPercentage = accuracyPercentage;
        Comparisons = comparisons;
        CorrectionMode = correctionMode;
        CorrectionTrace = correctionTrace;
        MistakePatternStatus = mistakePatternStatus;
        MistakePatternMessage = mistakePatternMessage;
    }
}

public sealed record MistakePatternAnnotation(
    int ComparisonIndex,
    int SourceComparisonIndex,
    IReadOnlyList<string> Tags,
    string StudentPhrase);

public sealed record ComparisonSnapshot(
    TextRange OriginalTextRange,
    string OriginalText,
    TextRange UserTextRange,
    string UserText);

public sealed record CorrectionStageTrace(
    string Action,
    string ReasonCode,
    IReadOnlyList<ComparisonSnapshot> Output,
    string? ValidationStatus = null,
    IReadOnlyList<ComparisonSnapshot>? ProposedOutput = null,
    string? ValidationFailureReason = null);

public sealed record CorrectionTraceEntry(
    int SourceComparisonIndex,
    ComparisonSnapshot Initial,
    CorrectionStageTrace? Deterministic = null);
