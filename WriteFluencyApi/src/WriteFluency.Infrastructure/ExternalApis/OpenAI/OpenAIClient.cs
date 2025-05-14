using System.Net.Http.Json;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WriteFluency.Domain.Extensions;
using WriteFluency.Infrastructure.ExternalApis.OpenAI.Enums;
using WriteFluency.Infrastructure.Http.Services;
using WriteFluency.Propositions;
using WriteFluency.Shared;
using WriteFluency.TextComparisons;

namespace WriteFluency.Infrastructure.ExternalApis;

public class OpenAIClient : BaseHttpClientService, IGenerativeAIClient
{
    private readonly OpenAIOptions _options;

    public OpenAIClient(HttpClient httpClient, ILogger<OpenAIClient> logger, IOptions<OpenAIOptions> options)
        : base(httpClient, logger)
    {
        _options = options.Value;
    }

    [Obsolete]
    public async Task<string> GenerateTextAsync(GeneratePropositionDto generateTextDto, int attempt = 1)
    {
        var request = new CompletionRequest
        {
            Model = "gpt-3.5-turbo",
            Messages = new List<RequestMessage>()
                { new RequestMessage() {
                    Content = GenerateTextPrompt(generateTextDto)
                    } },
            MaxTokens = 1200,
            Temperature = 1.0m
        };

        var response = await _httpClient.PostAsJsonAsync(_options.Routes.Completion, request);

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

    private string GenerateTextPrompt(GeneratePropositionDto dto)
        => @$"
            Write about some subject related to {dto.Subject.GetDescription()}.
            Maximum of one paragraph, from 250 to 600 characteres.
            Write it in a way that normal people can understand well, without specialist vocabulary.
            Write just the text please.
            Without titles.
            Without identation, like paragraphs.
            Without line breaks.
            Without special characters, like quotes. 
            Don't use $100, use '100 dollars'.
            Be creative.
            {dto.Complexity.GetDescription()}
        ";

    public async Task<Result<string>> GenerateTextAsync(ComplexityEnum complexity, string articleContent)
    {
        var request = new CompletionRequest
        {
            Model = "gpt-4.1-nano",
            Messages = new List<RequestMessage>()
                { new RequestMessage() {
                    Content = GenerateTextPrompt(complexity, articleContent)
                    } },
            MaxTokens = 1200,
            Temperature = 1.0m
        };

        var requestResult = await PostAsync(_options.Routes.Completion, request, new CompletionResponseValidator());

        if (requestResult.IsFailed)
        {
            var errorMessage = requestResult.Errors.Message();
            _logger.LogError("Error fetching data from OpenAI API (Completion endpoint): {ErrorMessage}", errorMessage);
            return Result.Fail(new Error($"Error when calling OpenAI API (Completion endpoint). {errorMessage}"));
        }

        return Result.Ok(requestResult.Value.Choices[0].Message.Content);
    }

    private string GenerateTextPrompt(ComplexityEnum complexity, string articleContent)
        => @$"
            You are writing for an English-learning app where users listen to a short audio and try to transcribe what they hear.
 
            Based on the following article, write a short and simple adapted version of the main idea, using natural English:
            
            """"""
            {articleContent}
            """"""
 
            Guidelines:
            - Write a single paragraph with 250 to 600 characters.
            - Avoid long or difficult names, dates, or detailed statistics.
            - Use common vocabulary and clear sentence structure.
            - Avoid titles, line breaks, quotes, or special formatting.
            - Do not include a title, introduction, or closing phrase.
            - Do not add anything unrelated to the article.
            - Don't use $100, use '100 dollars'.
 
            Tone:
            - {complexity.GetDescription()}
            - Be engaging, informative, and easy to understand.
 
            Write only the adapted paragraph.
        ";
        
    public async Task<Result<AudioDto>> GenerateAudioAsync(string text)
    {
        var voices = Enum.GetValues<VoicesEnum>();
        var voice = voices[new Random().Next(0, voices.Length)].ToString().ToLower();
        var request = new SpeechRequest(
            "gpt-4o-mini-tts",
            text,
            voice
        );

        var requestResult = await PostAsync<SpeechRequest, byte[]>(_options.Routes.Speech, request);

        if(requestResult.IsFailed)
        {
            var errorMessage = requestResult.Errors.Message();
            _logger.LogError("Error fetching data from OpenAI API (Speech endpoint): {ErrorMessage}", errorMessage);
            return Result.Fail(new Error($"Error when calling OpenAI API (Speech endpoint). {errorMessage}"));
        }
       
        return Result.Ok(new AudioDto(requestResult.Value, voice));
    }
}
