using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using WriteFluencyApi.ExternalApis.TextToSpeech.Requests;
using WriteFluencyApi.ExternalApis.TextToSpeech.Responses;
using WriteFluencyApi.Services.ListenAndWrite;

namespace WriteFluencyApi.ExternalApis.TextToSpeech;

public class TextToSpeechApi : ISpeechGenerator
{
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly TextToSpeechConfig _textToSpeechConfig;

    public TextToSpeechApi(IOptions<TextToSpeechConfig> textToSpeechConfig)
    {
        _textToSpeechConfig = textToSpeechConfig.Value;
        _httpClient.BaseAddress = new Uri(_textToSpeechConfig.BaseAddress);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add(_textToSpeechConfig.KeyName, _textToSpeechConfig.Key);
    }

    public async Task<byte[]> GenerateSpeechAsync(string text)
    {
        var request = new TextToSpeechRequest(
            new Input(text),
            new AudioConfig("OGG_OPUS"),
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
            throw new HttpRequestException($"Error fetching data from TextToSpeech API: {response.StatusCode}");
        }
    }
}
