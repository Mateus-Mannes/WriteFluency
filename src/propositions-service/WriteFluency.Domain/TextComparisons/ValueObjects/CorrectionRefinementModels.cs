namespace WriteFluency.TextComparisons;

public sealed record CorrectionComparisonRange(
    int SourceComparisonIndex,
    int OriginalTextInitialIndex,
    int OriginalTextFinalIndex,
    int UserTextInitialIndex,
    int UserTextFinalIndex);

public static class CorrectionRefinementActions
{
    public const string Keep = "keep";
    public const string Remove = "remove";
    public const string Refine = "refine";
}
