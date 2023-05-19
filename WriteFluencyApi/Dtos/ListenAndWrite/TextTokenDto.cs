public record TextTokenDto(
    string Token,
    (int, int) TextRangeIndex
);