namespace WriteFluency.UsersProgressService.Progress;

public sealed class UserProgressRecord
{
    public string Id { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public int ExerciseId { get; set; }

    public string Status { get; set; } = ProgressStatus.InProgress;

    public string? ExerciseTitle { get; set; }

    public string? Subject { get; set; }

    public string? Complexity { get; set; }

    public int AttemptCount { get; set; }

    public double? LatestAccuracyPercentage { get; set; }

    public double? BestAccuracyPercentage { get; set; }

    public int? CurrentWordCount { get; set; }

    public int? OriginalWordCount { get; set; }

    public int TotalActiveSeconds { get; set; }

    public int CurrentAttemptActiveSeconds { get; set; }

    public DateTimeOffset? LastInteractionAtUtc { get; set; }

    public string? LastAttemptId { get; set; }

    public string? DraftExerciseState { get; set; }

    public string? DraftUserText { get; set; }

    public int? DraftAutoPauseSeconds { get; set; }

    public double? DraftPausedTimeSeconds { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class UserAttemptRecord
{
    public string Id { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public int ExerciseId { get; set; }

    public double? AccuracyPercentage { get; set; }

    public int? WordCount { get; set; }

    public int? OriginalWordCount { get; set; }

    public int ActiveSeconds { get; set; }

    public string? ExerciseTitle { get; set; }

    public string? Subject { get; set; }

    public string? Complexity { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
