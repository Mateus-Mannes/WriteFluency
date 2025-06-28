namespace WriteFluency.TextComparisons;

public class TokenAlignmentService
{
    public List<AlignedTokens> GetAlignedTokens(List<TextToken> seq1, List<TextToken> seq2, int[,] tracebackMatrix)
    {
        var alignedTokens = new List<AlignedTokens>();

        int i = seq1.Count;
        int j = seq2.Count;

        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0)
            {
                if (tracebackMatrix[i, j] == 1)
                {
                    alignedTokens.Insert(0, new AlignedTokens(seq1[i - 1], seq2[j - 1]));
                    i--;
                    j--;
                }
                else if (tracebackMatrix[i, j] == 2)
                {
                    alignedTokens.Insert(0, new AlignedTokens(seq1[i - 1], null));
                    i--;
                }
                else
                {
                    alignedTokens.Insert(0, new AlignedTokens(null, seq2[j - 1]));
                    j--;
                }
            }
            else if (i > 0)
            {
                alignedTokens.Insert(0, new AlignedTokens(seq1[i - 1], null));
                i--;
            }
            else
            {
                alignedTokens.Insert(0, new AlignedTokens(null, seq2[j - 1]));
                j--;
            }
        }

        return alignedTokens;
    }
}
