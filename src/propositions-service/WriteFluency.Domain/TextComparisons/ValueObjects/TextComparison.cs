namespace WriteFluency.TextComparisons;

public class TextComparison
{
    public TextRange OriginalTextRange { get; set; }
    public string? OriginalText { get; set; }
    public TextRange UserTextRange { get; set; }
    public string? UserText { get; set; }

    public TextComparison(TextRange originalTextRange, string originalText, TextRange userTextRange, string userText)
    {
        OriginalTextRange = originalTextRange;
        OriginalText = originalText;
        UserTextRange = userTextRange;
        UserText = userText;
    }

    public TextComparison(TextRange originalTextRange, TextRange userTextRange)
    {
        OriginalTextRange = originalTextRange;
        UserTextRange = userTextRange;
    }

    /// <summary>
    /// Generates a full Comparison between the original text and the user text.
    /// </summary>
    public TextComparison(string originalText, string userText)
    : this(
        new TextRange(0, originalText.Length - 1),
        originalText,
        new TextRange(0, userText.Length - 1),
        userText)
    {
    }

    public void IncrementComparison(TextRange originalTextRange, TextRange userTextRange)
    {
        UserTextRange = UserTextRange with { FinalIndex = userTextRange.FinalIndex };
        OriginalTextRange = OriginalTextRange with { FinalIndex = originalTextRange.FinalIndex };
    }

}