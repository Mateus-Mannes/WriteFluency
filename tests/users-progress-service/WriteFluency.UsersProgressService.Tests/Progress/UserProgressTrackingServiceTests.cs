using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using WriteFluency.UsersProgressService.Progress;

namespace WriteFluency.UsersProgressService.Tests.Progress;

public class UserProgressTrackingServiceTests
{
    [Fact]
    public async Task StartThenCompleteThenStartAgain_ShouldKeepCompletedStatusAndResetCurrentAttemptAccumulator()
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
            new StartProgressRequest(12, "Climate exercise", "Science", "Intermediate"),
            CancellationToken.None);
        startedAgain.TrackingEnabled.ShouldBeTrue();
        startedAgain.Status.ShouldBe(ProgressStatus.Completed);

        var items = await service.GetItemsAsync(userId, CancellationToken.None);
        items.Count.ShouldBe(1);
        items[0].Status.ShouldBe(ProgressStatus.Completed);
        items[0].AttemptCount.ShouldBe(1);
        items[0].LatestAccuracyPercentage.ShouldBe(0.82);
        items[0].BestAccuracyPercentage.ShouldBe(0.82);
        items[0].OriginalWordCount.ShouldBe(135);
        items[0].ActiveSeconds.ShouldBeGreaterThanOrEqualTo(0);

        var progressAfterRestart = await repository.GetProgressAsync(userId, 12, CancellationToken.None);
        progressAfterRestart.ShouldNotBeNull();
        progressAfterRestart.CurrentAttemptActiveSeconds.ShouldBe(0);
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
    public async Task GetState_AfterCompleteWithoutDraft_ShouldNotReportServerState()
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
                Complexity: null),
            CancellationToken.None);

        var state = await service.GetStateAsync(userId, 39, CancellationToken.None);

        state.TrackingEnabled.ShouldBeTrue();
        state.HasServerState.ShouldBeFalse();
        state.ExerciseState.ShouldBeNull();
        state.UserText.ShouldBeNull();
        state.WordCount.ShouldBe(7);
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
