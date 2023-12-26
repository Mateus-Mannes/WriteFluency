namespace WriteFluencyApi.ListenAndWrite.Domain;

public interface ITokenizeTextService
{
    List<TextTokenDto> TokenizeText(string text);
}
