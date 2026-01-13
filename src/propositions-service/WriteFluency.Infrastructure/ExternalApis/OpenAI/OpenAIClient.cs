using System.Net.Http.Json;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WriteFluency.Infrastructure.Http.Services;
using WriteFluency.Propositions;
using WriteFluency.Shared;
using WriteFluency.TextComparisons;
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
        try
        {
            // Generate initial paragraph from article
            var paragraphText = await GenerateParagraphAsync(articleContent, cancellationToken);
            if (string.IsNullOrWhiteSpace(paragraphText))
            {
                _logger.LogError("Generated paragraph is empty");
                return Result.Fail(new Error("Generated paragraph is empty"));
            }

            // Extract proper names from paragraph
            var properNames = await ExtractProperNamesAsync(paragraphText, cancellationToken);
            if (properNames == null || properNames.Count == 0)
            {
                _logger.LogWarning("No proper names extracted from paragraph");
            }
            if(properNames?.Count > 3)
            {
                string errorMsg = "Could not complet proposition generation because the news content has too many proper names";
                _logger.LogError(errorMsg);
                return Result.Fail(new Error(errorMsg));
            }

            // Generate title based on paragraph and proper names
            var title = await GenerateTitleAsync(paragraphText, properNames!, cancellationToken);
            if (string.IsNullOrWhiteSpace(title))
            {
                _logger.LogError("Generated title is empty");
                return Result.Fail(new Error("Generated title is empty"));
            }

            // Adjust difficulty for beginner/intermediate levels
            if (ShouldAdjustComplexity(complexity))
            {
                paragraphText = await AdjustTextComplexityAsync(paragraphText, title, complexity, cancellationToken);
                if (string.IsNullOrWhiteSpace(paragraphText))
                {
                    _logger.LogError("Adjusted paragraph is empty after complexity adjustment");
                    return Result.Fail(new Error("Failed to adjust text complexity"));
                }
            }

            return Result.Ok(new AIGeneratedTextDto(title, paragraphText));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate text with OpenAI");
            return Result.Fail(new Error($"Failed to generate text: {ex.Message}"));
        }
    }

    private async Task<string> GenerateParagraphAsync(string articleContent, CancellationToken cancellationToken)
    {
        var response = await _chatClient.GetResponseAsync<string>(
            CreateMessages(GenerateTextSystemPrompt(), GenerateTextUserPrompt(articleContent)),
            CreateChatOptions(maxTokens: 1200),
            cancellationToken: cancellationToken);

        return response.Result;
    }

    private async Task<List<string>> ExtractProperNamesAsync(string paragraphText, CancellationToken cancellationToken)
    {
        var response = await _chatClient.GetResponseAsync<string>(
            CreateMessages(ExtractProperNamesSystemPrompt(), ExtractProperNamesUserPrompt(paragraphText)),
            CreateChatOptions(maxTokens: 200, temperature: 0.3f),
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Result))
            return new List<string>();

        // Parse the comma-separated list of proper names
        return response.Result
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private async Task<string> GenerateTitleAsync(string paragraphText, List<string> properNames, CancellationToken cancellationToken)
    {
        var response = await _chatClient.GetResponseAsync<string>(
            CreateMessages(GenerateTitleSystemPrompt(), GenerateTitleUserPrompt(paragraphText, properNames)),
            CreateChatOptions(maxTokens: 300),
            cancellationToken: cancellationToken);

        return response.Result;
    }

    private async Task<string> AdjustTextComplexityAsync(string paragraphText, string title, ComplexityEnum complexity, CancellationToken cancellationToken)
    {
        var response = await _chatClient.GetResponseAsync<string>(
            CreateMessages(AdaptTextDifficultySystemPrompt(), AdaptTextDifficultyUserPrompt(paragraphText, title, complexity)),
            CreateChatOptions(maxTokens: 1200),
            cancellationToken: cancellationToken);

        return response.Result;
    }

    private static bool ShouldAdjustComplexity(ComplexityEnum complexity) =>
        complexity is ComplexityEnum.Beginner or ComplexityEnum.Intermediate;

    private ChatMessage[] CreateMessages(string systemPrompt, string userPrompt) =>
        [
            new ChatMessage(ChatRole.System, ProperNameDefinitionSystemPrompt()),
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ];

    private static ChatOptions CreateChatOptions(int maxTokens, float temperature = 0.7f) =>
        new() { MaxOutputTokens = maxTokens, Temperature = temperature };

    private string ProperNameDefinitionSystemPrompt()
    => """
        Definition of proper names:

        - A proper name is any word or sequence of words that starts with a capital letter AND refers to a specific, unique entity that requires external or contextual knowledge to be written correctly.

        - Proper names identify things that a person would not necessarily know how to spell or name without prior exposure or knowledge of that entity.

        Included examples of proper names:
            people; cities, regions, and countries; teams and sports franchises; companies and organizations;
            venues and buildings; schools and institutions; events, tournaments, leagues, and competitions;
            media outlets and platforms; ships and vehicles; historical periods and official programs;
            products, services, and technologies; government bodies and public agencies;
            laws and treaties; political parties; hospitals and research centers;
            financial institutions; natural landmarks and geographic features;
            neighborhoods and districts; infrastructure such as highways and bridges;
            software, applications, and websites; awards, festivals, books, movies, and TV shows.

        Explicit exclusions (NOT proper names), even if capitalized:
            - Names of months (e.g. January, December)
            - Names of days of the week (e.g. Monday, Friday)
            - Languages and nationalities (e.g. English, Spanish, Brazilian)
            - Common holidays and seasons (e.g. Christmas, Summer)
            - General demonyms or adjectives derived from places
            - Any common word that is capitalized only due to grammar rules and does not refer to a unique entity
        """;

    private string GenerateTextSystemPrompt()
        => @$"
            You are writing for an English-learning app where users listen to an audio and try to transcribe what they hear, word for word.

            Based on the following article, your task is to generate an adapted version of the content, using natural, global English.

            Return a single paragraph (600 - 1000 characters) that retells the main story in a clear, engaging, and listener-friendly way.

            Rules:

                - Write the paragraph as if you are telling someone an interesting, surprising, or emotional story. Make it engaging.

                - Ensure that the paragraph can be fully and accurately transcribed just by listening to it. The user must be able to write the exact text without seeing it.

                - Use 1 to 3 proper names (people, places, organizations) in the entire paragraph.

                - Use general terms (e.g., 'a man,' 'a major city') when a name is not essential.

                - Do not refer to the article, news source, journalist, or writing process.

                - Avoid acronyms, abbreviations, dates, and complex numbers. Use simple, spoken equivalents instead. 

                - Do not use em dashes (—), single or double quotes, or symbols such as %, $, “ ”, or bullets. Use plain punctuation (commas, periods, etc.).

                - Use globally understandable, neutral vocabulary. Avoid slang, idioms, and regional expressions.

                - Do not use line breaks, paragraph spacing, or formatting.

                - The users are Advanced and fluent English learners, so use natural and sophisticated language.

            If the article does not provide enough clear, interesting, or informative content to generate a meaningful paragraph and title, return null.
        ";

    private string GenerateTextUserPrompt(string articleContent)
        => @$"
            --- ARTICLE START ---
            {articleContent}
            --- ARTICLE END ---
        ";

    private string ExtractProperNamesSystemPrompt()
    => """
        You are extracting proper names from the provided text.
        
        Your task is to identify and list ALL proper names that appear in the text.
        
        Output format:
        - Return a comma-separated list of proper names (e.g., "John Smith, Paris, Microsoft")
        - If no proper names are found, return an empty string
        - Do not include any additional commentary or formatting
        - Only include names that are actually capitalized in the text
    """;

    private string ExtractProperNamesUserPrompt(string paragraphText)
        => @$"
            --- TEXT START ---
            {paragraphText}
            --- TEXT END ---
        ";

    private string GenerateTitleSystemPrompt()
    => """
        You are generating a title based strictly on the provided text.

        Critical rule (must follow):
        - Every PROPER NAME from the provided list must appear in the title.
        - If it is not possible to include all proper names in a single coherent title, return null.

        Output only the title text, without any additional commentary or formatting.
    """;

    private string GenerateTitleUserPrompt(string articleContent, List<string> properNames)
        => @$"
            Proper names that MUST appear in the title:
            {(properNames != null && properNames.Count > 0 ? string.Join(", ", properNames) : "None")}

            --- TEXT START ---
            {articleContent}
            --- TEXT END ---
        ";

    private string AdaptTextDifficultySystemPrompt()
    => """
        You are given:
        - A title
        - A single paragraph of text written at an advanced English level
        - A target complexity level: Beginner or Intermediate

        Your task is to rewrite ONLY the paragraph by SIMPLIFYING the language to match the target complexity level.
        The title must remain EXACTLY the same and must NOT be modified.

        Core rules (must follow):
        - The original paragraph is already advanced and correct.
        - Do NOT change the meaning, facts, events, or message of the paragraph.
        - Do NOT add new information.
        - Do NOT remove important details.
        - The rewritten paragraph must communicate the same story and content as the original text.
        - Keep all proper names exactly as they appear in the original paragraph.
        - Do NOT introduce new proper names.

        Simplification rules:
        - Beginner:
        - Use short, direct sentences.
        - Use simple verb tenses and active voice.
        - Prefer common, everyday vocabulary.
        - Avoid complex clauses, passive voice, and abstract expressions.
        - Intermediate:
        - Use clear sentences with moderate length.
        - Allow basic connectors such as because, when, after, and while.
        - Use a wider vocabulary than Beginner, but avoid advanced or academic wording.

        Formatting and language rules:
        - Rewrite ONLY the paragraph.
        - Return a single paragraph with no line breaks.
        - Do not use quotes, symbols, abbreviations, or special punctuation.
        - Use natural, globally understandable English.

        If you cannot simplify the paragraph while fully preserving its original meaning, return null.
    """;

    private string AdaptTextDifficultyUserPrompt(string paragraph, string title, ComplexityEnum complexity)
        => @$"
            Target complexity level: {complexity}

            TITLE: {title}

            --- TEXT START ---
            {paragraph}
            --- TEXT END ---
        ";

}