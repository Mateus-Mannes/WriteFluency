using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using WriteFluencyApi.Dtos.ListenAndWrite;
using WriteFluencyApi.ExternalApis.OpenAI.Requests;
using WriteFluencyApi.ExternalApis.OpenAI.Responses;
using WriteFluencyApi.Services.ListenAndWrite;

namespace WriteFluencyApi.ExternalApis.OpenAI;

public class OpenAIApi : ITextGenerator
{
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly OpenAIConfig _openAIConfig;
    
    public OpenAIApi(IOptions<OpenAIConfig> openAIConfig)
    {
        _openAIConfig = openAIConfig.Value;
        _httpClient.BaseAddress = new Uri(_openAIConfig.BaseAddress);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _openAIConfig.Key);
    }

    public async Task<string> GenerateText(GenerateTextDto generateTextDto)
    {
        var request = new CompletionRequest
        {
            Model = "gpt-3.5-turbo",
            Prompt = "Say this is a test!",
            MaxTokens = 700,
            Temperature = 1.0m
        };

        var response = await _httpClient.PostAsJsonAsync(_openAIConfig.Routes.Completion, request);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<CompletionResponse>()
                ?? throw new HttpRequestException("Error fetching data from OpenAI API");
            return result.Choices[0].Message.Content;
        }
        else
        {
            throw new HttpRequestException($"Error fetching data from OpenAI API: {response.StatusCode}");
        }
    }
}
