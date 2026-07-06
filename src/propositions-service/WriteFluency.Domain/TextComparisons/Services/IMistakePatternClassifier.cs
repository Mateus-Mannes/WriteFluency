namespace WriteFluency.TextComparisons;

public interface IMistakePatternClassifier
{
    bool IsEnabled { get; }

    Task<MistakePatternClassificationRun> ClassifyWithDiagnosticsAsync(
        MistakePatternClassificationRequest request,
        CancellationToken cancellationToken);
}
