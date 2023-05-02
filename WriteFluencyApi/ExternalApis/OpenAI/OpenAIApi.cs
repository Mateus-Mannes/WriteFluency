using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using WriteFluencyApi.Dtos.ListenAndWrite;
using WriteFluencyApi.Services.ListenAndWrite;

namespace WriteFluencyApi.ExternalApis.OpenAI;

public class OpenAIApi : ITextGenerator
{
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly ExternalApisConfig _externalApisConfig;
    
    public OpenAIApi(IOptions<ExternalApisConfig> externalApisConfig)
    {
        _externalApisConfig = externalApisConfig.Value;
        _httpClient.BaseAddress = new Uri(_externalApisConfig.OpenAI.Url);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string GenerateText(GenerateTextDto generateTextDto)
    {
        return "OpenAI";
    }
}
