namespace WriteFluency.TextComparisons;

public class TokenizeTextService
{
    public List<TextToken> TokenizeText(string text)
    {
        text = text.ToLower();
        string originalText = text;

        string[] punctuation = new string[] { ". ", ", ", "! ", "? ", "; ", ": ", "\"", "_", "+", "=", "/", "|", "\\", "(", ")", "[", "]", "{", "}" };
        foreach (var p in punctuation) text = text.Replace(p, " ");
        if (text.EndsWith(".") || text.EndsWith("?") || text.EndsWith("!"))
            text = text[..^1];

        var words = text.Split(' ').ToList();
        words.RemoveAll(t => string.IsNullOrWhiteSpace(t));

        var tokens = new List<TextToken>();
        int endIndex = 0;
        foreach (var word in words)
        {
            int startIndex = originalText.IndexOf(word, endIndex);
            if (startIndex < 0) continue;
            endIndex = startIndex + word.Length - 1;
            tokens.Add(new TextToken(word, new TextRange(startIndex, endIndex)));
        }

        return tokens;
    }
}