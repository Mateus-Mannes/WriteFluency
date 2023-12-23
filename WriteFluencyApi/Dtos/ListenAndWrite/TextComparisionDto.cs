namespace WriteFluencyApi.ListenAndWrite;

public record TextComparisionDto
{
    public TextRangeDto OriginalTextRange { get; set; }
    public string OriginalText { get; set; } = null!;
    public TextRangeDto UserTextRange { get; set; }
    public string UserText { get; set; } = null!;

    public TextComparisionDto(TextRangeDto originalTextRange, string originalText,  TextRangeDto userTextRange, string userText)
    {
        OriginalTextRange = originalTextRange;
        OriginalText = originalText;
        UserTextRange = userTextRange;
        UserText = userText;
    }

    public TextComparisionDto(TextRangeDto originalTextRange,  TextRangeDto userTextRange)
    {
        OriginalTextRange = originalTextRange;
        UserTextRange = userTextRange;
    }

    /// <summary>
    /// Generates a full comparision between the original text and the user text.
    /// </summary>
    public TextComparisionDto(string originalText, string userText)
    : this(
        new TextRangeDto(0, originalText.Length - 1), 
        originalText,
        new TextRangeDto(0, userText.Length - 1), 
        userText)
    {
    }

}