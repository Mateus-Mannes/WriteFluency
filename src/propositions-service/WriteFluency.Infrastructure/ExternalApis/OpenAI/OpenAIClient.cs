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
    public async Task<string> GenerateTextAsync(GetPropositionDto generateTextDto, int attempt = 1, CancellationToken cancellationToken = default)
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

    private string GenerateTextPrompt(GetPropositionDto dto)
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
        var systemPrompt = GenerateTextSystemPrompt();
        var userPrompt = GenerateTextUserPrompt(complexity, articleContent);

        var result = await _chatClient.GetResponseAsync<AIGeneratedTextDto>(
            [new(ChatRole.System, systemPrompt), new ChatMessage(ChatRole.User, userPrompt)],
            new ChatOptions() { MaxOutputTokens = 1200, Temperature = 1.0f },
            cancellationToken: cancellationToken);

        if (result.TryGetResult(out var response))
        {
            return Result.Ok(response);
        }
        else
        {
            var error = "The Generative AI could not generate the text in the specified format based on the article content.";
            _logger.LogError(error);
            return Result.Fail(new Error(error));
        }
    }

    private string GenerateTextSystemPrompt()
        => @$"
            You are writing for an English-learning app where users listen to an audio and try to transcribe what they hear, word for word.

            Based on the following article, your task is to generate an adapted version of the content, using natural, global English. The output must contain two parts:

            1. A catchy and relevant title.
            2. A single paragraph (600 - 1000 characters) that retells the main story in a clear, engaging, and listener-friendly way.

            Rules:

                - Write the paragraph as if you are telling someone an interesting, surprising, or emotional story. Make it engaging.

                - Ensure that the paragraph can be fully and accurately transcribed just by listening to it. The user must be able to write the exact text without seeing it.

                - Every proper name (person, city, organization, companies, etc.) used in the paragraph must also appear in the title. This is mandatory. Never include a name in the paragraph unless it also appears in the title.

                - Use general terms (e.g., 'a man,' 'a major city') when a name is not essential.

                - If the event happens in a specific location (city, state, country, or region), include that location in both the title and the paragraph.

                - Do not refer to the article, news source, journalist, or writing process.

                - Do not start every paragraph the same way — avoid patterns like always beginning with “Imagine...”.

                - Avoid acronyms, abbreviations, dates, and complex numbers. Use simple, spoken equivalents instead. 

                - Do not use em dashes (—), single or double quotes, or symbols such as %, $, “ ”, or bullets. Use plain punctuation (commas, periods, etc.).

                - Use globally understandable, neutral vocabulary. Avoid slang, idioms, and regional expressions.

                - Do not use line breaks, paragraph spacing, or formatting.

            Complexity levels (only apply one, according to the specified level below):
            - Beginner: {ComplexityEnum.Beginner.GetDescription()}
            - Intermediate: {ComplexityEnum.Intermediate.GetDescription()}
            - Advanced: {ComplexityEnum.Advanced.GetDescription()}

            If the article does not provide enough clear, interesting, or informative content to generate a meaningful paragraph and title, return null.
        ";

    private string GenerateTextUserPrompt(ComplexityEnum complexity, string articleContent)
        => @$"
            Apply complexity level: {complexity}.

            --- ARTICLE START ---
            {articleContent}
            --- ARTICLE END ---
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
