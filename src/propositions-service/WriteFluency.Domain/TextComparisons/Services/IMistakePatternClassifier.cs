namespace WriteFluency.TextComparisons;

public interface IMistakePatternClassifier
{
    Task<IReadOnlyList<MistakePatternAnnotation>> ClassifyAsync(
        MistakePatternClassificationRequest request,
        CancellationToken cancellationToken);
}
