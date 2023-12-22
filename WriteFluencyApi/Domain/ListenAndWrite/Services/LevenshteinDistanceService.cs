namespace WriteFluencyApi.ListenAndWrite.Domain;

public class LevenshteinDistanceService {
    public int ComputeDistance(string s1, string s2) {
        int len1 = s1.Length;
        int len2 = s2.Length;

        int[,] d = new int[len1 + 1, len2 + 1];

        for (int i = 0; i <= len1; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 0; j <= len2; j++)
        {
            d[0, j] = j;
        }

        for (int i = 1; i <= len1; i++)
        {
            for (int j = 1; j <= len2; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[len1, len2];
    }
}