namespace WriteFluencyApi.ListenAndWrite.Domain;

public interface ITokenComparisonService
{
    void AddTokenComparison(
        ref int tokenAlignmentIndex,
        List<AlignedTokensDto> alignedTokens,
        List<TextComparisonDto> textComparisons,
        string originalText,
        string userText);
}
