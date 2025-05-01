using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using WriteFluency.Propositions;
using WriteFluency.Shared;
using WriteFluency.TextComparisons;

namespace WriteFluency.Infrastructure.ExternalApis.OpenAI;

public class OpenAIClient : ITextGenerator
{
    private readonly HttpClient _httpClient;
    private readonly OpenAIOptions _options;

    public OpenAIClient(HttpClient httpClient, IOptions<OpenAIOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

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
}
