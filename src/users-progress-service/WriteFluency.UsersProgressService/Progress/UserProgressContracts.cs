namespace WriteFluency.UsersProgressService.Progress;

public sealed record StartProgressRequest(
    int ExerciseId,
    string? ExerciseTitle,
    string? Subject,
    string? Complexity,
    int? OriginalWordCount = null,
    bool ResetCompletedState = false);

public sealed record CompleteProgressRequest(
    int ExerciseId,
    double? AccuracyPercentage,
    int? WordCount,
    int? OriginalWordCount,
    string? ExerciseTitle,
    string? Subject,
    string? Complexity,
    string? UserText = null,
    string? OriginalText = null,
    IReadOnlyList<ProgressTextComparison>? Comparisons = null,
    string? CorrectionMode = null,
    IReadOnlyList<ProgressCorrectionTraceEntry>? CorrectionTrace = null);

public sealed record ProgressOperationResponse(
    bool TrackingEnabled,
    int ExerciseId,
    string Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record SaveProgressStateRequest(
    int ExerciseId,
    string? ExerciseState,
    string? UserText,
    int? WordCount,
    int? AutoPauseSeconds,
    double? PausedTimeSeconds,
    string? ExerciseTitle,
    string? Subject,
    string? Complexity,
    int? OriginalWordCount = null);

public sealed record ProgressStateResponse(
    bool TrackingEnabled,
    int ExerciseId,
    bool HasServerState,
    string? ExerciseState,
    string? UserText,
    int? WordCount,
    int? AutoPauseSeconds,
    double? PausedTimeSeconds,
    DateTimeOffset? UpdatedAtUtc,
    double? AccuracyPercentage = null,
    string? OriginalText = null,
    IReadOnlyList<ProgressTextComparison>? Comparisons = null,
    string? CorrectionMode = null,
    IReadOnlyList<ProgressCorrectionTraceEntry>? CorrectionTrace = null);

public sealed record ProgressTextComparison(
    ProgressTextRange? OriginalTextRange,
    string? OriginalText,
    ProgressTextRange? UserTextRange,
    string? UserText,
    int? SourceComparisonIndex = null,
    bool IsDeterministicallyRefined = false);

public sealed record ProgressTextRange(
    int? InitialIndex,
    int? FinalIndex);

public sealed record ProgressComparisonSnapshot(
    ProgressTextRange? OriginalTextRange,
    string? OriginalText,
    ProgressTextRange? UserTextRange,
    string? UserText);

public sealed record ProgressCorrectionStageTrace(
    string? Action,
    string? ReasonCode,
    IReadOnlyList<ProgressComparisonSnapshot>? Output,
    string? ValidationStatus = null,
    IReadOnlyList<ProgressComparisonSnapshot>? ProposedOutput = null,
    string? ValidationFailureReason = null);

public sealed record ProgressCorrectionTraceEntry(
    int? SourceComparisonIndex,
    ProgressComparisonSnapshot? Initial,
    ProgressCorrectionStageTrace? Deterministic = null);

public sealed record ProgressSummaryResponse(
    bool TrackingEnabled,
    int TotalItems,
    int InProgressCount,
    int CompletedCount,
    int TotalAttempts,
    int TotalActiveSeconds,
    double? AverageAccuracyPercentage,
    double? BestAccuracyPercentage,
    DateTimeOffset? LastActivityAtUtc);

public sealed record ProgressItemResponse(
    int ExerciseId,
    string Status,
    string? ExerciseTitle,
    string? Subject,
    string? Complexity,
    int AttemptCount,
    double? LatestAccuracyPercentage,
    double? BestAccuracyPercentage,
    int ActiveSeconds,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int? CurrentWordCount = null,
    int? OriginalWordCount = null);

public sealed record ProgressAttemptResponse(
    string AttemptId,
    int ExerciseId,
    double? AccuracyPercentage,
    int? WordCount,
    int? OriginalWordCount,
    int ActiveSeconds,
    DateTimeOffset CreatedAtUtc,
    string? ExerciseTitle,
    string? Subject,
    string? Complexity);
