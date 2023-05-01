using WriteFluencyApi.Dtos.ListenAndWrite;

namespace WriteFluencyApi.Services.ListenAndWrite;

public interface ITextGenerator
{
    public string GenerateText(GenerateTextDto generateTextDto);    
}
