namespace WriteFluency.TextComparisons;

public sealed class AiRefinementOutputValidator
{
    private readonly AiRefinementRangeValidator _rangeValidator = new();

    public AiRefinementDecisionValidationResult ValidateDecisions(
        AiRefinementRequest request,
        IReadOnlyList<AiRefinementDecision>? decisions)
    {
        if (decisions is null)
        {
            return AiRefinementDecisionValidationResult.Failure(
                AiRefinementValidationErrors.MissingDecisions);
        }

        var sources = request.Comparisons.ToDictionary(
            comparison => comparison.SourceComparisonIndex);
        if (decisions.Any(decision =>
                !sources.ContainsKey(decision.SourceComparisonIndex)))
        {
            return AiRefinementDecisionValidationResult.Failure(
                AiRefinementValidationErrors.UnknownSourceComparison);
        }

        var result = new DecisionValidationAccumulator(sources.Count);
        foreach (var source in request.Comparisons)
        {
            ValidateSourceDecision(
                request,
                source,
                decisions,
                result);
        }

        if (result.HasOnlyRejectedDecisions)
        {
            return AiRefinementDecisionValidationResult.Failure(
                result.FirstFailureReason!);
        }

        return new AiRefinementDecisionValidationResult(
            true,
            OrderComparisons(result.OutputComparisons),
            result.Decisions,
            result.FirstFailureReason,
            result.RejectedSources);
    }

    public AiRefinementValidationResult Validate(
        AiRefinementRequest request,
        IReadOnlyList<AiRefinedComparison>? refinedComparisons) =>
        _rangeValidator.Validate(request, refinedComparisons);

    private void ValidateSourceDecision(
        AiRefinementRequest request,
        AiRefinementSourceComparison source,
        IReadOnlyList<AiRefinementDecision> decisions,
        DecisionValidationAccumulator result)
    {
        var sourceDecisions = decisions
            .Where(decision =>
                decision.SourceComparisonIndex
                == source.SourceComparisonIndex)
            .ToList();

        if (sourceDecisions.Count != 1)
        {
            result.Reject(
                source,
                sourceDecisions.FirstOrDefault(),
                sourceDecisions.Count == 0
                    ? AiRefinementValidationErrors.MissingSourceDecision
                    : AiRefinementValidationErrors.DuplicateSourceDecision);
            return;
        }

        var decision = sourceDecisions[0];
        var action = decision.Action.Trim().ToLowerInvariant();

        switch (action)
        {
            case AiRefinementActions.Keep
                when decision.Comparisons.Count == 0:
                result.AcceptKeep(source, decision);
                return;

            case AiRefinementActions.Remove
                when decision.Comparisons.Count == 0:
                result.AcceptRemove(source, decision);
                return;

            case AiRefinementActions.Refine
                when decision.Comparisons.Count == 1:
                ValidateRefinement(
                    request,
                    source,
                    decision,
                    result);
                return;

            default:
                result.Reject(
                    source,
                    decision,
                    IsKnownAction(action)
                        ? AiRefinementValidationErrors.InvalidActionRanges
                        : AiRefinementValidationErrors.InvalidAction);
                return;
        }
    }

    private void ValidateRefinement(
        AiRefinementRequest request,
        AiRefinementSourceComparison source,
        AiRefinementDecision decision,
        DecisionValidationAccumulator result)
    {
        var sourceRequest = request with { Comparisons = [source] };
        var validation = _rangeValidator.Validate(
            sourceRequest,
            decision.Comparisons);

        if (!validation.IsValid
            || validation.RejectedSourceComparisonCount > 0)
        {
            result.Reject(
                source,
                decision,
                validation.FailureReason
                ?? AiRefinementValidationErrors.InvalidRefinement);
            return;
        }

        var isEffectiveChange = !MatchesSource(
            source,
            validation.Comparisons);
        var refined = validation.Comparisons
            .Select(comparison => ApplyProvenance(
                comparison,
                source.SourceComparisonIndex,
                isEffectiveChange))
            .ToList();

        result.AcceptRefinement(
            source,
            decision,
            refined,
            isEffectiveChange);
    }

    private static IReadOnlyList<TextComparison> OrderComparisons(
        IEnumerable<TextComparison> comparisons) =>
        comparisons
            .OrderBy(comparison =>
                comparison.OriginalTextRange.InitialIndex)
            .ThenBy(comparison =>
                comparison.UserTextRange.InitialIndex)
            .ToList();

    private static TextComparison ApplyProvenance(
        TextComparison comparison,
        int sourceComparisonIndex,
        bool isAiRefined)
    {
        comparison.SourceComparisonIndex = sourceComparisonIndex;
        comparison.IsAiRefined = isAiRefined;
        return comparison;
    }

    private static bool MatchesSource(
        AiRefinementSourceComparison source,
        IReadOnlyList<TextComparison> comparisons) =>
        comparisons.Count == 1
        && comparisons[0].OriginalTextRange == source.OriginalTextRange
        && comparisons[0].UserTextRange == source.UserTextRange;

    private static bool IsKnownAction(string action) =>
        action is AiRefinementActions.Keep
            or AiRefinementActions.Remove
            or AiRefinementActions.Refine;

    private sealed class DecisionValidationAccumulator
    {
        private int _acceptedDecisionCount;

        public DecisionValidationAccumulator(int capacity)
        {
            Decisions = new List<AiRefinementDecisionValidation>(capacity);
            RejectedSources = new List<AiRefinementValidationIssue>();
            OutputComparisons = new List<TextComparison>();
        }

        public List<AiRefinementDecisionValidation> Decisions { get; }
        public List<AiRefinementValidationIssue> RejectedSources { get; }
        public List<TextComparison> OutputComparisons { get; }

        public bool HasOnlyRejectedDecisions =>
            RejectedSources.Count > 0 && _acceptedDecisionCount == 0;

        public string? FirstFailureReason =>
            RejectedSources.FirstOrDefault()?.Reason;

        public void AcceptKeep(
            AiRefinementSourceComparison source,
            AiRefinementDecision decision)
        {
            var sourceComparison =
                AiRefinementComparisonFactory.CreateSource(source);
            OutputComparisons.Add(sourceComparison);
            Decisions.Add(CreateAcceptedDecision(
                source,
                decision,
                [sourceComparison]));
            _acceptedDecisionCount++;
        }

        public void AcceptRemove(
            AiRefinementSourceComparison source,
            AiRefinementDecision decision)
        {
            Decisions.Add(CreateAcceptedDecision(
                source,
                decision,
                [],
                isEffectiveChange: true));
            _acceptedDecisionCount++;
        }

        public void AcceptRefinement(
            AiRefinementSourceComparison source,
            AiRefinementDecision decision,
            IReadOnlyList<TextComparison> refined,
            bool isEffectiveChange)
        {
            OutputComparisons.AddRange(refined);
            Decisions.Add(CreateAcceptedDecision(
                source,
                decision,
                refined,
                isEffectiveChange));
            _acceptedDecisionCount++;
        }

        public void Reject(
            AiRefinementSourceComparison source,
            AiRefinementDecision? decision,
            string reason)
        {
            var fallback =
                AiRefinementComparisonFactory.CreateSource(source);
            OutputComparisons.Add(fallback);
            RejectedSources.Add(new AiRefinementValidationIssue(
                source.SourceComparisonIndex,
                reason));
            Decisions.Add(new AiRefinementDecisionValidation(
                source.SourceComparisonIndex,
                decision?.Action ?? AiRefinementActions.Keep,
                decision?.ReasonCode ?? "validation_fallback",
                decision?.Comparisons ?? [],
                [fallback],
                "rejected",
                reason));
        }

        private static AiRefinementDecisionValidation CreateAcceptedDecision(
            AiRefinementSourceComparison source,
            AiRefinementDecision decision,
            IReadOnlyList<TextComparison> output,
            bool isEffectiveChange = false) =>
            new(
                source.SourceComparisonIndex,
                decision.Action.Trim().ToLowerInvariant(),
                decision.ReasonCode,
                decision.Comparisons,
                output,
                "accepted",
                null)
            {
                IsEffectiveChange = isEffectiveChange
            };
    }
}
