using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using WriteFluency.UsersProgressService.Progress;

namespace WriteFluency.UsersProgressService.Tests.Progress;

public class UserProgressTrackingServiceTests
{
    [Fact]
    public async Task StartThenCompleteThenStartAgainWithResetIntent_ShouldMoveToInProgressAndResetCurrentAttemptAccumulator()
    {
        var repository = new InMemoryUserProgressRepository();
        var service = new UserProgressTrackingService(repository, NullLogger<UserProgressTrackingService>.Instance);
        var userId = "user-1";

        var started = await service.StartAsync(
            userId,
            new StartProgressRequest(12, "Climate exercise", "Science", "Intermediate"),
            CancellationToken.None);
        started.TrackingEnabled.ShouldBeTrue();
        started.Status.ShouldBe(ProgressStatus.InProgress);

        var completed = await service.CompleteAsync(
            userId,
            new CompleteProgressRequest(12, 0.82, 120, 135, "Climate exercise", "Science", "Intermediate"),
            CancellationToken.None);
        completed.TrackingEnabled.ShouldBeTrue();
        completed.Status.ShouldBe(ProgressStatus.Completed);

        var seededProgress = await repository.GetProgressAsync(userId, 12, CancellationToken.None);
        seededProgress.ShouldNotBeNull();
        seededProgress.CurrentAttemptActiveSeconds = 123;
        seededProgress.LastInteractionAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
        await repository.UpsertProgressAsync(seededProgress, CancellationToken.None);

        var startedAgain = await service.StartAsync(
            userId,
            new StartProgressRequest(
                ExerciseId: 12,
                ExerciseTitle: "Climate exercise",
                Subject: "Science",
                Complexity: "Intermediate",
                ResetCompletedState: true),
            CancellationToken.None);
        startedAgain.TrackingEnabled.ShouldBeTrue();
        startedAgain.Status.ShouldBe(ProgressStatus.InProgress);

        var items = await service.GetItemsAsync(userId, CancellationToken.None);
        items.Count.ShouldBe(1);
        items[0].Status.ShouldBe(ProgressStatus.InProgress);
        items[0].AttemptCount.ShouldBe(1);
        items[0].LatestAccuracyPercentage.ShouldBe(0.82);
        items[0].BestAccuracyPercentage.ShouldBe(0.82);
        items[0].OriginalWordCount.ShouldBe(135);
        items[0].ActiveSeconds.ShouldBeGreaterThanOrEqualTo(0);

        var progressAfterRestart = await repository.GetProgressAsync(userId, 12, CancellationToken.None);
        progressAfterRestart.ShouldNotBeNull();
        progressAfterRestart.Status.ShouldBe(ProgressStatus.InProgress);
        progressAfterRestart.CurrentAttemptActiveSeconds.ShouldBe(0);
    }

    [Fact]
    public async Task StartCompletedExerciseWithoutResetIntent_ShouldPreserveCompletedRestoreState()
    {
        var repository = new InMemoryUserProgressRepository();
        var service = new UserProgressTrackingService(repository, NullLogger<UserProgressTrackingService>.Instance);
        var userId = "user-start-preserve";

        await service.CompleteAsync(
            userId,
            new CompleteProgressRequest(
                ExerciseId: 13,
                AccuracyPercentage: 0.7,
                WordCount: 8,
                OriginalWordCount: 10,
                ExerciseTitle: "Exercise 13",
                Subject: "Science",
                Complexity: "Intermediate",
                UserText: "submitted text",
                OriginalText: "original exercise text",
                Comparisons:
                [
                    new ProgressTextComparison(
                        new ProgressTextRange(0, 8),
                        "original",
                        new ProgressTextRange(0, 9),
                        "submitted",
                        SourceComparisonIndex: 3,
                        IsAiRefined: true)
                ],
                CorrectionMode: "ai_refined",
                AiAttempted: true,
                CorrectionTrace:
                [
                    new ProgressCorrectionTraceEntry(
                        3,
                        new ProgressComparisonSnapshot(
                            new ProgressTextRange(0, 8),
                            "original",
                            new ProgressTextRange(0, 9),
                            "submitted"),
                        Ai: new ProgressCorrectionStageTrace(
                            "refine",
                            "word_substitution",
                            [
                                new ProgressComparisonSnapshot(
                                    new ProgressTextRange(0, 8),
                                    "original",
                                    new ProgressTextRange(0, 9),
                                    "submitted")
                            ],
                            ValidationStatus: "accepted"))
                ]),
            CancellationToken.None);

        var completedProgress = await repository.GetProgressAsync(userId, 13, CancellationToken.None);
        completedProgress.ShouldNotBeNull();
        completedProgress.CurrentAttemptActiveSeconds = 55;
        completedProgress.LastInteractionAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
        await repository.UpsertProgressAsync(completedProgress, CancellationToken.None);

        var started = await service.StartAsync(
            userId,
            new StartProgressRequest(13, "Exercise 13", "Science", "Intermediate"),
            CancellationToken.None);

        started.Status.ShouldBe(ProgressStatus.Completed);

        var state = await service.GetStateAsync(userId, 13, CancellationToken.None);
        state.HasServerState.ShouldBeTrue();
        state.ExerciseState.ShouldBe("results");
        state.UserText.ShouldBe("submitted text");
        state.OriginalText.ShouldBe("original exercise text");
        state.Comparisons.ShouldNotBeNull();
        state.Comparisons.Count.ShouldBe(1);
        state.Comparisons.Single().SourceComparisonIndex.ShouldBe(3);
        state.Comparisons.Single().IsAiRefined.ShouldBeTrue();
        state.CorrectionMode.ShouldBe("ai_refined");
        state.AiAttempted.ShouldBe(true);
        state.CorrectionTrace.ShouldNotBeNull();
        state.CorrectionTrace.Single().Ai.ShouldNotBeNull();

        var attempts = await repository.GetAttemptsAsync(
            userId,
            13,
            CancellationToken.None);
        var attempt = attempts.Single();
        attempt.CorrectionMode.ShouldBe("ai_refined");
        attempt.AiAttempted.ShouldBe(true);
        attempt.Comparisons.ShouldNotBeNull();
        attempt.CorrectionTrace.ShouldNotBeNull();

        var progressAfterStart = await repository.GetProgressAsync(userId, 13, CancellationToken.None);
        progressAfterStart.ShouldNotBeNull();
        progressAfterStart.CurrentAttemptActiveSeconds.ShouldBe(55);
    }

    [Fact]
    public async Task SaveStateCompletedExercise_ShouldIgnoreRequestAndPreserveCompletedRestoreState()
    {
        var repository = new InMemoryUserProgressRepository();
        var service = new UserProgressTrackingService(repository, NullLogger<UserProgressTrackingService>.Instance);
        var userId = "user-save-completed";

        await service.CompleteAsync(
            userId,
            new CompleteProgressRequest(
                ExerciseId: 14,
                AccuracyPercentage: 0.8,
                WordCount: 5,
                OriginalWordCount: 6,
                ExerciseTitle: "Exercise 14",
                Subject: "Science",
                Complexity: "Intermediate",
                UserText: "completed text",
                OriginalText: "completed original",
                Comparisons:
                [
                    new ProgressTextComparison(
                        new ProgressTextRange(0, 8),
                        "completed",
                        new ProgressTextRange(0, 8),
                        "completed")
                ]),
            CancellationToken.None);

        var beforeSave = await repository.GetProgressAsync(userId, 14, CancellationToken.None);
        beforeSave.ShouldNotBeNull();
        var beforeUpdatedAt = beforeSave.UpdatedAtUtc;

        var saved = await service.SaveStateAsync(
            userId,
            new SaveProgressStateRequest(
                ExerciseId: 14,
                ExerciseState: "exercise",
                UserText: "accidental draft",
                WordCount: 2,
                AutoPauseSeconds: 4,
                PausedTimeSeconds: 9,
                ExerciseTitle: "Changed title",
                Subject: "Changed subject",
                Complexity: "Changed complexity"),
            CancellationToken.None);

        saved.Status.ShouldBe(ProgressStatus.Completed);
        saved.UpdatedAtUtc.ShouldBe(beforeUpdatedAt);

        var state = await service.GetStateAsync(userId, 14, CancellationToken.None);
        state.ExerciseState.ShouldBe("results");
        state.UserText.ShouldBe("completed text");
        state.OriginalText.ShouldBe("completed original");
        state.WordCount.ShouldBe(5);
        state.AutoPauseSeconds.ShouldBeNull();
        state.PausedTimeSeconds.ShouldBeNull();
        state.Comparisons.ShouldNotBeNull();
        state.Comparisons.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CompleteAlreadyCompletedExercise_ShouldIgnoreRequestAndPreserveExistingCompletion()
    {
        var repository = new InMemoryUserProgressRepository();
        var service = new UserProgressTrackingService(repository, NullLogger<UserProgressTrackingService>.Instance);
        var userId = "user-complete-completed";

        await service.CompleteAsync(
            userId,
            new CompleteProgressRequest(
                ExerciseId: 15,
                AccuracyPercentage: 0.8,
                WordCount: 5,
                OriginalWordCount: 6,
                ExerciseTitle: "Exercise 15",
                Subject: "Science",
                Complexity: "Intermediate",
                UserText: "first completed text",
                OriginalText: "first completed original",
                Comparisons:
                [
                    new ProgressTextComparison(
                        new ProgressTextRange(0, 5),
                        "first",
                        new ProgressTextRange(0, 5),
                        "first")
                ]),
            CancellationToken.None);

        var firstState = await service.GetStateAsync(userId, 15, CancellationToken.None);
        var ignored = await service.CompleteAsync(
            userId,
            new CompleteProgressRequest(
                ExerciseId: 15,
                AccuracyPercentage: 0.1,
                WordCount: 1,
                OriginalWordCount: 2,
                ExerciseTitle: "Changed exercise",
                Subject: "Changed subject",
                Complexity: "Changed complexity",
                UserText: "second completed text",
                OriginalText: "second completed original",
                Comparisons: []),
            CancellationToken.None);

        ignored.Status.ShouldBe(ProgressStatus.Completed);

        var secondState = await service.GetStateAsync(userId, 15, CancellationToken.None);
        secondState.UpdatedAtUtc.ShouldBe(firstState.UpdatedAtUtc);
        secondState.UserText.ShouldBe("first completed text");
        secondState.OriginalText.ShouldBe("first completed original");
        secondState.AccuracyPercentage.ShouldBe(0.8);
        secondState.WordCount.ShouldBe(5);

        var attempts = await service.GetAttemptsAsync(userId, exerciseId: 15, CancellationToken.None);
        attempts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SaveState_ShouldAccrueCappedActiveTime()
    {
        var repository = new InMemoryUserProgressRepository();
        var service = new UserProgressTrackingService(repository, NullLogger<UserProgressTrackingService>.Instance);
        var userId = "user-2";

        await repository.UpsertProgressAsync(
            new UserProgressRecord
            {
                Id = CosmosUserProgressRepository.CreateProgressDocumentId(21),
                UserId = userId,
                ExerciseId = 21,
                Status = ProgressStatus.InProgress,
                StartedAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
                UpdatedAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
                TotalActiveSeconds = 10,
                CurrentAttemptActiveSeconds = 4,
                LastInteractionAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
            },
            CancellationToken.None);

        await service.SaveStateAsync(
            userId,
            new SaveProgressStateRequest(
                ExerciseId: 21,
                ExerciseState: "exercise",
                UserText: "one two three",
                WordCount: 3,
                OriginalWordCount: 111,
                AutoPauseSeconds: 2,
                PausedTimeSeconds: 5,
                ExerciseTitle: "Exercise 21",
                Subject: "World",
                Complexity: "Easy"),
            CancellationToken.None);

        var progress = await repository.GetProgressAsync(userId, 21, CancellationToken.None);
        progress.ShouldNotBeNull();
        progress.TotalActiveSeconds.ShouldBe(70);
        progress.CurrentAttemptActiveSeconds.ShouldBe(64);
        progress.LastInteractionAtUtc.ShouldNotBeNull();

        var items = await service.GetItemsAsync(userId, CancellationToken.None);
        items.Count.ShouldBe(1);
        items[0].ActiveSeconds.ShouldBe(70);
        items[0].CurrentWordCount.ShouldBe(3);
        items[0].OriginalWordCount.ShouldBe(111);
    }

    [Fact]
    public async Task Complete_ShouldPersistAttemptActiveSecondsAndExposeActiveTimeInSummaryItemsAttempts()
    {
        var repository = new InMemoryUserProgressRepository();
        var service = new UserProgressTrackingService(repository, NullLogger<UserProgressTrackingService>.Instance);
        var userId = "user-3";

        await repository.UpsertProgressAsync(
            new UserProgressRecord
            {
                Id = CosmosUserProgressRepository.CreateProgressDocumentId(22),
                UserId = userId,
                ExerciseId = 22,
                Status = ProgressStatus.InProgress,
                StartedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
                UpdatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
                TotalActiveSeconds = 40,
                CurrentAttemptActiveSeconds = 12,
                LastInteractionAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3)
            },
            CancellationToken.None);

        var completed = await service.CompleteAsync(
            userId,
            new CompleteProgressRequest(22, 0.91, 140, 150, "Exercise 22", "Economy", "Hard"),
            CancellationToken.None);

        completed.TrackingEnabled.ShouldBeTrue();
        completed.Status.ShouldBe(ProgressStatus.Completed);

        var items = await service.GetItemsAsync(userId, CancellationToken.None);
        items.Count.ShouldBe(1);
        items[0].Status.ShouldBe(ProgressStatus.Completed);
        items[0].ActiveSeconds.ShouldBe(100);
        items[0].OriginalWordCount.ShouldBe(150);

        var attempts = await service.GetAttemptsAsync(userId, exerciseId: 22, CancellationToken.None);
        attempts.Count.ShouldBe(1);
        attempts[0].ActiveSeconds.ShouldBe(72);

        var summary = await service.GetSummaryAsync(userId, CancellationToken.None);
        summary.TrackingEnabled.ShouldBeTrue();
        summary.TotalItems.ShouldBe(1);
        summary.CompletedCount.ShouldBe(1);
        summary.TotalAttempts.ShouldBe(1);
        summary.TotalActiveSeconds.ShouldBe(100);
    }

    [Fact]
    public async Task SaveStateAndGetState_ShouldRoundTripDraftForInProgressExercise()
    {
        var repository = new InMemoryUserProgressRepository();
        var service = new UserProgressTrackingService(repository, NullLogger<UserProgressTrackingService>.Instance);
        var userId = "user-4";

        var saved = await service.SaveStateAsync(
            userId,
            new SaveProgressStateRequest(
                ExerciseId: 30,
                ExerciseState: "exercise",
                UserText: "one two three four",
                WordCount: 4,
                OriginalWordCount: 120,
                AutoPauseSeconds: 3,
                PausedTimeSeconds: 12.5,
                ExerciseTitle: "Exercise 30",
                Subject: "Technology",
                Complexity: "Medium"),
            CancellationToken.None);

        saved.TrackingEnabled.ShouldBeTrue();
        saved.Status.ShouldBe(ProgressStatus.InProgress);

        var state = await service.GetStateAsync(userId, 30, CancellationToken.None);
        state.TrackingEnabled.ShouldBeTrue();
        state.HasServerState.ShouldBeTrue();
        state.ExerciseState.ShouldBe("exercise");
        state.UserText.ShouldBe("one two three four");
        state.WordCount.ShouldBe(4);
        state.AutoPauseSeconds.ShouldBe(3);
        state.PausedTimeSeconds.ShouldBe(12.5);

        var items = await service.GetItemsAsync(userId, CancellationToken.None);
        items.Count.ShouldBe(1);
        items[0].Status.ShouldBe(ProgressStatus.InProgress);
        items[0].CurrentWordCount.ShouldBe(4);
        items[0].OriginalWordCount.ShouldBe(120);
        items[0].ActiveSeconds.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetState_AfterComplete_ShouldReportRestorableResultsState()
    {
        var repository = new InMemoryUserProgressRepository();
        var service = new UserProgressTrackingService(repository, NullLogger<UserProgressTrackingService>.Instance);
        var userId = "user-5";

        await service.SaveStateAsync(
            userId,
            new SaveProgressStateRequest(
                ExerciseId: 39,
                ExerciseState: "exercise",
                UserText: "rewq dsaf ads fewq radsf as 4",
                WordCount: 7,
                AutoPauseSeconds: 0,
                PausedTimeSeconds: 0,
                ExerciseTitle: "Exercise 39",
                Subject: null,
                Complexity: null),
            CancellationToken.None);

        await service.CompleteAsync(
            userId,
            new CompleteProgressRequest(
                ExerciseId: 39,
                AccuracyPercentage: 0,
                WordCount: 7,
                OriginalWordCount: 113,
                ExerciseTitle: "Exercise 39",
                Subject: null,
                Complexity: null,
                UserText: "rewq dsaf ads fewq radsf as 4",
                OriginalText: "correct original exercise text",
                Comparisons:
                [
                    new ProgressTextComparison(
                        new ProgressTextRange(0, 7),
                        "correct",
                        new ProgressTextRange(0, 3),
                        "rewq")
                ]),
            CancellationToken.None);

        var state = await service.GetStateAsync(userId, 39, CancellationToken.None);

        state.TrackingEnabled.ShouldBeTrue();
        state.HasServerState.ShouldBeTrue();
        state.ExerciseState.ShouldBe("results");
        state.UserText.ShouldBe("rewq dsaf ads fewq radsf as 4");
        state.WordCount.ShouldBe(7);
        state.AccuracyPercentage.ShouldBe(0);
        state.OriginalText.ShouldBe("correct original exercise text");
        state.Comparisons.ShouldNotBeNull();
        state.Comparisons.Count.ShouldBe(1);
        state.Comparisons[0].OriginalText.ShouldBe("correct");
        state.Comparisons[0].UserText.ShouldBe("rewq");
        state.Comparisons[0].OriginalTextRange.ShouldBe(new ProgressTextRange(0, 7));
        state.Comparisons[0].UserTextRange.ShouldBe(new ProgressTextRange(0, 3));
    }

    [Fact]
    public async Task GetItemsAndSummary_ShouldReturnAtMost100MostRecentExercises()
    {
        var repository = new InMemoryUserProgressRepository();
        var service = new UserProgressTrackingService(repository, NullLogger<UserProgressTrackingService>.Instance);
        var userId = "user-6";
        var now = DateTimeOffset.UtcNow;

        for (var exerciseId = 1; exerciseId <= 120; exerciseId++)
        {
            await repository.UpsertProgressAsync(
                new UserProgressRecord
                {
                    Id = CosmosUserProgressRepository.CreateProgressDocumentId(exerciseId),
                    UserId = userId,
                    ExerciseId = exerciseId,
                    Status = exerciseId % 2 == 0 ? ProgressStatus.Completed : ProgressStatus.InProgress,
                    StartedAtUtc = now.AddMinutes(-exerciseId),
                    UpdatedAtUtc = now.AddMinutes(-exerciseId),
                    TotalActiveSeconds = exerciseId
                },
                CancellationToken.None);
        }

        var items = await service.GetItemsAsync(userId, CancellationToken.None);
        items.Count.ShouldBe(100);
        items[0].ExerciseId.ShouldBe(1);
        items[^1].ExerciseId.ShouldBe(100);

        var summary = await service.GetSummaryAsync(userId, CancellationToken.None);
        summary.TotalItems.ShouldBe(100);
    }

    private sealed class InMemoryUserProgressRepository : IUserProgressRepository
    {
        private readonly Dictionary<string, UserProgressRecord> _progress = new(StringComparer.Ordinal);
        private readonly List<UserAttemptRecord> _attempts = [];

        public bool IsEnabled => true;

        public Task<bool> EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<UserProgressRecord?> GetProgressAsync(string userId, int exerciseId, CancellationToken cancellationToken)
        {
            var key = BuildKey(userId, exerciseId);
            _progress.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task UpsertProgressAsync(UserProgressRecord progress, CancellationToken cancellationToken)
        {
            _progress[BuildKey(progress.UserId, progress.ExerciseId)] = progress;
            return Task.CompletedTask;
        }

        public Task AddAttemptAsync(UserAttemptRecord attempt, CancellationToken cancellationToken)
        {
            _attempts.Add(attempt);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UserProgressRecord>> GetProgressItemsAsync(string userId, CancellationToken cancellationToken)
        {
            var items = _progress.Values
                .Where(item => string.Equals(item.UserId, userId, StringComparison.Ordinal))
                .OrderByDescending(item => item.UpdatedAtUtc)
                .Take(100)
                .ToArray();

            return Task.FromResult<IReadOnlyList<UserProgressRecord>>(items);
        }

        public Task<IReadOnlyList<UserAttemptRecord>> GetAttemptsAsync(string userId, int? exerciseId, CancellationToken cancellationToken)
        {
            var attempts = _attempts
                .Where(item => string.Equals(item.UserId, userId, StringComparison.Ordinal))
                .Where(item => !exerciseId.HasValue || item.ExerciseId == exerciseId.Value)
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToArray();

            return Task.FromResult<IReadOnlyList<UserAttemptRecord>>(attempts);
        }

        private static string BuildKey(string userId, int exerciseId)
        {
            return $"{userId}:{exerciseId}";
        }
    }
}
