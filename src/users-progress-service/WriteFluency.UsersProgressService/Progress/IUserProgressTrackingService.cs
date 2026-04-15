namespace WriteFluency.UsersProgressService.Progress;

public interface IUserProgressTrackingService
{
    Task<ProgressOperationResponse> StartAsync(string userId, StartProgressRequest request, CancellationToken cancellationToken);

    Task<ProgressOperationResponse> CompleteAsync(string userId, CompleteProgressRequest request, CancellationToken cancellationToken);

    Task<ProgressOperationResponse> SaveStateAsync(string userId, SaveProgressStateRequest request, CancellationToken cancellationToken);

    Task<ProgressStateResponse> GetStateAsync(string userId, int exerciseId, CancellationToken cancellationToken);

    Task<ProgressSummaryResponse> GetSummaryAsync(string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProgressItemResponse>> GetItemsAsync(string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProgressAttemptResponse>> GetAttemptsAsync(string userId, int? exerciseId, CancellationToken cancellationToken);
}
