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
            if (request.Comparisons.Count == 0)
            {
                return new AiRefinementResult(
                    [],
                    Model,
                    PromptVersion,
                    stopwatch.ElapsedMilliseconds,
                    0,
                    0);
            }

            var decisions = new List<AiRefinementDecision>(request.Comparisons.Count);
            var inputTokenCount = 0L;
            var outputTokenCount = 0L;
            var hasCompleteUsage = true;
            var responseModel = Model;

            foreach (var comparisons in request.Comparisons.Chunk(
                         _options.MaxComparisonsPerRequest))
            {
                var batchRequest = request with { Comparisons = comparisons };
                var batch = await RefineBatchAsync(batchRequest, cancellationToken);

                decisions.AddRange(batch.Decisions);
                responseModel = batch.Model;
                hasCompleteUsage &= batch.InputTokenCount.HasValue
                    && batch.OutputTokenCount.HasValue;
                inputTokenCount += batch.InputTokenCount ?? 0;
                outputTokenCount += batch.OutputTokenCount ?? 0;
            }

            return new AiRefinementResult(
                decisions,
                responseModel,
                PromptVersion,
                stopwatch.ElapsedMilliseconds,
                hasCompleteUsage ? inputTokenCount : null,
                hasCompleteUsage ? outputTokenCount : null);
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

    private async Task<BatchRefinementResult> RefineBatchAsync(
        AiRefinementRequest request,
        CancellationToken cancellationToken)
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
            || structuredResponse?.Decisions is null)
        {
            throw new InvalidOperationException(
                "The AI refinement response did not match the required schema.");
        }

        return new BatchRefinementResult(
            structuredResponse.Decisions
                .Select(decision => ToDecision(request, decision))
                .ToList(),
            response.ModelId ?? Model,
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount);
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

    private static AiRefinementDecision ToDecision(
        AiRefinementRequest request,
        StructuredRefinementDecision decision) =>
        new(
            decision.SourceComparisonIndex,
            decision.Action ?? string.Empty,
            decision.ReasonCode ?? string.Empty,
            (decision.Comparisons ?? [])
                .Select(comparison => ToAbsoluteComparison(
                    request,
                    decision.SourceComparisonIndex,
                    comparison))
                .ToList());

    private static AiRefinedComparison ToAbsoluteComparison(
        AiRefinementRequest request,
        int sourceComparisonIndex,
        StructuredRefinedComparison comparison)
    {
        var source = request.Comparisons.FirstOrDefault(
            candidate => candidate.SourceComparisonIndex == sourceComparisonIndex);

        if (source is null)
        {
            return new AiRefinedComparison(
                sourceComparisonIndex,
                -1,
                -1,
                -1,
                -1);
        }

        return new AiRefinedComparison(
            sourceComparisonIndex,
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
        public required List<StructuredRefinementDecision> Decisions { get; init; }
    }

    public sealed class StructuredRefinementDecision
    {
        public required int SourceComparisonIndex { get; init; }
        public string? Action { get; init; }
        public string? ReasonCode { get; init; }
        public List<StructuredRefinedComparison>? Comparisons { get; init; }
    }

    public sealed class StructuredRefinedComparison
    {
        public required int OriginalTextStartOffset { get; init; }
        public required int OriginalTextEndOffset { get; init; }
        public required int UserTextStartOffset { get; init; }
        public required int UserTextEndOffset { get; init; }
    }

    private sealed record BatchRefinementResult(
        IReadOnlyList<AiRefinementDecision> Decisions,
        string Model,
        long? InputTokenCount,
        long? OutputTokenCount);
}
