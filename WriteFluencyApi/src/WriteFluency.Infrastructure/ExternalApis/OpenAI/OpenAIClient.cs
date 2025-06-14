using System.Net.Http.Json;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WriteFluency.Infrastructure.ExternalApis.OpenAI.Enums;
using WriteFluency.Infrastructure.Http.Services;
using WriteFluency.Propositions;
using WriteFluency.Shared;
using WriteFluency.TextComparisons;
using WriteFluency.Extensions;
using Microsoft.Extensions.AI;

namespace WriteFluency.Infrastructure.ExternalApis;

public class OpenAIClient : BaseHttpClientService, IGenerativeAIClient
{
    private readonly OpenAIOptions _options;
    private readonly IChatClient _chatClient;

    public OpenAIClient(
        HttpClient httpClient,
        ILogger<OpenAIClient> logger,
        IOptionsMonitor<OpenAIOptions> options,
        IChatClient chatClient)
        : base(httpClient, logger)
    {
        _options = options.CurrentValue;
        _chatClient = chatClient;
    }

    [Obsolete]
    public async Task<string> GenerateTextAsync(GeneratePropositionDto generateTextDto, int attempt = 1, CancellationToken cancellationToken = default)
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

        var response = await _httpClient.PostAsJsonAsync(_options.Routes.Completion, request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<CompletionResponse>(cancellationToken)
                ?? throw new HttpRequestException("Error fetching data from OpenAI API");
            return result.Choices[0].Message.Content;
        }
        else
        {
            await Task.Delay(1000);
            if (attempt == 1) return await GenerateTextAsync(generateTextDto, 2, cancellationToken);
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

    public async Task<Result<AIGeneratedTextDto>> GenerateTextAsync(ComplexityEnum complexity, string articleContent, CancellationToken cancellationToken = default)
    {
        var prompt = GenerateTextPrompt(complexity, articleContent);

        var result = await _chatClient.GetResponseAsync<AIGeneratedTextDto>(
            [new ChatMessage(ChatRole.User, prompt)],
            new ChatOptions() { MaxOutputTokens = 1200, Temperature = 1.0f },
            cancellationToken: cancellationToken);

        if(result.TryGetResult(out var response))
        {
            return Result.Ok(response);
        }
        else
        {
            _logger.LogError("Error fetching data from OpenAI API (Chat endpoint)");
            return Result.Fail(new Error($"Error when calling OpenAI API (Chat endpoint)."));
        }
    }

    private string GenerateTextPrompt(ComplexityEnum complexity, string articleContent)
        => @$"
            You are writing for an English-learning app where users listen to an audio and try to transcribe what they hear, word for word.

            Based on the following article, your task is to generate an adapted version of the content, using natural, global English. The output must contain two parts:

            1. A catchy and relevant **title** (up to one sentence). The title can include proper names or specific terms if necessary.
            2. A single **paragraph** (500 - 1000 characters) that retells the main story in a clear, engaging, and listener-friendly way.

            Rules:
            - Write as if you're telling someone an interesting, surprising, or emotional story.
            - Avoid long or difficult names, places, or organizations unless they also appear in the title and are reasonably simple to understand when heard.
            - Use general terms (e.g., 'a man,' 'a major city') when a name is not essential.
            - Include names in the title if they are part of the article and easy to understand.
            - Avoid self-references to the article, news, journalist, or writing process.
            - Do not begin every paragraph the same way (e.g. avoid always starting with 'Imagine...').
            - Avoid acronyms, abbreviations, dates, or complex numbers.
            - Never include em dashes (—), single/double quotes, or symbols like %, $, “ ”, or bullets. Use plain punctuation (commas, periods, etc.).
            - Use globally understandable, neutral vocabulary — avoid slang and local references.
            - Do not use line breaks, paragraph spacing, or formatting.
            - Only mention proper names (people, places, etc.) **when essential to the story**.
            - If using a proper name in the paragraph, try to mention it in the title as well, so users will see the correct spelling before hearing it.

            Complexity levels (only apply one, according to the specified level below):
            - Beginner: {ComplexityEnum.Beginner.GetDescription()}
            - Intermediate: {ComplexityEnum.Intermediate.GetDescription()}
            - Advanced: {ComplexityEnum.Advanced.GetDescription()}

            --- ARTICLE START ---
            {articleContent}
            --- ARTICLE END ---

            Use the following complexity level: **{complexity.GetDescription()}**
        ";

    public async Task<Result<AudioDto>> GenerateAudioAsync(string text, CancellationToken cancellationToken = default)
    {
        var voices = Enum.GetValues<VoicesEnum>();
        var voice = voices[new Random().Next(0, voices.Length)].ToString().ToLower();
        var request = new SpeechRequest(
            "gpt-4o-mini-tts",
            text,
            voice
        );

        var requestResult = await PostAsync<SpeechRequest, byte[]>(
            _options.Routes.Speech, request, cancellationToken: cancellationToken);

        if (requestResult.IsFailed)
        {
            var errorMessage = requestResult.Errors.Message();
            _logger.LogError("Error fetching data from OpenAI API (Speech endpoint): {ErrorMessage}", errorMessage);
            return Result.Fail(new Error($"Error when calling OpenAI API (Speech endpoint). {errorMessage}"));
        }

        return Result.Ok(new AudioDto(requestResult.Value, voice));
    }
}
