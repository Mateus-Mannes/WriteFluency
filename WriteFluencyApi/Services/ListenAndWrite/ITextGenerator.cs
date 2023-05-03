using WriteFluencyApi.Dtos.ListenAndWrite;

namespace WriteFluencyApi.Services.ListenAndWrite;

public interface ITextGenerator
{
    Task<string> GenerateTextAsync(GenerateTextDto generateTextDto);    
}
