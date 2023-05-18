public class TextComparisionService {

    private const double SimilartyThresholdPercentage = 0.70; 
    private readonly LevenshteinDistanceService _levenshteinDistanceService;
    private readonly NeedlemanWunschAlignmentService _needlemanWunschAlignmentService;
    private readonly TokenizeTextService _tokenizeTextService;

    public TextComparisionService(LevenshteinDistanceService levenshteinDistanceService, 
        NeedlemanWunschAlignmentService needlemanWunschAlignmentService, 
        TokenizeTextService tokenizeTextService)
    {
        _levenshteinDistanceService = levenshteinDistanceService;
        _needlemanWunschAlignmentService = needlemanWunschAlignmentService;
        _tokenizeTextService = tokenizeTextService;
    }

    public List<TextComparisionDto> CompareTexts(string originalText, string userText) {

        if(!IsMinimalSimilar(originalText, userText)) 
            return new List<TextComparisionDto>() { new TextComparisionDto((0, userText.Length - 1), originalText) };
        
        List<string> originalTokens = _tokenizeTextService.TokenizeText(originalText);
        List<string> userTokens = _tokenizeTextService.TokenizeText(userText);

        (int[,] scoreMatrix, int[,] tracebackMatrix) = _needlemanWunschAlignmentService.NeedlemanWunschAlignment(originalTokens, userTokens);
        List<Tuple<string, string>> alignedTokens = _needlemanWunschAlignmentService.GetAlignedTokens(originalTokens, userTokens, tracebackMatrix);

        List<TextComparisionDto> textComparisions = new List<TextComparisionDto>();

        int userInputIndex = 0;
        int alignedTokenIndex = 0;
        var arr = userText.ToArray();
        int jumpedchars = 0;
        while (userInputIndex < userText.Length && alignedTokenIndex < alignedTokens.Count)
        {
            char currentChar = userText[userInputIndex];

            if (char.IsWhiteSpace(currentChar) || char.IsPunctuation(currentChar))
            {
                jumpedchars++;
                userInputIndex++;
                continue;
            }

            if (alignedTokens[alignedTokenIndex].Item1 != alignedTokens[alignedTokenIndex].Item2)
            {
                int startIndex = userInputIndex;
                IncrementIndexToNextBreak(ref userInputIndex, userText);
                int endIndex = userInputIndex - 1;

                // Extend the previous highlighted area
                if(alignedTokens[alignedTokenIndex].Item1 == "-"){
                    // increment endIndex until the next word
                    int startOfWord = 0;
                    while (endIndex < userInput.Length)
                    {
                        endIndex++;
                        if(char.IsWhiteSpace(userInput[endIndex]) || char.IsPunctuation(userInput[endIndex])){
                            startOfWord++;
                            if(startOfWord == 2){
                                break;
                            }
                            while(char.IsWhiteSpace(userInput[endIndex]) || char.IsPunctuation(userInput[endIndex])) endIndex++;
                        }
                    }
                    
                    userInputIndex = endIndex;
                    endIndex--;
                }

                if (highlightedAreas.Count > 0 && startIndex == highlightedAreas.Last().Item2 + jumpedchars + 1)
                {
                    highlightedAreas[highlightedAreas.Count - 1] = Tuple.Create(highlightedAreas.Last().Item1, endIndex);
                }
                else
                {
                    // if it is a missing or extra word, mark the surrounding words
                    int startOfWord = 0;
                    if(alignedTokens[alignedTokenIndex].Item2 == "-"){
                        var x = 1;
                        while (startIndex >= 0)
                        {
                            startIndex--;
                            if(char.IsWhiteSpace(userInput[startIndex]) || char.IsPunctuation(userInput[startIndex])){
                                startOfWord++;
                                if(startOfWord == 2){
                                    startIndex++;
                                    break;
                                }
                                while(char.IsWhiteSpace(userInput[startIndex]) || char.IsPunctuation(userInput[startIndex])) startIndex--;
                            }
                        }
                    }
                    
                    if(alignedTokens[alignedTokenIndex].Item1 == "-"){
                        // increment startIndex until the previous word
                        startOfWord = 0;
                        while (startIndex >= 0)
                        {
                            startIndex--;
                            if(char.IsWhiteSpace(userInput[startIndex]) || char.IsPunctuation(userInput[startIndex])){
                                startOfWord++;
                                if(startOfWord == 2){
                                    startIndex++;
                                    break;
                                }
                                while(char.IsWhiteSpace(userInput[startIndex]) || char.IsPunctuation(userInput[startIndex])) startIndex--;
                            }
                        }
                    }

                    // Add a new highlighted area
                    highlightedAreas.Add(Tuple.Create(startIndex, endIndex));
                }
            }
            else
                IncrementIndexToNextBreak(ref userInputIndex, userText);

            // Get extra right word if it is a missing word
            if(alignedTokens[alignedTokenIndex].Item2 == "-" || alignedTokens[alignedTokenIndex].Item1 == "-")
                alignedTokenIndex++;

            alignedTokenIndex++;
            jumpedchars = 0;
        }

    }

    private bool IsMinimalSimilar(string originalText, string userText) {
        int distance = _levenshteinDistanceService.ComputeDistance(originalText, userText);
        double similarity =  1 - (double)distance / Math.Max(originalText.Length, userText.Length);
        return similarity >= SimilartyThresholdPercentage;
    }

    private void IncrementIndexToNextBreak(ref int index, string text) {
        while (IsTextPosition(index, text)) index++;
    }

    private bool IsTextPosition(int index, string text){
        return index >= 0 && index < text.Length
            && !char.IsWhiteSpace(text[index]) && !char.IsPunctuation(text[index]);
    }
}