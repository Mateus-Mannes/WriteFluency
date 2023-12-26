namespace WriteFluencyApi.ListenAndWrite.Domain;

public interface ITextComparisonService
{
    List<TextComparisonDto> CompareTexts(string originalText, string userText);
}
