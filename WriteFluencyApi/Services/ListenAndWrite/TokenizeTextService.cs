public class TokenizeTextService {
    public List<TextTokenDto> TokenizeText(string text)
    {
        text = text.ToLower();
        string originalText = text;

        string[] punctuation = new string[] { ".", ",", "!", "?", ";", ":", "\"", "_", "+", "=", "/", "|", "\\", "(", ")", "[", "]", "{", "}"};
        foreach(var p in punctuation) text = text.Replace(p, "");

        var words = text.Split(' ').ToList();
        words.RemoveAll(t => string.IsNullOrWhiteSpace(t));

        var tokens = new List<TextTokenDto>();
        int endIndex = 0;
        foreach(var word in words) 
        {
            int startIndex = originalText.IndexOf(word, endIndex);
            endIndex = startIndex + word.Length - 1;
            if(startIndex >= 0)
                tokens.Add(new TextTokenDto(word, new TextRangeDto(startIndex, endIndex)));
        }

        return tokens;
    }
}