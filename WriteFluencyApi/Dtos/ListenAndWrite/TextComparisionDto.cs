public record TextComparisionDto
{
    public int TokenAlignmentIndex { get; set; }
    public TextRangeDto OriginalTextRange { get; set; }
    public TextRangeDto UserTextRange { get; set; }

    public TextComparisionDto(TextRangeDto originalTextRange,  TextRangeDto userTextRange, int tokenAlignmentIndex)
    {
        OriginalTextRange = originalTextRange;
        UserTextRange = userTextRange;
        TokenAlignmentIndex = tokenAlignmentIndex;
    }
}