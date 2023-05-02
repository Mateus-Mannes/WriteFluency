using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WriteFluencyApi.Dtos.ListenAndWrite;
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
        var content = new
        {
            model = "gpt-3.5-turbo",
            prompt = "Say this is a test!",
            max_tokens = 700,
            temperature = 1.0
        };

        var response = await _httpClient.PostAsJsonAsync(_openAIConfig.Routes.Completion, content);

        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadFromJsonAsync<string>();
            dynamic parsedJson = JsonConvert.DeserializeObject(responseJson);
            return parsedJson.choices[0].text.ToString().Trim();
        }
        else
        {
            throw new HttpRequestException($"Error fetching data from OpenAI API: {response.StatusCode}");
        }
    }
}
