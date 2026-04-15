namespace WriteFluency.UsersProgressService.Progress;

public sealed class UserProgressTrackingService : IUserProgressTrackingService
{
    private const int MaxDraftUserTextLength = 20000;
    private const int MaxIdleActiveSeconds = 60;
    private static readonly HashSet<string> AllowedExerciseStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "intro",
        "exercise",
        "results"
    };

    private readonly IUserProgressRepository _repository;
    private readonly ILogger<UserProgressTrackingService> _logger;

    public UserProgressTrackingService(
        IUserProgressRepository repository,
        ILogger<UserProgressTrackingService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ProgressOperationResponse> StartAsync(
        string userId,
        StartProgressRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Start progress operation requested. UserId={UserId}, ExerciseId={ExerciseId}.",
            userId,
            request.ExerciseId);

        var now = DateTimeOffset.UtcNow;
        var current = await _repository.GetProgressAsync(userId, request.ExerciseId, cancellationToken);

        var progress = current ?? new UserProgressRecord
        {
            Id = CosmosUserProgressRepository.CreateProgressDocumentId(request.ExerciseId),
            UserId = userId,
            ExerciseId = request.ExerciseId,
            StartedAtUtc = now,
            UpdatedAtUtc = now,
            Status = ProgressStatus.InProgress
        };

        if (current is not null)
        {
            progress.StartedAtUtc = progress.StartedAtUtc == default ? now : progress.StartedAtUtc;
            if (!string.Equals(progress.Status, ProgressStatus.Completed, StringComparison.OrdinalIgnoreCase))
            {
                progress.Status = ProgressStatus.InProgress;
            }
            else
            {
                progress.CurrentAttemptActiveSeconds = 0;
                progress.LastInteractionAtUtc = now;
            }
        }

        AccrueActiveSeconds(progress, now);
        ApplyExerciseMetadata(progress, request.ExerciseTitle, request.Subject, request.Complexity);
        progress.UpdatedAtUtc = now;

        await _repository.UpsertProgressAsync(progress, cancellationToken);

        _logger.LogInformation(
            "Start progress operation completed. UserId={UserId}, ExerciseId={ExerciseId}, Status={Status}, AttemptCount={AttemptCount}, TotalActiveSeconds={TotalActiveSeconds}.",
            userId,
            request.ExerciseId,
            progress.Status,
            progress.AttemptCount,
            NormalizeActiveSeconds(progress.TotalActiveSeconds));

        return new ProgressOperationResponse(true, request.ExerciseId, progress.Status, now);
    }

    public async Task<ProgressOperationResponse> CompleteAsync(
        string userId,
        CompleteProgressRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Complete progress operation requested. UserId={UserId}, ExerciseId={ExerciseId}.",
            userId,
            request.ExerciseId);

        var now = DateTimeOffset.UtcNow;
        var current = await _repository.GetProgressAsync(userId, request.ExerciseId, cancellationToken);

        var accuracy = NormalizeAccuracyPercentage(request.AccuracyPercentage);
        var progress = current ?? new UserProgressRecord
        {
            Id = CosmosUserProgressRepository.CreateProgressDocumentId(request.ExerciseId),
            UserId = userId,
            ExerciseId = request.ExerciseId,
            StartedAtUtc = now
        };

        progress.StartedAtUtc = progress.StartedAtUtc == default ? now : progress.StartedAtUtc;
        AccrueActiveSeconds(progress, now);
        var attemptActiveSeconds = NormalizeActiveSeconds(progress.CurrentAttemptActiveSeconds);

        var attempt = new UserAttemptRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            ExerciseId = request.ExerciseId,
            AccuracyPercentage = accuracy,
            WordCount = request.WordCount,
            OriginalWordCount = request.OriginalWordCount,
            ActiveSeconds = attemptActiveSeconds,
            ExerciseTitle = request.ExerciseTitle,
            Subject = request.Subject,
            Complexity = request.Complexity,
            CreatedAtUtc = now
        };

        await _repository.AddAttemptAsync(attempt, cancellationToken);

        progress.Status = ProgressStatus.Completed;
        progress.CompletedAtUtc ??= now;
        progress.UpdatedAtUtc = now;
        progress.AttemptCount = Math.Max(progress.AttemptCount, 0) + 1;
        progress.LatestAccuracyPercentage = accuracy;
        progress.BestAccuracyPercentage = MergeBestAccuracy(progress.BestAccuracyPercentage, accuracy);
        progress.CurrentWordCount = NormalizeWordCount(request.WordCount) ?? progress.CurrentWordCount;
        progress.LastAttemptId = attempt.Id;
        progress.DraftExerciseState = null;
        progress.DraftUserText = null;
        progress.DraftAutoPauseSeconds = null;
        progress.DraftPausedTimeSeconds = null;

        ApplyExerciseMetadata(progress, request.ExerciseTitle, request.Subject, request.Complexity);

        await _repository.UpsertProgressAsync(progress, cancellationToken);

        _logger.LogInformation(
            "Complete progress operation completed. UserId={UserId}, ExerciseId={ExerciseId}, AttemptId={AttemptId}, AttemptCount={AttemptCount}, LatestAccuracyPercentage={LatestAccuracyPercentage}, TotalActiveSeconds={TotalActiveSeconds}.",
            userId,
            request.ExerciseId,
            attempt.Id,
            progress.AttemptCount,
            progress.LatestAccuracyPercentage,
            NormalizeActiveSeconds(progress.TotalActiveSeconds));

        return new ProgressOperationResponse(true, request.ExerciseId, ProgressStatus.Completed, now);
    }

    public async Task<ProgressOperationResponse> SaveStateAsync(
        string userId,
        SaveProgressStateRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Save progress state operation requested. UserId={UserId}, ExerciseId={ExerciseId}.",
            userId,
            request.ExerciseId);

        var now = DateTimeOffset.UtcNow;
        var current = await _repository.GetProgressAsync(userId, request.ExerciseId, cancellationToken);

        var progress = current ?? new UserProgressRecord
        {
            Id = CosmosUserProgressRepository.CreateProgressDocumentId(request.ExerciseId),
            UserId = userId,
            ExerciseId = request.ExerciseId,
            StartedAtUtc = now,
            UpdatedAtUtc = now,
            Status = ProgressStatus.InProgress
        };

        progress.StartedAtUtc = progress.StartedAtUtc == default ? now : progress.StartedAtUtc;
        if (!string.Equals(progress.Status, ProgressStatus.Completed, StringComparison.OrdinalIgnoreCase))
        {
            progress.Status = ProgressStatus.InProgress;
        }

        AccrueActiveSeconds(progress, now);
        progress.CurrentWordCount = NormalizeWordCount(request.WordCount) ?? progress.CurrentWordCount;
        progress.DraftExerciseState = NormalizeExerciseState(request.ExerciseState);
        progress.DraftUserText = NormalizeDraftText(request.UserText);
        progress.DraftAutoPauseSeconds = NormalizeAutoPauseSeconds(request.AutoPauseSeconds);
        progress.DraftPausedTimeSeconds = NormalizePausedTimeSeconds(request.PausedTimeSeconds);
        progress.UpdatedAtUtc = now;

        ApplyExerciseMetadata(progress, request.ExerciseTitle, request.Subject, request.Complexity);

        await _repository.UpsertProgressAsync(progress, cancellationToken);

        _logger.LogInformation(
            "Save progress state operation completed. UserId={UserId}, ExerciseId={ExerciseId}, Status={Status}, ExerciseState={ExerciseState}, WordCount={WordCount}.",
            userId,
            request.ExerciseId,
            progress.Status,
            progress.DraftExerciseState,
            progress.CurrentWordCount);

        return new ProgressOperationResponse(true, request.ExerciseId, progress.Status, now);
    }

    public async Task<ProgressStateResponse> GetStateAsync(string userId, int exerciseId, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Get progress state operation requested. UserId={UserId}, ExerciseId={ExerciseId}.",
            userId,
            exerciseId);

        var current = await _repository.GetProgressAsync(userId, exerciseId, cancellationToken);
        if (current is null)
        {
            _logger.LogInformation(
                "Get progress state operation found no record. UserId={UserId}, ExerciseId={ExerciseId}.",
                userId,
                exerciseId);

            return EmptyState(exerciseId);
        }

        var normalizedState = NormalizeExerciseState(current.DraftExerciseState);
        var normalizedText = NormalizeDraftText(current.DraftUserText);
        var normalizedWordCount = NormalizeWordCount(current.CurrentWordCount);
        var normalizedAutoPause = NormalizeAutoPauseSeconds(current.DraftAutoPauseSeconds);
        var normalizedPausedTime = NormalizePausedTimeSeconds(current.DraftPausedTimeSeconds);

        var hasServerState = normalizedState is not null
            || !string.IsNullOrWhiteSpace(normalizedText)
            || normalizedWordCount.HasValue
            || normalizedAutoPause.HasValue
            || normalizedPausedTime.HasValue;

        _logger.LogInformation(
            "Get progress state operation completed. UserId={UserId}, ExerciseId={ExerciseId}, HasServerState={HasServerState}.",
            userId,
            exerciseId,
            hasServerState);

        return new ProgressStateResponse(
            TrackingEnabled: true,
            ExerciseId: exerciseId,
            HasServerState: hasServerState,
            ExerciseState: normalizedState,
            UserText: normalizedText,
            WordCount: normalizedWordCount,
            AutoPauseSeconds: normalizedAutoPause,
            PausedTimeSeconds: normalizedPausedTime,
            UpdatedAtUtc: current.UpdatedAtUtc);
    }

    public async Task<ProgressSummaryResponse> GetSummaryAsync(string userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Get progress summary operation requested. UserId={UserId}.",
            userId);

        var items = await _repository.GetProgressItemsAsync(userId, cancellationToken);
        var attempts = await _repository.GetAttemptsAsync(userId, exerciseId: null, cancellationToken);

        var inProgressCount = items.Count(item => string.Equals(item.Status, ProgressStatus.InProgress, StringComparison.OrdinalIgnoreCase));
        var completedCount = items.Count(item => string.Equals(item.Status, ProgressStatus.Completed, StringComparison.OrdinalIgnoreCase));

        var completedScores = items
            .Where(item => string.Equals(item.Status, ProgressStatus.Completed, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.LatestAccuracyPercentage)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        var averageAccuracy = completedScores.Length == 0
            ? (double?)null
            : completedScores.Average();

        var bestAccuracyValues = items
            .Select(item => item.BestAccuracyPercentage)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        var bestAccuracy = bestAccuracyValues.Length == 0
            ? (double?)null
            : bestAccuracyValues.Max();

        var lastProgressActivity = items.Count == 0
            ? (DateTimeOffset?)null
            : items.Max(item => item.UpdatedAtUtc);

        var lastAttemptActivity = attempts.Count == 0
            ? (DateTimeOffset?)null
            : attempts.Max(item => item.CreatedAtUtc);

        var lastActivity = Max(lastProgressActivity, lastAttemptActivity);
        var totalActiveSeconds = items.Sum(item => NormalizeActiveSeconds(item.TotalActiveSeconds));

        _logger.LogInformation(
            "Get progress summary operation completed. UserId={UserId}, TotalItems={TotalItems}, InProgressCount={InProgressCount}, CompletedCount={CompletedCount}, TotalAttempts={TotalAttempts}, TotalActiveSeconds={TotalActiveSeconds}.",
            userId,
            items.Count,
            inProgressCount,
            completedCount,
            attempts.Count,
            totalActiveSeconds);

        return new ProgressSummaryResponse(
            true,
            items.Count,
            inProgressCount,
            completedCount,
            attempts.Count,
            totalActiveSeconds,
            averageAccuracy,
            bestAccuracy,
            lastActivity);
    }

    public async Task<IReadOnlyList<ProgressItemResponse>> GetItemsAsync(string userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Get progress items operation requested. UserId={UserId}.",
            userId);

        var items = await _repository.GetProgressItemsAsync(userId, cancellationToken);

        var response = items
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new ProgressItemResponse(
                item.ExerciseId,
                NormalizeStatus(item.Status),
                item.ExerciseTitle,
                item.Subject,
                item.Complexity,
                item.AttemptCount,
                item.LatestAccuracyPercentage,
                item.BestAccuracyPercentage,
                NormalizeActiveSeconds(item.TotalActiveSeconds),
                item.StartedAtUtc,
                item.CompletedAtUtc,
                item.UpdatedAtUtc,
                NormalizeWordCount(item.CurrentWordCount)))
            .ToArray();

        _logger.LogInformation(
            "Get progress items operation completed. UserId={UserId}, ItemCount={ItemCount}.",
            userId,
            response.Length);

        return response;
    }

    public async Task<IReadOnlyList<ProgressAttemptResponse>> GetAttemptsAsync(
        string userId,
        int? exerciseId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Get progress attempts operation requested. UserId={UserId}, ExerciseId={ExerciseId}.",
            userId,
            exerciseId);

        var attempts = await _repository.GetAttemptsAsync(userId, exerciseId, cancellationToken);

        var response = attempts
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => new ProgressAttemptResponse(
                item.Id,
                item.ExerciseId,
                item.AccuracyPercentage,
                item.WordCount,
                item.OriginalWordCount,
                NormalizeActiveSeconds(item.ActiveSeconds),
                item.CreatedAtUtc,
                item.ExerciseTitle,
                item.Subject,
                item.Complexity))
            .ToArray();

        _logger.LogInformation(
            "Get progress attempts operation completed. UserId={UserId}, ExerciseId={ExerciseId}, AttemptCount={AttemptCount}.",
            userId,
            exerciseId,
            response.Length);

        return response;
    }

    private static void ApplyExerciseMetadata(UserProgressRecord progress, string? exerciseTitle, string? subject, string? complexity)
    {
        if (!string.IsNullOrWhiteSpace(exerciseTitle))
        {
            progress.ExerciseTitle = exerciseTitle.Trim();
        }

        if (!string.IsNullOrWhiteSpace(subject))
        {
            progress.Subject = subject.Trim();
        }

        if (!string.IsNullOrWhiteSpace(complexity))
        {
            progress.Complexity = complexity.Trim();
        }
    }

    private static double? NormalizeAccuracyPercentage(double? accuracyPercentage)
    {
        if (!accuracyPercentage.HasValue)
        {
            return null;
        }

        if (double.IsNaN(accuracyPercentage.Value) || double.IsInfinity(accuracyPercentage.Value))
        {
            return null;
        }

        return Math.Clamp(accuracyPercentage.Value, 0d, 1d);
    }

    private static double? MergeBestAccuracy(double? currentBest, double? latestAccuracy)
    {
        if (!latestAccuracy.HasValue)
        {
            return currentBest;
        }

        if (!currentBest.HasValue)
        {
            return latestAccuracy;
        }

        return Math.Max(currentBest.Value, latestAccuracy.Value);
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.Equals(status, ProgressStatus.Completed, StringComparison.OrdinalIgnoreCase))
        {
            return ProgressStatus.Completed;
        }

        return ProgressStatus.InProgress;
    }

    private static int? NormalizeWordCount(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value < 0 ? 0 : value.Value;
    }

    private static int NormalizeActiveSeconds(int value)
    {
        return value < 0 ? 0 : value;
    }

    private static int? NormalizeAutoPauseSeconds(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value < 0 ? 0 : value.Value;
    }

    private static double? NormalizePausedTimeSeconds(double? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            return null;
        }

        return Math.Max(0d, value.Value);
    }

    private static string? NormalizeExerciseState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!AllowedExerciseStates.Contains(trimmed))
        {
            return null;
        }

        return trimmed.ToLowerInvariant();
    }

    private static string? NormalizeDraftText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= MaxDraftUserTextLength)
        {
            return trimmed;
        }

        return trimmed[..MaxDraftUserTextLength];
    }

    private static void AccrueActiveSeconds(UserProgressRecord progress, DateTimeOffset now)
    {
        var deltaSeconds = CalculateCappedDeltaSeconds(progress.LastInteractionAtUtc, now);
        if (deltaSeconds > 0)
        {
            progress.TotalActiveSeconds = NormalizeActiveSeconds(progress.TotalActiveSeconds) + deltaSeconds;
            progress.CurrentAttemptActiveSeconds = NormalizeActiveSeconds(progress.CurrentAttemptActiveSeconds) + deltaSeconds;
        }

        progress.LastInteractionAtUtc = now;
    }

    private static int CalculateCappedDeltaSeconds(DateTimeOffset? lastInteractionAtUtc, DateTimeOffset now)
    {
        if (!lastInteractionAtUtc.HasValue)
        {
            return 0;
        }

        var delta = now - lastInteractionAtUtc.Value;
        if (delta <= TimeSpan.Zero)
        {
            return 0;
        }

        var wholeSeconds = (int)Math.Floor(delta.TotalSeconds);
        if (wholeSeconds <= 0)
        {
            return 0;
        }

        return Math.Min(wholeSeconds, MaxIdleActiveSeconds);
    }

    private static ProgressStateResponse EmptyState(int exerciseId)
    {
        return new ProgressStateResponse(
            TrackingEnabled: true,
            ExerciseId: exerciseId,
            HasServerState: false,
            ExerciseState: null,
            UserText: null,
            WordCount: null,
            AutoPauseSeconds: null,
            PausedTimeSeconds: null,
            UpdatedAtUtc: null);
    }

    private static DateTimeOffset? Max(DateTimeOffset? first, DateTimeOffset? second)
    {
        if (!first.HasValue)
        {
            return second;
        }

        if (!second.HasValue)
        {
            return first;
        }

        return first.Value >= second.Value ? first : second;
    }
}
