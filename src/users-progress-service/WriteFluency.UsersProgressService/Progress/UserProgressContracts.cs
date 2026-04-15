namespace WriteFluency.UsersProgressService.Progress;

public sealed record StartProgressRequest(
    int ExerciseId,
    string? ExerciseTitle,
    string? Subject,
    string? Complexity);

public sealed record CompleteProgressRequest(
    int ExerciseId,
    double? AccuracyPercentage,
    int? WordCount,
    int? OriginalWordCount,
    string? ExerciseTitle,
    string? Subject,
    string? Complexity);

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
    string? Complexity);

public sealed record ProgressStateResponse(
    bool TrackingEnabled,
    int ExerciseId,
    bool HasServerState,
    string? ExerciseState,
    string? UserText,
    int? WordCount,
    int? AutoPauseSeconds,
    double? PausedTimeSeconds,
    DateTimeOffset? UpdatedAtUtc);

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
    int? CurrentWordCount = null);

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
