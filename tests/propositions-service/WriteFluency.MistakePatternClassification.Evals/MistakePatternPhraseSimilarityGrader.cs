using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace WriteFluency.MistakePatternClassification.Evals;

public sealed class MistakePatternPhraseSimilarityGrader
{
    private const double PassingScore = 0.80;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IChatClient _chatClient;
    private readonly string _model;
    private readonly float _temperature;
    private readonly int _maxComparisonsPerRequest;

    public MistakePatternPhraseSimilarityGrader(
        IChatClient chatClient,
        string model,
        float temperature,
        int maxComparisonsPerRequest)
    {
        _chatClient = chatClient;
        _model = model;
        _temperature = temperature;
        _maxComparisonsPerRequest = Math.Max(1, maxComparisonsPerRequest);
    }

    public async Task<EvaluationPhraseSimilarityRun> GradeAsync(
        EvaluationCase evaluationCase,
        IReadOnlyList<EvaluationComparisonResult> comparisons,
        CancellationToken cancellationToken)
    {
        var grades = new List<EvaluationPhraseSimilarityGrade>();
        var requests = new List<EvaluationRequestResult>();
        var batchNumber = 0;

        for (var startIndex = 0; startIndex < comparisons.Count; startIndex += _maxComparisonsPerRequest)
        {
            batchNumber++;
            var batch = comparisons
                .Skip(startIndex)
                .Take(_maxComparisonsPerRequest)
                .ToArray();

            var stopwatch = Stopwatch.StartNew();
            var response = await _chatClient.GetResponseAsync<StructuredPhraseSimilarityResponse>(
                CreateMessages(evaluationCase, batch),
                JsonOptions,
                CreateChatOptions(),
                useJsonSchemaResponseFormat: true,
                cancellationToken);
            stopwatch.Stop();

            requests.Add(new EvaluationRequestResult(
                "phrase-grader",
                batchNumber,
                startIndex,
                batch.Length,
                stopwatch.ElapsedMilliseconds,
                response.Usage?.InputTokenCount,
                response.Usage?.OutputTokenCount,
                response.Usage?.TotalTokenCount));

            foreach (var grade in response.Result?.Grades ?? [])
            {
                var score = Math.Clamp(grade.Score, 0, 1);
                grades.Add(new EvaluationPhraseSimilarityGrade(
                    grade.ComparisonIndex,
                    score,
                    score >= PassingScore,
                    string.IsNullOrWhiteSpace(grade.Reason)
                        ? "No reason returned."
                        : grade.Reason.Trim()));
            }
        }

        return new EvaluationPhraseSimilarityRun(
            grades
                .GroupBy(grade => grade.ComparisonIndex)
                .Select(group => group.First())
                .OrderBy(grade => grade.ComparisonIndex)
                .ToArray(),
            requests);
    }

    private ChatMessage[] CreateMessages(
        EvaluationCase evaluationCase,
        IReadOnlyList<EvaluationComparisonResult> comparisons) =>
        [
            new(ChatRole.System, SystemPrompt()),
            new(ChatRole.User, UserPrompt(evaluationCase, comparisons))
        ];

    private ChatOptions CreateChatOptions() =>
        new()
        {
            MaxOutputTokens = 2500,
            Temperature = _temperature,
            ModelId = _model
        };

    private static string UserPrompt(
        EvaluationCase evaluationCase,
        IReadOnlyList<EvaluationComparisonResult> comparisons)
    {
        var promptComparisons = comparisons
            .Select(comparison => new PromptPhraseComparison(
                comparison.ComparisonIndex,
                comparison.OriginalText,
                comparison.UserText,
                comparison.OriginalContext.Text,
                comparison.UserContext.Text,
                comparison.ExpectedTags,
                comparison.ActualTags,
                comparison.ReferenceStudentPhrase,
                comparison.ActualPhrase ?? string.Empty))
            .ToArray();

        return JsonSerializer.Serialize(
            new
            {
                evaluationCase.CaseId,
                evaluationCase.Category,
                comparisons = promptComparisons
            },
            JsonOptions);
    }

    private static string SystemPrompt() =>
        """
        <role>
          You are grading whether two English-learning feedback phrases teach the same correction.
          The reference phrase is the expected educational meaning.
          The actual phrase is generated feedback shown to a student.
        </role>

        <task>
          For each comparison, return:
          - comparisonIndex
          - score from 0.0 to 1.0
          - one short reason explaining the score
        </task>

        <grading-rules>
          Grade meaning and teaching value, not exact wording.
          A good actual phrase can be longer, shorter, or worded differently than the reference.
          It should pass if it teaches the same main English issue using the same correction context.
          Penalize if it misses a major issue, teaches a different issue, names the wrong grammar point, or is too generic.
          Penalize if it only says the student wrote something different without explaining the lesson.
          Do not require all tags to match; tags are shown only as context.
          Use score >= 0.80 when the phrase should count as similar enough.
        </grading-rules>

        <score-guide>
          1.00: Same lesson and same correction context, with equal or better student value.
          0.90: Same main lesson, minor wording or detail differences.
          0.80: Same useful lesson, but less complete or missing a secondary detail.
          0.60: Related topic, but misses an important part of the expected lesson.
          0.40: Mentions the same words but teaches the wrong or too generic lesson.
          0.20: Mostly different correction or misleading explanation.
          0.00: Missing phrase, unrelated phrase, or unsafe/non-educational output.
        </score-guide>

        <good-examples>
          <example>
            reference: "\"Vaccines\" is plural; \"vaccine\" is singular."
            actual: "The original uses the plural \"vaccines\" because more than one vaccine is given, not the singular \"vaccine\"."
            score: 1.00
            reason: "The actual phrase teaches the same singular/plural distinction and adds helpful context."
          </example>
          <example>
            reference: "\"Will\" presents the result as expected in the future; \"would\" makes it sound more hypothetical or conditional."
            actual: "\"Will\" shows a real future result, while \"would\" makes the sentence sound conditional."
            score: 0.95
            reason: "The same modal meaning contrast is explained with slightly different wording."
          </example>
          <example>
            reference: "\"Embroidery\" means decorative sewing; \"brodery\" is not the correct spelling."
            actual: "\"Brodery\" is misspelled; the correct word is \"embroidery,\" which refers to decorative stitching."
            score: 0.95
            reason: "The spelling issue and meaning are both preserved."
          </example>
        </good-examples>

        <bad-examples>
          <example>
            reference: "\"Vaccines\" is plural; \"vaccine\" is singular."
            actual: "Listen carefully to the exact phrase."
            score: 0.20
            reason: "The actual phrase is generic and does not teach the plural issue."
          </example>
          <example>
            reference: "\"Meters\" measures distance, while \"minutes\" measures time, so the unit changes the meaning."
            actual: "The spelling is different here."
            score: 0.20
            reason: "The actual phrase gives the wrong category; the issue is unit meaning, not spelling."
          </example>
          <example>
            reference: "\"It is\" is a statement; \"is it\" uses question word order."
            actual: "\"It\" is singular."
            score: 0.30
            reason: "The actual phrase talks about number, but the expected lesson is word order."
          </example>
        </bad-examples>

        <reason-style>
          Give one concise reason.
          Mention whether the actual phrase preserves the main lesson, misses a secondary issue, is generic, or teaches the wrong issue.
          Do not restate the full phrases.
        </reason-style>

        <output>
          Return JSON only.
          Include exactly one grade for every supplied comparisonIndex.
        </output>
        """;

    private sealed record PromptPhraseComparison(
        int ComparisonIndex,
        string OriginalText,
        string UserText,
        string OriginalContext,
        string UserContext,
        IReadOnlyList<string> ExpectedTags,
        IReadOnlyList<string> ActualTags,
        string ReferenceStudentPhrase,
        string ActualStudentPhrase);

    private sealed class StructuredPhraseSimilarityResponse
    {
        [JsonPropertyName("grades")]
        public List<StructuredPhraseSimilarityGrade>? Grades { get; set; }
    }

    private sealed class StructuredPhraseSimilarityGrade
    {
        [JsonPropertyName("comparisonIndex")]
        public int ComparisonIndex { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}
