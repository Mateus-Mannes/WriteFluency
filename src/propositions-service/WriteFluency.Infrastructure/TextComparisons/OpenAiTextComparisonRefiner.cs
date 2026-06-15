using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using WriteFluency.TextComparisons;

namespace WriteFluency.Infrastructure.TextComparisons;

public sealed class OpenAiTextComparisonRefiner : ITextComparisonAiRefiner
{
    private readonly IChatClient _chatClient;
    private readonly AiRefinementOptions _options;
    private readonly ILogger<OpenAiTextComparisonRefiner> _logger;

    public OpenAiTextComparisonRefiner(
        IChatClient chatClient,
        IOptions<AiRefinementOptions> options,
        ILogger<OpenAiTextComparisonRefiner> logger)
    {
        _chatClient = chatClient;
        _options = options.Value;
        _logger = logger;
    }

    public string Model => _options.Model;
    public string PromptVersion => TextComparisonAiPrompt.Version;

    public async Task<AiRefinementResult> RefineAsync(
        AiRefinementRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await _chatClient.GetResponseAsync<StructuredRefinementResponse>(
                TextComparisonAiPrompt.CreateMessages(request),
                CreateChatOptions(),
                useJsonSchemaResponseFormat: true,
                cancellationToken: cancellationToken);

            if (response.FinishReason == Microsoft.Extensions.AI.ChatFinishReason.Length)
            {
                throw new InvalidOperationException(
                    $"The AI refinement response was truncated after reaching the {_options.MaxOutputTokens}-token output limit.");
            }

            if (!response.TryGetResult(out var structuredResponse)
                || structuredResponse?.Comparisons is null)
            {
                throw new InvalidOperationException(
                    "The AI refinement response did not match the required schema.");
            }

            return new AiRefinementResult(
                structuredResponse.Comparisons
                    .Select(comparison => ToAbsoluteComparison(request, comparison))
                    .ToList(),
                response.ModelId ?? Model,
                PromptVersion,
                stopwatch.ElapsedMilliseconds,
                response.Usage?.InputTokenCount,
                response.Usage?.OutputTokenCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "AI comparison refinement failed: Model={Model}, PromptVersion={PromptVersion}, ComparisonCount={ComparisonCount}, DurationMs={DurationMs}",
                Model,
                PromptVersion,
                request.Comparisons.Count,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private ChatOptions CreateChatOptions() =>
        new()
        {
            ModelId = Model,
            MaxOutputTokens = _options.MaxOutputTokens,
            RawRepresentationFactory = _ => new ChatCompletionOptions
            {
                ReasoningEffortLevel = ToReasoningEffort(_options.ReasoningEffort)
            }
        };

    private static ChatReasoningEffortLevel ToReasoningEffort(string reasoningEffort) =>
        reasoningEffort.Trim().ToLowerInvariant() switch
        {
            "low" => ChatReasoningEffortLevel.Low,
            "medium" => ChatReasoningEffortLevel.Medium,
            "high" => ChatReasoningEffortLevel.High,
            _ => throw new InvalidOperationException(
                $"Unsupported AI refinement reasoning effort '{reasoningEffort}'.")
        };

    private static AiRefinedComparison ToAbsoluteComparison(
        AiRefinementRequest request,
        StructuredRefinedComparison comparison)
    {
        var source = request.Comparisons.FirstOrDefault(
            candidate => candidate.SourceComparisonIndex == comparison.SourceComparisonIndex);

        if (source is null)
        {
            return new AiRefinedComparison(
                comparison.SourceComparisonIndex,
                -1,
                -1,
                -1,
                -1);
        }

        return new AiRefinedComparison(
            comparison.SourceComparisonIndex,
            AddOffset(source.OriginalTextRange.InitialIndex, comparison.OriginalTextStartOffset),
            AddOffset(source.OriginalTextRange.InitialIndex, comparison.OriginalTextEndOffset),
            AddOffset(source.UserTextRange.InitialIndex, comparison.UserTextStartOffset),
            AddOffset(source.UserTextRange.InitialIndex, comparison.UserTextEndOffset));
    }

    private static int AddOffset(int initialIndex, int offset)
    {
        var result = (long)initialIndex + offset;
        return result switch
        {
            > int.MaxValue => int.MaxValue,
            < int.MinValue => int.MinValue,
            _ => (int)result
        };
    }

    public sealed class StructuredRefinementResponse
    {
        public required List<StructuredRefinedComparison> Comparisons { get; init; }
    }

    public sealed class StructuredRefinedComparison
    {
        public required int SourceComparisonIndex { get; init; }
        public required int OriginalTextStartOffset { get; init; }
        public required int OriginalTextEndOffset { get; init; }
        public required int UserTextStartOffset { get; init; }
        public required int UserTextEndOffset { get; init; }
    }
}
