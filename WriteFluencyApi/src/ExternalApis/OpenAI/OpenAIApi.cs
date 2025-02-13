using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using WriteFluencyApi.ListenAndWrite;
using WriteFluencyApi.ListenAndWrite.Domain;

namespace WriteFluencyApi.ExternalApis.OpenAI;

public class OpenAIApi : ITextGenerator
{
    private readonly HttpClient _httpClient;
    private readonly OpenAIOptions _openAIConfig;

    public OpenAIApi(HttpClient httpClient, IOptions<OpenAIOptions> openAIConfig)
    {
        _httpClient = httpClient;
        _openAIConfig = openAIConfig.Value;
        _httpClient.BaseAddress = new Uri(_openAIConfig.BaseAddress);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _openAIConfig.Key);
    }

    public async Task<string> GenerateTextAsync(GeneratePropositionDto generateTextDto, int attempt = 1)
    {
        var request = new CompletionRequest
        {
            Model = "gpt-3.5-turbo",
            Messages = new List<RequestMessage>()
                { new RequestMessage() {
                    Content = Prompts.GenerateText(generateTextDto)
                    } },
            MaxTokens = 1200,
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
            await Task.Delay(1000);
            if (attempt == 1) return await GenerateTextAsync(generateTextDto, 2);
            else throw new HttpRequestException($"Error fetching data from OpenAI API: {response.StatusCode}");
        }
    }
}
