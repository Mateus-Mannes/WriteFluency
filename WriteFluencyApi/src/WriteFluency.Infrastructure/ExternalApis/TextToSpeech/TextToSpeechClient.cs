using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using WriteFluency.Propositions;

namespace WriteFluency.Infrastructure.ExternalApis;

[Obsolete]
public class TextToSpeechClient : ITextToSpeechClient
{
    private readonly HttpClient _httpClient;
    private readonly TextToSpeechOptions _options;

    public TextToSpeechClient(HttpClient httpClient, IOptions<TextToSpeechOptions> textToSpeechConfig)
    {
        _httpClient = httpClient;
        _options = textToSpeechConfig.Value;
    }

    public async Task<byte[]> GenerateSpeechAsync(string text, int attempt = 1)
    {
        var request = new TextToSpeechRequest(
            new Input(text),
            new AudioConfig("MP3"),
            new Voice("en-US")
        );

        var response = await _httpClient.PostAsJsonAsync(_options.Routes.TextSynthesize, request);

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
