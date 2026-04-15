namespace WriteFluency.UsersProgressService.Progress;

public interface IUserProgressRepository
{
    bool IsEnabled { get; }

    Task<bool> EnsureInitializedAsync(CancellationToken cancellationToken);

    Task<UserProgressRecord?> GetProgressAsync(string userId, int exerciseId, CancellationToken cancellationToken);

    Task UpsertProgressAsync(UserProgressRecord progress, CancellationToken cancellationToken);

    Task AddAttemptAsync(UserAttemptRecord attempt, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserProgressRecord>> GetProgressItemsAsync(string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserAttemptRecord>> GetAttemptsAsync(string userId, int? exerciseId, CancellationToken cancellationToken);
}
