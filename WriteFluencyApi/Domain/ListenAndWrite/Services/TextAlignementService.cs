﻿using WriteFluencyApi.ListenAndWrite;

namespace WriteFluencyApi.ListenAndWrite.Domain;

public class TextAlignementService
{
    private readonly NeedlemanWunschAlignmentService _needlemanWunschAlignmentService;
    private readonly TokenizeTextService _tokenizeTextService;
    private readonly TokenAlignmentService _tokenAlignmentService;

    public TextAlignementService(
        NeedlemanWunschAlignmentService needlemanWunschAlignmentService, 
        TokenizeTextService tokenizeTextService, 
        TokenAlignmentService tokenAlignmentService)
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
