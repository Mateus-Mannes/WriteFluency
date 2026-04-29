using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace WriteFluency.Infrastructure.ExternalApis;

public class OpenAIClientTests
{
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

    private static OpenAIClient CreateClient(IChatClient chatClient)
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<OpenAIOptions>>();
        optionsMonitor.CurrentValue.Returns(new OpenAIOptions
        {
            Key = "test-key",
            BaseAddress = "https://example.test",
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
}
