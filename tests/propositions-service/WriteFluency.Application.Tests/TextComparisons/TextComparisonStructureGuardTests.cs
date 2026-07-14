using Shouldly;
using WriteFluency.TextComparisons;

namespace WriteFluency.Application.Tests.TextComparisons;

public sealed class TextComparisonStructureGuardTests
{
    [Fact]
    public void EnsureValid_WithValidOrderedNonOverlappingComparisons_ShouldNotThrow()
    {
        var result = CreateResult([
            CreateComparison(0, 2, "one", 0, 2, "won"),
            CreateComparison(8, 10, "two", 8, 10, "too")
        ]);

        Should.NotThrow(() => TextComparisonStructureGuard.EnsureValid(result));
    }

    [Fact]
    public void EnsureValid_WithNoComparisons_ShouldNotThrow()
    {
        var result = CreateResult([]);

        Should.NotThrow(() => TextComparisonStructureGuard.EnsureValid(result));
    }

    [Theory]
    [InlineData(-1, 2, "original range initial index is negative")]
    [InlineData(3, 2, "original range final index is before initial index")]
    [InlineData(0, 99, "original range is out of bounds")]
    public void EnsureValid_WithInvalidOriginalRange_ShouldThrow(
        int initialIndex,
        int finalIndex,
        string expectedReason)
    {
        var result = CreateResult([
            CreateComparison(initialIndex, finalIndex, "one", 0, 2, "won")
        ]);

        var exception = Should.Throw<InvalidOperationException>(() =>
            TextComparisonStructureGuard.EnsureValid(result));
        exception.Message.ShouldContain(expectedReason);
    }

    [Fact]
    public void EnsureValid_WithInvalidUserRange_ShouldThrow()
    {
        var result = CreateResult([
            CreateComparison(0, 2, "one", 0, 99, "won")
        ]);

        var exception = Should.Throw<InvalidOperationException>(() =>
            TextComparisonStructureGuard.EnsureValid(result));
        exception.Message.ShouldContain("user range is out of bounds");
    }

    [Fact]
    public void EnsureValid_WithStaleOriginalSnippet_ShouldThrow()
    {
        var result = CreateResult([
            CreateComparison(0, 2, "two", 0, 2, "won")
        ]);

        var exception = Should.Throw<InvalidOperationException>(() =>
            TextComparisonStructureGuard.EnsureValid(result));
        exception.Message.ShouldContain("original selected text does not match its range");
    }

    [Fact]
    public void EnsureValid_WithStaleUserSnippet_ShouldThrow()
    {
        var result = CreateResult([
            CreateComparison(0, 2, "one", 0, 2, "too")
        ]);

        var exception = Should.Throw<InvalidOperationException>(() =>
            TextComparisonStructureGuard.EnsureValid(result));
        exception.Message.ShouldContain("user selected text does not match its range");
    }

    [Fact]
    public void EnsureValid_WithOverlappingOriginalRanges_ShouldThrow()
    {
        var result = CreateResult([
            CreateComparison(0, 2, "one", 0, 2, "won"),
            CreateComparison(2, 4, "e a", 8, 10, "too")
        ]);

        var exception = Should.Throw<InvalidOperationException>(() =>
            TextComparisonStructureGuard.EnsureValid(result));
        exception.Message.ShouldContain("original ranges must be sorted and non-overlapping");
    }

    [Fact]
    public void EnsureValid_WithNonMonotonicUserRanges_ShouldThrow()
    {
        var result = CreateResult([
            CreateComparison(0, 2, "one", 8, 10, "too"),
            CreateComparison(8, 10, "two", 0, 2, "won")
        ]);

        var exception = Should.Throw<InvalidOperationException>(() =>
            TextComparisonStructureGuard.EnsureValid(result));
        exception.Message.ShouldContain("user ranges must be monotonic and non-overlapping");
    }

    [Fact]
    public void EnsureValidSourceIndexes_WithNonNegativeIndexes_ShouldNotThrow()
    {
        var comparisons = new List<TextComparison>
        {
            CreateComparison(0, 2, "one", 0, 2, "won", sourceComparisonIndex: 0),
            CreateComparison(8, 10, "two", 8, 10, "too", sourceComparisonIndex: 0)
        };

        Should.NotThrow(() =>
            TextComparisonStructureGuard.EnsureValidSourceIndexes(comparisons));
    }

    [Fact]
    public void EnsureValidSourceIndexes_WithNegativeIndex_ShouldThrow()
    {
        var comparisons = new List<TextComparison>
        {
            CreateComparison(0, 2, "one", 0, 2, "won", sourceComparisonIndex: -1)
        };

        var exception = Should.Throw<InvalidOperationException>(() =>
            TextComparisonStructureGuard.EnsureValidSourceIndexes(comparisons));
        exception.Message.ShouldContain("source comparison index is negative");
    }

    private static TextComparisonResult CreateResult(
        List<TextComparison> comparisons) =>
        new(
            "one and two",
            "won and too",
            0.5,
            comparisons);

    private static TextComparison CreateComparison(
        int originalStart,
        int originalEnd,
        string originalText,
        int userStart,
        int userEnd,
        string userText,
        int sourceComparisonIndex = 0) =>
        new(
            new TextRange(originalStart, originalEnd),
            originalText,
            new TextRange(userStart, userEnd),
            userText,
            sourceComparisonIndex: sourceComparisonIndex);
}
