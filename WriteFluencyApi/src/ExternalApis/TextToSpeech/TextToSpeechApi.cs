using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using WriteFluencyApi.ListenAndWrite;
using WriteFluencyApi.ListenAndWrite.Domain;

namespace WriteFluencyApi.ExternalApis.TextToSpeech;

public class TextToSpeechApi : ISpeechGenerator
{
    private readonly HttpClient _httpClient;
    private readonly TextToSpeechOptions _textToSpeechConfig;

    public TextToSpeechApi(HttpClient httpClient, IOptions<TextToSpeechOptions> textToSpeechConfig)
    {
        _httpClient = httpClient;
        _textToSpeechConfig = textToSpeechConfig.Value;
        _httpClient.BaseAddress = new Uri(_textToSpeechConfig.BaseAddress);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add(_textToSpeechConfig.KeyName, _textToSpeechConfig.Key);
    }

    public async Task<byte[]> GenerateSpeechAsync(string text, int attempt = 1)
    {
        var request = new TextToSpeechRequest(
            new Input(text),
            new AudioConfig("MP3"),
            new Voice("en-US")
        );

        var response = await _httpClient.PostAsJsonAsync(_textToSpeechConfig.Routes.TextSynthesize, request);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<TextToSpeechResponse>()
                ?? throw new HttpRequestException("Error fetching data from TextToSpeech API");
            return Convert.FromBase64String(result.AudioContent);
        }
        else
        {
            await Task.Delay(1000);
            if (attempt == 1) return await GenerateSpeechAsync(text, 2);
            else throw new HttpRequestException($"Error fetching data from TextToSpeech API: {response.StatusCode}");
        }
    }
}
