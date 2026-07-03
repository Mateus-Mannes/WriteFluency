using WriteFluency.TextComparisons;

namespace WriteFluency.Infrastructure.TextComparisons;

public sealed record MistakePatternClassificationRun(
    IReadOnlyList<MistakePatternAnnotation> Annotations,
    IReadOnlyList<MistakePatternClassificationRequestMetrics> Requests);

public sealed record MistakePatternClassificationRequestMetrics(
    int BatchNumber,
    int StartIndex,
    int ComparisonCount,
    long DurationMilliseconds,
    long? InputTokenCount,
    long? OutputTokenCount,
    long? TotalTokenCount);
