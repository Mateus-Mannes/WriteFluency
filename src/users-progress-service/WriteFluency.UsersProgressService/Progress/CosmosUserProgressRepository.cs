using System.Net;
using Azure.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using WriteFluency.UsersProgressService.Options;

namespace WriteFluency.UsersProgressService.Progress;

public sealed class CosmosUserProgressRepository : IUserProgressRepository
{
    private readonly CosmosProgressOptions _options;
    private readonly TokenCredential _credential;
    private readonly ILogger<CosmosUserProgressRepository> _logger;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);

    private CosmosClient? _client;
    private Container? _progressContainer;
    private Container? _attemptsContainer;

    private volatile bool _initialized;
    private volatile bool _enabled;

    public CosmosUserProgressRepository(
        IOptions<CosmosProgressOptions> options,
        TokenCredential credential,
        ILogger<CosmosUserProgressRepository> logger)
    {
        _options = options.Value;
        _credential = credential;
        _logger = logger;
    }

    public bool IsEnabled => _enabled;

    public async Task<bool> EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            _logger.LogDebug("Cosmos progress repository already initialized. Enabled={Enabled}.", _enabled);
            return _enabled;
        }

        await _initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return _enabled;
            }

            if (!_options.IsConfigured)
            {
                throw new InvalidOperationException(
                    $"Cosmos progress configuration is incomplete. Namespace={_options.NormalizedNamespace}, EndpointConfigured={!string.IsNullOrWhiteSpace(_options.Endpoint)}, DatabaseConfigured={!string.IsNullOrWhiteSpace(_options.DatabaseName)}.");
            }

            try
            {
                var clientOptions = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConsistencyLevel = ConsistencyLevel.Session,
                    ApplicationName = "WriteFluency.UsersProgressService",
                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                    }
                };

                _client = new CosmosClient(_options.Endpoint, _credential, clientOptions);

                var database = _client.GetDatabase(_options.DatabaseName);
                await database.ReadAsync(cancellationToken: cancellationToken);

                var progressContainerName = _options.ResolveProgressContainerName();
                var attemptsContainerName = _options.ResolveAttemptsContainerName();

                _progressContainer = database.GetContainer(progressContainerName);
                _attemptsContainer = database.GetContainer(attemptsContainerName);

                await _progressContainer.ReadContainerAsync(cancellationToken: cancellationToken);
                await _attemptsContainer.ReadContainerAsync(cancellationToken: cancellationToken);

                _enabled = true;
                _initialized = true;
                _logger.LogInformation(
                    "Cosmos progress tracking initialized successfully. Namespace={Namespace}, Database={DatabaseName}, ProgressContainer={ProgressContainer}, AttemptsContainer={AttemptsContainer}.",
                    _options.NormalizedNamespace,
                    _options.DatabaseName,
                    progressContainerName,
                    attemptsContainerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos progress tracking initialization failed. Namespace={Namespace}, Endpoint={Endpoint}, Database={DatabaseName}.",
                    _options.NormalizedNamespace,
                    _options.Endpoint,
                    _options.DatabaseName);

                throw;
            }

            return true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public async Task<UserProgressRecord?> GetProgressAsync(string userId, int exerciseId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        _logger.LogInformation(
            "Loading progress record. UserId={UserId}, ExerciseId={ExerciseId}.",
            userId,
            exerciseId);

        try
        {
            var response = await GetProgressContainer().ReadItemAsync<UserProgressRecord>(
                CreateProgressDocumentId(exerciseId),
                new PartitionKey(userId),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Progress record loaded. UserId={UserId}, ExerciseId={ExerciseId}, Status={Status}, UpdatedAtUtc={UpdatedAtUtc}, RequestCharge={RequestCharge}.",
                userId,
                exerciseId,
                response.Resource.Status,
                response.Resource.UpdatedAtUtc,
                response.RequestCharge);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "Progress record not found. UserId={UserId}, ExerciseId={ExerciseId}.",
                userId,
                exerciseId);

            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(
                ex,
                "Failed to load progress record. UserId={UserId}, ExerciseId={ExerciseId}, StatusCode={StatusCode}, ActivityId={ActivityId}.",
                userId,
                exerciseId,
                ex.StatusCode,
                ex.ActivityId);

            throw;
        }
    }

    public async Task UpsertProgressAsync(UserProgressRecord progress, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var response = await GetProgressContainer()
                .UpsertItemAsync(progress, new PartitionKey(progress.UserId), cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Progress record upserted. UserId={UserId}, ExerciseId={ExerciseId}, Status={Status}, AttemptCount={AttemptCount}, UpdatedAtUtc={UpdatedAtUtc}, RequestCharge={RequestCharge}.",
                progress.UserId,
                progress.ExerciseId,
                progress.Status,
                progress.AttemptCount,
                progress.UpdatedAtUtc,
                response.RequestCharge);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(
                ex,
                "Failed to upsert progress record. UserId={UserId}, ExerciseId={ExerciseId}, StatusCode={StatusCode}, ActivityId={ActivityId}.",
                progress.UserId,
                progress.ExerciseId,
                ex.StatusCode,
                ex.ActivityId);

            throw;
        }
    }

    public async Task AddAttemptAsync(UserAttemptRecord attempt, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var response = await GetAttemptsContainer()
                .CreateItemAsync(attempt, new PartitionKey(attempt.UserId), cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Progress attempt created. UserId={UserId}, ExerciseId={ExerciseId}, AttemptId={AttemptId}, CreatedAtUtc={CreatedAtUtc}, RequestCharge={RequestCharge}.",
                attempt.UserId,
                attempt.ExerciseId,
                attempt.Id,
                attempt.CreatedAtUtc,
                response.RequestCharge);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(
                ex,
                "Failed to create progress attempt. UserId={UserId}, ExerciseId={ExerciseId}, AttemptId={AttemptId}, StatusCode={StatusCode}, ActivityId={ActivityId}.",
                attempt.UserId,
                attempt.ExerciseId,
                attempt.Id,
                ex.StatusCode,
                ex.ActivityId);

            throw;
        }
    }

    public async Task<IReadOnlyList<UserProgressRecord>> GetProgressItemsAsync(string userId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        const string query = "SELECT * FROM c WHERE c.userId = @userId ORDER BY c.updatedAtUtc DESC";
        var queryDefinition = new QueryDefinition(query)
            .WithParameter("@userId", userId);

        try
        {
            return await ExecuteQueryAsync<UserProgressRecord>(
                operationName: "GetProgressItems",
                container: GetProgressContainer(),
                queryDefinition: queryDefinition,
                userId: userId,
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(
                ex,
                "Failed to query progress items. UserId={UserId}, StatusCode={StatusCode}, ActivityId={ActivityId}.",
                userId,
                ex.StatusCode,
                ex.ActivityId);

            throw;
        }
    }

    public async Task<IReadOnlyList<UserAttemptRecord>> GetAttemptsAsync(string userId, int? exerciseId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        QueryDefinition queryDefinition;
        if (exerciseId.HasValue)
        {
            queryDefinition = new QueryDefinition("SELECT TOP 200 * FROM c WHERE c.userId = @userId AND c.exerciseId = @exerciseId ORDER BY c.createdAtUtc DESC")
                .WithParameter("@userId", userId)
                .WithParameter("@exerciseId", exerciseId.Value);
        }
        else
        {
            queryDefinition = new QueryDefinition("SELECT TOP 200 * FROM c WHERE c.userId = @userId ORDER BY c.createdAtUtc DESC")
                .WithParameter("@userId", userId);
        }

        try
        {
            return await ExecuteQueryAsync<UserAttemptRecord>(
                operationName: "GetAttempts",
                container: GetAttemptsContainer(),
                queryDefinition: queryDefinition,
                userId: userId,
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(
                ex,
                "Failed to query attempts. UserId={UserId}, ExerciseId={ExerciseId}, StatusCode={StatusCode}, ActivityId={ActivityId}.",
                userId,
                exerciseId,
                ex.StatusCode,
                ex.ActivityId);

            throw;
        }
    }

    private async Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(
        string operationName,
        Container container,
        QueryDefinition queryDefinition,
        string userId,
        CancellationToken cancellationToken)
    {
        var results = new List<T>();
        var pageCount = 0;
        var totalRequestCharge = 0d;
        var iterator = container.GetItemQueryIterator<T>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(userId)
            });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            pageCount++;
            totalRequestCharge += response.RequestCharge;
            results.AddRange(response.Resource);
        }

        _logger.LogInformation(
            "Cosmos query completed. Operation={Operation}, Container={Container}, UserId={UserId}, ResultCount={ResultCount}, PageCount={PageCount}, RequestCharge={RequestCharge}.",
            operationName,
            container.Id,
            userId,
            results.Count,
            pageCount,
            totalRequestCharge);

        return results;
    }

    private Container GetProgressContainer()
    {
        return _progressContainer ?? throw new InvalidOperationException("Progress container is not initialized.");
    }

    private Container GetAttemptsContainer()
    {
        return _attemptsContainer ?? throw new InvalidOperationException("Attempts container is not initialized.");
    }

    public static string CreateProgressDocumentId(int exerciseId)
    {
        return $"exercise-{exerciseId}";
    }
}
