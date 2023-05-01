using Microsoft.AspNetCore.Mvc;
using WriteFluencyApi.Dtos.ListenAndWrite;
using WriteFluencyApi.Services.ListenAndWrite;

namespace WriteFluencyApi.ExternalApis.OpenAI;

public class OpenAIApi : ITextGenerator
{
    private readonly HttpClient _httpClient = new HttpClient();
    
    public OpenAIApi([FromServices]IConfiguration configuration)
    {
        _httpClient.BaseAddress = new Uri("https://api.openai.com/");
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string GenerateText(GenerateTextDto generateTextDto)
    {
        
    }
}
