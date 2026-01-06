using FluentResults;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WriteFluency.Propositions;

namespace WriteFluency.Infrastructure.ExternalApis;

public class TextToSpeechClient : ITextToSpeechClient
{
    private readonly TextToSpeechOptions _options;
    private readonly ILogger<TextToSpeechClient> _logger;

    private readonly string[] _voices = [
        "en-US-AdamMultilingualNeural",
        "en-US-JasonNeural",
        "en-US-SamuelMultilingualNeural",
        "en-US-AvaMultilingualNeural",
        "en-US-AndrewMultilingualNeural",
        "en-US-PhoebeMultilingualNeural",
        "en-US-SteffanMultilingualNeural",
        "en-US-BrianMultilingualNeural",
        "en-US-AvaNeural",
        "en-US-KaiNeural",
        "en-US-LunaNeural",
        "en-US-JennyNeural",
        "en-US-DustinMultilingualNeural",
    ];

    public TextToSpeechClient(HttpClient httpClient, IOptionsMonitor<TextToSpeechOptions> textToSpeechConfig, ILogger<TextToSpeechClient> logger)
    {
        _options = textToSpeechConfig.CurrentValue;
        _logger = logger;
    }

    public async Task<Result<AudioDto>> GenerateAudioAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var speechConfig = SpeechConfig.FromSubscription(_options.Key, "eastus");
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio24Khz48KBitRateMonoMp3);
            string randomVoice = _voices[new Random().Next(0, _voices.Length)];
            speechConfig.SpeechSynthesisVoiceName = randomVoice;
            using var speechSynthesizer = new SpeechSynthesizer(speechConfig, null);
            var result = await speechSynthesizer.SpeakTextAsync(text);
            return Result.Ok(new AudioDto(result.AudioData, randomVoice));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching data from Azure TTS API (Speech endpoint)");
            return Result.Fail(new Error($"Error when calling Azure TTS API (Speech endpoint). {ex.Message}"));
        }
    }
}
