public record TextComparisionDto
{
    public TextRangeDto OriginalTextRange { get; set; }
    public string OriginalText { get; set; } = null!;
    public TextRangeDto UserTextRange { get; set; }
    public string userText { get; set; } = null!;

    public TextComparisionDto(TextRangeDto originalTextRange,  TextRangeDto userTextRange)
    {
        OriginalTextRange = originalTextRange;
        UserTextRange = userTextRange;
    }
}