public class NeedlemanWunschAlignmentService {

    private readonly LevenshteinDistanceService _levenshteinDistanceService;

    public NeedlemanWunschAlignmentService(LevenshteinDistanceService levenshteinDistanceService)
        => _levenshteinDistanceService = levenshteinDistanceService;
    

    public (int[,], int[,]) NeedlemanWunschAlignment(List<string> seq1, List<string> seq2)
    {
        int matchScore = 2;
        int gapScore = -2;

        int[,] scoreMatrix = new int[seq1.Count + 1, seq2.Count + 1];
        int[,] tracebackMatrix = new int[seq1.Count + 1, seq2.Count + 1];

        for (int i = 1; i <= seq1.Count; i++) scoreMatrix[i, 0] = gapScore * i;
        for (int j = 1; j <= seq2.Count; j++) scoreMatrix[0, j] = gapScore * j;

        for (int i = 1; i <= seq1.Count; i++)
        {
            for (int j = 1; j <= seq2.Count; j++)
            {
                int mismatchScore = _levenshteinDistanceService.ComputeDistance(seq1[i - 1], seq2[j - 1]) * -1;
                
                int scoreDiag = scoreMatrix[i - 1, j - 1] + (seq1[i - 1] == seq2[j - 1] ? matchScore : mismatchScore);
                int scoreLeft = scoreMatrix[i - 1, j] + gapScore;
                int scoreUp = scoreMatrix[i, j - 1] + gapScore;

                scoreMatrix[i, j] = Math.Max(scoreDiag, Math.Max(scoreLeft, scoreUp));

                if (scoreMatrix[i, j] == scoreDiag) tracebackMatrix[i, j] = 1;
                else if (scoreMatrix[i, j] == scoreLeft) tracebackMatrix[i, j] = 2;
                else tracebackMatrix[i, j] = 3;
            }
        }
        return (scoreMatrix, tracebackMatrix);
    }

    public List<AlignedTokensDto> GetAlignedTokens(List<TextTokenDto> seq1, List<TextTokenDto> seq2, int[,] tracebackMatrix)
    {
        var alignedTokens = new List<AlignedTokensDto>();

        int i = seq1.Count;
        int j = seq2.Count;

        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0)
            {
                if (tracebackMatrix[i, j] == 1)
                {
                    alignedTokens.Insert(0, new AlignedTokensDto(seq1[i - 1], seq2[j - 1]));
                    i--;
                    j--;
                }
                else if (tracebackMatrix[i, j] == 2)
                {
                    alignedTokens.Insert(0, new AlignedTokensDto(seq1[i - 1], null));
                    i--;
                }
                else
                {
                    alignedTokens.Insert(0, new AlignedTokensDto(null, seq2[j - 1]));
                    j--;
                }
            }
            else if (i > 0)
            {
                alignedTokens.Insert(0, new AlignedTokensDto(seq1[i - 1], null));
                i--;
            }
            else
            {
                alignedTokens.Insert(0, new AlignedTokensDto(null, seq2[j - 1]));
                j--;
            }
        }

        return alignedTokens;
    }
}