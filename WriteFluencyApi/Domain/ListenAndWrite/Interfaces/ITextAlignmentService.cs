namespace WriteFluencyApi.ListenAndWrite.Domain;

public interface ITextAlignmentService
{
    List<AlignedTokensDto> AlignTexts(string originalText, string userText);
}
