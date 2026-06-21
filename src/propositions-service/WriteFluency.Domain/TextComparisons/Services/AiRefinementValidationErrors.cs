namespace WriteFluency.TextComparisons;

internal static class AiRefinementValidationErrors
{
    public const string MissingDecisions = "missing_decisions";
    public const string MissingComparisons = "missing_comparisons";
    public const string UnknownSourceComparison = "unknown_source_comparison";
    public const string MissingSourceDecision = "missing_source_decision";
    public const string DuplicateSourceDecision = "duplicate_source_decision";
    public const string InvalidActionRanges = "invalid_action_ranges";
    public const string InvalidAction = "invalid_action";
    public const string InvalidRefinement = "invalid_refinement";
    public const string InvalidRange = "invalid_range";
    public const string RangeOutsideSource = "range_outside_source";
    public const string CrossingRanges = "crossing_ranges";
    public const string EmptyRangeAfterNormalization =
        "empty_range_after_normalization";
    public const string UnsafeTextSlice = "unsafe_text_slice";
    public const string PartialWordRange = "partial_word_range";
    public const string IdenticalSelectedText = "identical_selected_text";
}
