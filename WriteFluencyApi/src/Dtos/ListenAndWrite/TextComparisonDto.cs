namespace WriteFluencyApi.ListenAndWrite;

public class TextComparisonDto
{
    public TextRangeDto OriginalTextRange { get; set; }
    public string? OriginalText { get; set; }
    public TextRangeDto UserTextRange { get; set; }
    public string? UserText { get; set; }

    public TextComparisonDto(TextRangeDto originalTextRange, string originalText, TextRangeDto userTextRange, string userText)
    {
        OriginalTextRange = originalTextRange;
        OriginalText = originalText;
        UserTextRange = userTextRange;
        UserText = userText;
    }

    public TextComparisonDto(TextRangeDto originalTextRange, TextRangeDto userTextRange)
    {
        OriginalTextRange = originalTextRange;
        UserTextRange = userTextRange;
    }

    /// <summary>
    /// Generates a full Comparison between the original text and the user text.
    /// </summary>
    public TextComparisonDto(string originalText, string userText)
    : this(
        new TextRangeDto(0, originalText.Length - 1),
        originalText,
        new TextRangeDto(0, userText.Length - 1),
        userText)
    {
    }

    public void IncrementComparison(TextRangeDto originalTextRange, TextRangeDto userTextRange)
    {
        UserTextRange = UserTextRange with { FinalIndex = userTextRange.FinalIndex };
        OriginalTextRange = OriginalTextRange with { FinalIndex = originalTextRange.FinalIndex };
    }

}