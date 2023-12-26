using WriteFluencyApi.ListenAndWrite;

namespace WriteFluencyApi.ListenAndWrite.Domain;

public class TextAlignmentService : ITextAlignmentService
{
    private readonly INeedlemanWunschAlignmentService _needlemanWunschAlignmentService;
    private readonly ITokenizeTextService _tokenizeTextService;
    private readonly ITokenAlignmentService _tokenAlignmentService;

    public TextAlignmentService(
        INeedlemanWunschAlignmentService needlemanWunschAlignmentService, 
        ITokenizeTextService tokenizeTextService, 
        ITokenAlignmentService tokenAlignmentService)
    {
        _needlemanWunschAlignmentService = needlemanWunschAlignmentService;
        _tokenizeTextService = tokenizeTextService;
        _tokenAlignmentService = tokenAlignmentService;
    }

    public List<AlignedTokensDto> AlignTexts(string originalText, string userText)
    {
        var originalTokens = _tokenizeTextService.TokenizeText(originalText);
        var userTokens = _tokenizeTextService.TokenizeText(userText);

        (int[,] scoreMatrix, int[,] tracebackMatrix) =
            _needlemanWunschAlignmentService.NeedlemanWunschAlignment(
                originalTokens.Select(x => x.Token).ToList(),
        userTokens.Select(x => x.Token).ToList());

        var alignedTokens = _tokenAlignmentService.GetAlignedTokens(originalTokens, userTokens, tracebackMatrix);

        return alignedTokens;
    }
}
