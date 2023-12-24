using static System.Net.Mime.MediaTypeNames;

namespace WriteFluencyApi.ListenAndWrite;

public record TextRangeDto
{
    public int InitialIndex { get; set; }
    public int FinalIndex { get; set; }

    public TextRangeDto(int initialIndex, int finalIndex)
    {
        InitialIndex = initialIndex;
        FinalIndex = finalIndex;
    }

    public TextRangeDto(TextTokenDto? previousToken, TextTokenDto? nextToken, string originalText) : this(
        previousToken?.TextRange.InitialIndex ?? 0,
        nextToken?.TextRange.FinalIndex ?? originalText.Length - 1)
    {
    }
}