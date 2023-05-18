public class TokenizeTextService {
    public List<string> TokenizeText(string text)
    {
        // Remove punctuation and convert to lowercase
        string[] punctuation = new string[] { ".", ",", "!", "?", ";", ":" };
        text = text.ToLower().Trim();
        foreach(var p in punctuation) text = text.Replace(p, "");

        // Split the text into tokens using space as a delimiter
        var tokens = text.Split(' ').ToList();

        tokens.RemoveAll(t => string.IsNullOrWhiteSpace(t));

        return tokens;
    }
}