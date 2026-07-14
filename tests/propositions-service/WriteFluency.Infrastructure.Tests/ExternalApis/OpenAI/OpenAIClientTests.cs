using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using WriteFluency.Propositions;

namespace WriteFluency.Infrastructure.ExternalApis;

public class OpenAIClientTests
{
    [Fact]
    public async Task GenerateTextAsync_WhenArticleValidationReturnsStructuredInvalidResponse_ShouldRejectArticleWithoutValidationFailure()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Is<ChatOptions>(options => options.ModelId == "test-validation-model"),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, """{"isValid":false}"""))));

        var client = CreateClient(chatClient, articleValidationModel: "test-validation-model");

        var result = await client.GenerateTextAsync(ComplexityEnum.Intermediate, "Navigation links and repeated menu text.");

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(error => error.Message).ShouldContain("Article content is invalid according to AI validation rules");
    }

    [Fact]
    public async Task ValidateImageAsync_WhenModelReturnsInvalid_ShouldReturnFalse()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "invalid"))));

        var client = CreateClient(chatClient);

        var result = await client.ValidateImageAsync([1, 2, 3], "Article title");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task GenerateTextAsync_WhenGeneratingInitialParagraph_ShouldUseConfiguredParagraphGenerationModel()
    {
        var chatClient = new CapturingChatClient();

        var client = CreateClient(
            chatClient,
            articleValidationModel: "test-validation-model",
            paragraphGenerationModel: "gpt-5.4-mini");

        await client.GenerateTextAsync(ComplexityEnum.Advanced, "Article text with enough readable information.");

        chatClient.CapturedOptions.ShouldContain(options =>
            options != null && options.ModelId == "gpt-5.4-mini" && options.MaxOutputTokens == 1200);
    }

    private static OpenAIClient CreateClient(
        IChatClient chatClient,
        string articleValidationModel = "gpt-5.4-nano-2026-03-17",
        string paragraphGenerationModel = "gpt-5.4-mini")
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<OpenAIOptions>>();
        optionsMonitor.CurrentValue.Returns(new OpenAIOptions
        {
            Key = "test-key",
            BaseAddress = "https://example.test",
            ParagraphGenerationModel = paragraphGenerationModel,
            ArticleValidationModel = articleValidationModel,
            Routes = new OpenAIOptions.OpenAIRoutes
            {
                Completion = "/completion",
                Speech = "/speech"
            }
        });

        return new OpenAIClient(
            new HttpClient(),
            NullLogger<OpenAIClient>.Instance,
            optionsMonitor,
            chatClient);
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public List<ChatOptions?> CapturedOptions { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CapturedOptions.Add(options);
            if (CapturedOptions.Count == 1)
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, """{"isValid":true}""")));

            var response = options?.ModelId switch
            {
                "gpt-5.4-mini" => "\"A clear paragraph about a local community project.\"",
                _ when options?.MaxOutputTokens == 200 => "\"Alice\"",
                _ when options?.MaxOutputTokens == 300 => "\"Alice Shares Local Project With Community Members\"",
                _ when options?.MaxOutputTokens == 80 => "\"valid\"",
                _ => "\"\""
            };

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
