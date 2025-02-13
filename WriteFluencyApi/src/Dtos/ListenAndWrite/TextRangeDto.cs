namespace WriteFluencyApi.ListenAndWrite;

public record TextRangeDto(int InitialIndex, int FinalIndex)
{
    /// <summary>
    /// Generates a new text range englobing the text from the previous token to the next token.
    /// </summary>
    public TextRangeDto(
        TextTokenDto? previousToken,
        TextTokenDto? nextToken,
        string originalText) : this(
        previousToken?.TextRange.InitialIndex ?? 0,
        nextToken?.TextRange.FinalIndex ?? originalText.Length - 1)
    {
    }
}