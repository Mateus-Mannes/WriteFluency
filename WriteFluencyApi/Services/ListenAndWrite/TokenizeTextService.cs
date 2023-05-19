public class TokenizeTextService {
    public List<TextTokenDto> TokenizeText(string text)
    {
        // Remove punctuation and convert to lowercase
        string[] punctuation = new string[] { ".", ",", "!", "?", ";", ":" };
        text = text.ToLower().Trim();
        foreach(var p in punctuation) text = text.Replace(p, "");

        // Split the text into tokens using space as a delimiter
        var words = text.Split(' ').ToList();

        words.RemoveAll(t => string.IsNullOrWhiteSpace(t));

        var tokens = new List<TextTokenDto>();
        foreach(var word in words) 
        {
            int startIndex = text.IndexOf(word);
            int endIndex = startIndex + word.Length - 1;
            tokens.Add(new TextTokenDto(word, (startIndex, endIndex)));
            text.Remove(startIndex, word.Length);
        }

        return tokens;
    }
}