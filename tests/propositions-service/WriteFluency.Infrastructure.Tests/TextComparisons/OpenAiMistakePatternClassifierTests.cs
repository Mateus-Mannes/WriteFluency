using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using WriteFluency.Infrastructure.TextComparisons;
using WriteFluency.TextComparisons;

namespace WriteFluency.Infrastructure.Tests.TextComparisons;

public sealed class OpenAiMistakePatternClassifierTests
{
    [Fact]
    public async Task ClassifyWithDiagnosticsAsync_ShouldMapStructuredAnnotationsAndUseConfiguredOptions()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Is<ChatOptions>(options =>
                    options.ModelId == "test-model"
                    && options.MaxOutputTokens == 500
                    && options.Temperature == 0.2f
                    && HasReasoningEffort(options, "low")
                    && options.ResponseFormat != null),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                """
                {
                  "annotations": [
                    {
                      "comparisonIndex": 0,
                      "tags": [" spelling ", "SPELLING", "word_choice", "extra_word"],
                      "studentPhrase": "  Listen for the exact word form.  "
                    },
                    {
                      "comparisonIndex": 0,
                      "tags": ["word_choice"],
                      "studentPhrase": "Duplicate comparison."
                    }
                  ]
                }
                """))));
        var classifier = CreateClassifier(chatClient);

        var classificationRun = await classifier.ClassifyWithDiagnosticsAsync(
            new MistakePatternClassificationRequest(
                "The color changed",
                "The colour changed",
                [
                    new TextComparison(
                        new TextRange(4, 8),
                        "color",
                        new TextRange(4, 9),
                        "colour",
                        sourceComparisonIndex: 2)
                ]),
            CancellationToken.None);
        var annotations = classificationRun.Annotations;
        var annotation = annotations.Single();
        annotation.ComparisonIndex.ShouldBe(0);
        annotation.SourceComparisonIndex.ShouldBe(2);
        annotation.Tags.ShouldBe(["spelling", "word_choice", "extra_word"]);
        annotation.StudentPhrase.ShouldBe("Listen for the exact word form.");
    }

    [Fact]
    public async Task ClassifyWithDiagnosticsAsync_ShouldBatchComparisonsAndKeepGlobalComparisonIndexes()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new ChatResponse(new ChatMessage(
                    ChatRole.Assistant,
                    """
                    {
                      "annotations": [
                        {
                          "comparisonIndex": 0,
                          "tags": ["word_choice"],
                          "studentPhrase": "Listen for the exact first word."
                        },
                        {
                          "comparisonIndex": 1,
                          "tags": ["verb_form"],
                          "studentPhrase": "The verb ending changes the tense."
                        }
                      ]
                    }
                    """))),
                Task.FromResult(new ChatResponse(new ChatMessage(
                    ChatRole.Assistant,
                    """
                    {
                      "annotations": [
                        {
                          "comparisonIndex": 2,
                          "tags": ["article"],
                          "studentPhrase": "Articles can be short, but they still change the phrase."
                        }
                      ]
                    }
                    """))));
        var classifier = CreateClassifier(chatClient, maxComparisonsPerRequest: 2);

        var classificationRun = await classifier.ClassifyWithDiagnosticsAsync(
            new MistakePatternClassificationRequest(
                "red caused the issue",
                "read cause they issue",
                [
                    CreateComparison(0, "red", 0, "read", 10),
                    CreateComparison(4, "caused", 5, "cause", 11),
                    CreateComparison(11, "the", 11, "they", 12)
                ]),
            CancellationToken.None);
        var annotations = classificationRun.Annotations;
        annotations.Select(annotation => annotation.ComparisonIndex).ShouldBe([0, 1, 2]);
        annotations.Select(annotation => annotation.SourceComparisonIndex).ShouldBe([10, 11, 12]);
        classificationRun.Requests.Count.ShouldBe(2);
        await chatClient
            .Received(2)
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClassifyWithDiagnosticsAsync_ShouldUseFallbackAnnotationWhenAiOutputIsExtreme()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                $$"""
                {
                  "annotations": [
                    {
                      "comparisonIndex": 0,
                      "tags": ["{{new string('x', 101)}}"],
                      "studentPhrase": "{{new string('y', 2501)}}"
                    }
                  ]
                }
                """))));
        var classifier = CreateClassifier(chatClient);

        var classificationRun = await classifier.ClassifyWithDiagnosticsAsync(
            new MistakePatternClassificationRequest(
                "The color changed",
                "The colour changed",
                [
                    new TextComparison(
                        new TextRange(4, 8),
                        "color",
                        new TextRange(4, 9),
                        "colour",
                        sourceComparisonIndex: 2)
                ]),
            CancellationToken.None);
        var annotations = classificationRun.Annotations;
        var annotation = annotations.Single();
        annotation.ComparisonIndex.ShouldBe(0);
        annotation.SourceComparisonIndex.ShouldBe(2);
        annotation.Tags.ShouldBe(["uncategorized"]);
        annotation.StudentPhrase.ShouldBe(
            "Review this correction carefully and compare the word form, meaning, and surrounding words.");
    }

    [Fact]
    public async Task ClassifyWithDiagnosticsAsync_ShouldKeepValidAnnotationsWhenOneAnnotationIsMalformed()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                """
                {
                  "annotations": [
                    {
                      "comparisonIndex": 0,
                      "tags": ["word_choice"],
                      "studentPhrase": "This word changes the sentence meaning."
                    },
                    "not an annotation object",
                    {
                      "comparisonIndex": "1",
                      "tags": ["verb_form", 123],
                      "studentPhrase": "The verb form changes the tense."
                    },
                    {
                      "comparisonIndex": { "bad": true },
                      "tags": ["spelling"],
                      "studentPhrase": "This malformed annotation should be ignored."
                    }
                  ]
                }
                """))));
        var classifier = CreateClassifier(chatClient);

        var classificationRun = await classifier.ClassifyWithDiagnosticsAsync(
            new MistakePatternClassificationRequest(
                "red caused the issue",
                "read cause they issue",
                [
                    CreateComparison(0, "red", 0, "read", 10),
                    CreateComparison(4, "caused", 5, "cause", 11),
                    CreateComparison(11, "the", 11, "they", 12)
                ]),
            CancellationToken.None);
        var annotations = classificationRun.Annotations;
        annotations.Count.ShouldBe(3);
        annotations[0].ComparisonIndex.ShouldBe(0);
        annotations[0].SourceComparisonIndex.ShouldBe(10);
        annotations[0].Tags.ShouldBe(["word_choice"]);
        annotations[0].StudentPhrase.ShouldBe("This word changes the sentence meaning.");
        annotations[1].ComparisonIndex.ShouldBe(1);
        annotations[1].SourceComparisonIndex.ShouldBe(11);
        annotations[1].Tags.ShouldBe(["verb_form"]);
        annotations[1].StudentPhrase.ShouldBe("The verb form changes the tense.");
        annotations[2].ComparisonIndex.ShouldBe(2);
        annotations[2].SourceComparisonIndex.ShouldBe(12);
        annotations[2].Tags.ShouldBe(["uncategorized"]);
    }

    private static OpenAiMistakePatternClassifier CreateClassifier(
        IChatClient chatClient,
        int maxComparisonsPerRequest = 10) =>
        new(
            chatClient,
            Options.Create(new MistakePatternClassificationOptions
            {
                Enabled = true,
                Model = "test-model",
                MaxOutputTokens = 500,
                MaxComparisonsPerRequest = maxComparisonsPerRequest,
                Temperature = 0.2f,
                ReasoningEffort = "low"
            }),
            NullLogger<OpenAiMistakePatternClassifier>.Instance);

    private static bool HasReasoningEffort(
        ChatOptions options,
        string expectedReasoningEffort) =>
        options.AdditionalProperties is not null
        && options.AdditionalProperties.TryGetValue(
            "reasoning_effort",
            out var reasoningEffort)
        && (string?)reasoningEffort == expectedReasoningEffort;

    private static TextComparison CreateComparison(
        int originalStart,
        string originalText,
        int userStart,
        string userText,
        int sourceComparisonIndex) =>
        new(
            new TextRange(originalStart, originalStart + originalText.Length - 1),
            originalText,
            new TextRange(userStart, userStart + userText.Length - 1),
            userText,
            sourceComparisonIndex: sourceComparisonIndex);
}
