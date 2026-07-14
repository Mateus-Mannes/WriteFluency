namespace WriteFluency.TextComparisons;

public sealed record MistakePatternClassificationRun(
    IReadOnlyList<MistakePatternAnnotation> Annotations,
    IReadOnlyList<MistakePatternClassificationRequestMetrics> Requests)
{
    public long? InputTokenCount => SumNullableTokens(request => request.InputTokenCount);

    public long? OutputTokenCount => SumNullableTokens(request => request.OutputTokenCount);

    public long? TotalTokenCount => SumNullableTokens(request => request.TotalTokenCount);

    private long? SumNullableTokens(
        Func<MistakePatternClassificationRequestMetrics, long?> selector)
    {
        var values = Requests
            .Select(selector)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return values.Length == 0 ? null : values.Sum();
    }
}

public sealed record MistakePatternClassificationRequestMetrics(
    int BatchNumber,
    int StartIndex,
    int ComparisonCount,
    long DurationMilliseconds,
    long? InputTokenCount,
    long? OutputTokenCount,
    long? TotalTokenCount);
