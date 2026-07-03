using System.Text.Json;
using Shouldly;
using WriteFluency.MistakePatternClassification.Evals;

namespace WriteFluency.MistakePatternClassification.Evals.Tests;

public sealed class EvaluationFixtureValidatorTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Validate_ShouldAcceptBundledFixture()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "examples2.json");
        var sourceCases = JsonSerializer.Deserialize<List<SourceEvaluationCase>>(
            await File.ReadAllTextAsync(path),
            JsonOptions);
        var cases = sourceCases!
            .Select(sourceCase => sourceCase.ToEvaluationCase())
            .ToList();

        Should.NotThrow(() => EvaluationFixtureValidator.Validate(cases));
    }

    [Fact]
    public void Validate_ShouldRejectRangeTextMismatch()
    {
        var cases = new List<EvaluationCase>
        {
            CreateCase(comparison => comparison with
            {
                OriginalText = "wrong"
            })
        };

        Should.Throw<InvalidOperationException>(
                () => EvaluationFixtureValidator.Validate(cases))
            .Message.ShouldContain("range text mismatch");
    }

    [Fact]
    public void Validate_ShouldRejectDuplicateComparisonIndexes()
    {
        var validCase = CreateCase();
        validCase.Comparisons.Add(CreateComparison());

        Should.Throw<InvalidOperationException>(
                () => EvaluationFixtureValidator.Validate([validCase]))
            .Message.ShouldContain("duplicate comparison index");
    }

    [Fact]
    public void Validate_ShouldRejectNonNormalizedTags()
    {
        var cases = new List<EvaluationCase>
        {
            CreateCase(comparison => comparison with
            {
                ExpectedTags = ["Word Choice"]
            })
        };

        Should.Throw<InvalidOperationException>(
                () => EvaluationFixtureValidator.Validate(cases))
            .Message.ShouldContain("non-normalized tag");
    }

    [Fact]
    public void Validate_ShouldRejectMissingReferenceStudentPhrase()
    {
        var cases = new List<EvaluationCase>
        {
            CreateCase(comparison => comparison with
            {
                ReferenceStudentPhrase = ""
            })
        };

        Should.Throw<InvalidOperationException>(
                () => EvaluationFixtureValidator.Validate(cases))
            .Message.ShouldContain("reference student phrase");
    }

    private static EvaluationCase CreateCase(
        Func<EvaluationComparison, EvaluationComparison>? transform = null)
    {
        var comparison = transform?.Invoke(CreateComparison()) ?? CreateComparison();
        return new EvaluationCase
        {
            CaseId = "case",
            Category = "category",
            OriginalText = "She sews fabric.",
            UserText = "She saws fabric.",
            Comparisons = [comparison]
        };
    }

    private static EvaluationComparison CreateComparison() =>
        new()
        {
            ComparisonIndex = 0,
            SourceComparisonIndex = 0,
            OriginalTextRange = new EvaluationTextRange(4, 7),
            OriginalText = "sews",
            UserTextRange = new EvaluationTextRange(4, 7),
            UserText = "saws",
            ExpectedTags = ["word_choice"],
            ReferenceStudentPhrase = "\"Sews\" means stitching fabric; \"saws\" means cutting with a tool."
        };
}
