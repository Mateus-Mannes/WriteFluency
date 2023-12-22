namespace WriteFluencyApi.ListenAndWrite.Domain;

public class TextComparisionService {

    private const double SimilartyThresholdPercentage = 0.60; 
    private readonly LevenshteinDistanceService _levenshteinDistanceService;
    private readonly NeedlemanWunschAlignmentService _needlemanWunschAlignmentService;
    private readonly TokenizeTextService _tokenizeTextService;
    private readonly TokenAlignmentService _tokenAlignmentService;

    public TextComparisionService(LevenshteinDistanceService levenshteinDistanceService, 
        NeedlemanWunschAlignmentService needlemanWunschAlignmentService, 
        TokenizeTextService tokenizeTextService,
        TokenAlignmentService tokenAlignmentService)
    {
        _levenshteinDistanceService = levenshteinDistanceService;
        _needlemanWunschAlignmentService = needlemanWunschAlignmentService;
        _tokenizeTextService = tokenizeTextService;
        _tokenAlignmentService = tokenAlignmentService;
    }

    public List<TextComparisionDto> CompareTexts(string originalText, string userText) {

        // what is the best way to oraganize this validation keeping single responsability principle?

        if(!IsMinimalSimilar(originalText, userText)) 
            return new List<TextComparisionDto>() { 
                new TextComparisionDto(
                    new TextRangeDto(0, originalText.Length - 1), originalText, 
                    new TextRangeDto(0, userText.Length - 1), userText) 
                };
        
        var originalTokens = _tokenizeTextService.TokenizeText(originalText);
        var userTokens = _tokenizeTextService.TokenizeText(userText);

        (int[,] scoreMatrix, int[,] tracebackMatrix) = 
            _needlemanWunschAlignmentService.NeedlemanWunschAlignment(
                originalTokens.Select(x => x.Token).ToList(), 
                userTokens.Select(x => x.Token).ToList());

        var alignedTokens = _tokenAlignmentService.GetAlignedTokens(originalTokens, userTokens, tracebackMatrix);

        List<TextComparisionDto> textComparisions = new List<TextComparisionDto>();

        for(int i = 0; i < alignedTokens.Count; i++) 
            CompareTokens(ref i, alignedTokens, textComparisions, originalText, userText);

        GetSubStrings(textComparisions, originalText, userText);

        return textComparisions;
    }

    private bool IsMinimalSimilar(string originalText, string userText) {
        int distance = _levenshteinDistanceService.ComputeDistance(originalText, userText);
        double similarity =  1 - (double)distance / Math.Max(originalText.Length, userText.Length);
        return similarity >= SimilartyThresholdPercentage;
    }

    private void CompareTokens(
        ref int tokenAlignmentIndex,
        List<AlignedTokensDto> alignedTokens, 
        List<TextComparisionDto> textComparisions,
        string originalText,
        string userText)
    {
        var token = alignedTokens[tokenAlignmentIndex];
        if(token.OriginalToken == null || token.UserToken == null)
        {
            var privious = GetPriviousAlignement(tokenAlignmentIndex, alignedTokens);
            var next = GetNextAlignement(tokenAlignmentIndex, alignedTokens);

            if(next != null) tokenAlignmentIndex++;
            AddComparision(
                new TextRangeDto(privious?.OriginalToken?.TextRange.InitialIndex ?? 0, 
                    next?.OriginalToken?.TextRange.FinalIndex ?? originalText.Length - 1),
                new TextRangeDto(privious?.UserToken?.TextRange.InitialIndex ?? 0, 
                    next?.UserToken?.TextRange.FinalIndex ?? userText.Length - 1),
                userText,
                textComparisions
            );
        }
        else if(token.OriginalToken.Token != token.UserToken.Token)
            AddComparision(
                token.OriginalToken!.TextRange, 
                token.UserToken!.TextRange,
                userText, 
                textComparisions);
    }

    private AlignedTokensDto? GetPriviousAlignement(int tokenAlignmentIndex, List<AlignedTokensDto> alignedTokens)
    {
        TextTokenDto? userToken = null;
        TextTokenDto? originalToken = null;
        for(int i = 1; tokenAlignmentIndex - i >= 0; i++)
        {
            var privious = alignedTokens.ElementAtOrDefault(tokenAlignmentIndex - i);
            if(userToken == null) userToken = privious?.UserToken;
            if(originalToken == null) originalToken = privious?.OriginalToken;
            if(userToken != null && originalToken != null) break;
        }
        return new AlignedTokensDto(originalToken, userToken);
    }

    private AlignedTokensDto? GetNextAlignement(int tokenAlignmentIndex, List<AlignedTokensDto> alignedTokens)
    {
        TextTokenDto? userToken = null;
        TextTokenDto? originalToken = null;
        for(int i = 1; tokenAlignmentIndex + i < alignedTokens.Count; i++)
        {
            var next = alignedTokens.ElementAtOrDefault(tokenAlignmentIndex + i);
            if(userToken == null) userToken = next?.UserToken;
            if(originalToken == null) originalToken = next?.OriginalToken;
            if(userToken != null && originalToken != null) break;
        }
        return new AlignedTokensDto(originalToken, userToken);
    }

    private void AddComparision(
        TextRangeDto originalTextRange,
        TextRangeDto userTextRange,
        string userText,
        List<TextComparisionDto> textComparisions)
    {
        var lastComparision = textComparisions.LastOrDefault();
        if(lastComparision != null 
            && (IsSequential(userText, lastComparision, userTextRange)
                || lastComparision.UserTextRange.FinalIndex >= userTextRange.InitialIndex)) 
        {
            lastComparision.UserTextRange = 
                new TextRangeDto(lastComparision.UserTextRange.InitialIndex, userTextRange.FinalIndex);
            lastComparision.OriginalTextRange =
                new TextRangeDto(lastComparision.OriginalTextRange.InitialIndex, originalTextRange.FinalIndex);
        }
        else
            textComparisions.Add(new TextComparisionDto(originalTextRange, userTextRange));
    }

    private bool IsSequential(string userText, TextComparisionDto lastTextComparision, TextRangeDto userTextRange)
    {
        int index = userTextRange.InitialIndex - 1;
        while(index >= 0 && (char.IsWhiteSpace(userText[index]) || char.IsPunctuation(userText[index])))
            index--;
        return index == lastTextComparision.UserTextRange.FinalIndex;
    }

    private void GetSubStrings(List<TextComparisionDto> textComparisions, string originalText, string userText)
    {
        foreach(var comparision in textComparisions)
        {
            comparision.OriginalText =  originalText.Substring(comparision.OriginalTextRange.InitialIndex, 
                comparision.OriginalTextRange.FinalIndex - comparision.OriginalTextRange.InitialIndex + 1);
            comparision.UserText = userText.Substring(comparision.UserTextRange.InitialIndex, 
                comparision.UserTextRange.FinalIndex - comparision.UserTextRange.InitialIndex + 1);
        }
    }

}