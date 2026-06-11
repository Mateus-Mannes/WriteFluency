using System.Globalization;
using System.Text.Json;
using Shouldly;
using WriteFluency.TextComparisons;

namespace WriteFluency.Application.Tests.TextComparisons;

public class DeterministicTextEquivalenceServiceTests
{
    private readonly DeterministicTextEquivalenceService _service =
        new(new EnglishNumberNormalizer());

    public static IEnumerable<object[]> RegressionCases()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TextComparisons",
            "Fixtures",
            "deterministic-equivalence-cases.json");

        var cases = JsonSerializer.Deserialize<List<EquivalenceRegressionCase>>(
            File.ReadAllText(fixturePath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return cases!
            .Select(testCase => new object[] { testCase });
    }

    [Theory]
    [MemberData(nameof(RegressionCases))]
    public void AreEquivalent_ShouldMatchSanitizedRegressionExpectation(
        EquivalenceRegressionCase testCase)
    {
        var result = _service.AreEquivalent(testCase.OriginalText, testCase.UserText);

        result.ShouldBe(
            testCase.ExpectedEquivalent,
            $"Regression case '{testCase.CaseId}' ({testCase.Category}) failed.");
    }

    [Fact]
    public void AreEquivalent_ShouldBeCultureInvariant()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = new CultureInfo("tr-TR");

            _service.AreEquivalent("THIS IS FIRST", "this is 1st").ShouldBeTrue();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "text")]
    [InlineData("text", null)]
    public void AreEquivalent_WithNullSnippet_ShouldReturnFalse(
        string? originalText,
        string? userText)
    {
        _service.AreEquivalent(originalText, userText).ShouldBeFalse();
    }

    public sealed record EquivalenceRegressionCase(
        string CaseId,
        string Category,
        string OriginalText,
        string UserText,
        bool ExpectedEquivalent,
        string? FutureDisposition);
}
