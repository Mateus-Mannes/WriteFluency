using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using WriteFluency.UsersProgressService.Authentication;
using WriteFluency.UsersProgressService.Options;
using WriteFluency.UsersProgressService.Progress;

namespace WriteFluency.UsersProgressService.Functions;

public sealed class UserProgressFunctions
{
    private readonly ISharedCookieAuthenticationService _authenticationService;
    private readonly IUserProgressTrackingService _progressTrackingService;
    private readonly IOptionsMonitor<CorsOptions> _corsOptions;
    private readonly ILogger<UserProgressFunctions> _logger;

    public UserProgressFunctions(
        ISharedCookieAuthenticationService authenticationService,
        IUserProgressTrackingService progressTrackingService,
        IOptionsMonitor<CorsOptions> corsOptions,
        ILogger<UserProgressFunctions> logger)
    {
        _authenticationService = authenticationService;
        _progressTrackingService = progressTrackingService;
        _corsOptions = corsOptions;
        _logger = logger;
    }

    [Function("ProgressStart")]
    public async Task<HttpResponseData> StartAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users/progress/start")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Progress start request received. Method={Method}, Path={Path}.",
            request.Method,
            request.Url.AbsolutePath);

        var auth = _authenticationService.Authenticate(request);
        if (!auth.IsAuthenticated || string.IsNullOrWhiteSpace(auth.UserId))
        {
            _logger.LogWarning(
                "Progress start request rejected as unauthorized. Path={Path}.",
                request.Url.AbsolutePath);

            return await CreateJsonAsync(request, HttpStatusCode.Unauthorized, new { Error = "unauthorized" }, cancellationToken);
        }

        if (!IsRequestFromAllowedOrigin(request, _corsOptions.CurrentValue.AllowedOrigins))
        {
            _logger.LogWarning(
                "Progress start request rejected by origin validation. UserId={UserId}, Origin={Origin}, Referer={Referer}.",
                auth.UserId,
                ReadSingleHeader(request, "Origin"),
                ReadSingleHeader(request, "Referer"));

            return await CreateJsonAsync(request, HttpStatusCode.Forbidden, new { Error = "csrf_origin_invalid" }, cancellationToken);
        }

        var body = await request.ReadFromJsonAsync<StartProgressRequest>(cancellationToken);
        if (body is null || body.ExerciseId <= 0)
        {
            _logger.LogWarning(
                "Progress start request rejected due to invalid payload. UserId={UserId}, HasPayload={HasPayload}, ExerciseId={ExerciseId}.",
                auth.UserId,
                body is not null,
                body?.ExerciseId);

            return await CreateJsonAsync(request, HttpStatusCode.BadRequest, new { Error = "exercise_id_invalid" }, cancellationToken);
        }

        var response = await _progressTrackingService.StartAsync(auth.UserId, body, cancellationToken);

        _logger.LogInformation(
            "Progress start request processed. UserId={UserId}, ExerciseId={ExerciseId}, Status={Status}, TrackingEnabled={TrackingEnabled}.",
            auth.UserId,
            body.ExerciseId,
            response.Status,
            response.TrackingEnabled);

        return await CreateJsonAsync(request, HttpStatusCode.OK, response, cancellationToken);
    }

    [Function("ProgressComplete")]
    public async Task<HttpResponseData> CompleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users/progress/complete")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Progress complete request received. Method={Method}, Path={Path}.",
            request.Method,
            request.Url.AbsolutePath);

        var auth = _authenticationService.Authenticate(request);
        if (!auth.IsAuthenticated || string.IsNullOrWhiteSpace(auth.UserId))
        {
            _logger.LogWarning(
                "Progress complete request rejected as unauthorized. Path={Path}.",
                request.Url.AbsolutePath);

            return await CreateJsonAsync(request, HttpStatusCode.Unauthorized, new { Error = "unauthorized" }, cancellationToken);
        }

        if (!IsRequestFromAllowedOrigin(request, _corsOptions.CurrentValue.AllowedOrigins))
        {
            _logger.LogWarning(
                "Progress complete request rejected by origin validation. UserId={UserId}, Origin={Origin}, Referer={Referer}.",
                auth.UserId,
                ReadSingleHeader(request, "Origin"),
                ReadSingleHeader(request, "Referer"));

            return await CreateJsonAsync(request, HttpStatusCode.Forbidden, new { Error = "csrf_origin_invalid" }, cancellationToken);
        }

        var body = await request.ReadFromJsonAsync<CompleteProgressRequest>(cancellationToken);
        if (body is null || body.ExerciseId <= 0)
        {
            _logger.LogWarning(
                "Progress complete request rejected due to invalid payload. UserId={UserId}, HasPayload={HasPayload}, ExerciseId={ExerciseId}.",
                auth.UserId,
                body is not null,
                body?.ExerciseId);

            return await CreateJsonAsync(request, HttpStatusCode.BadRequest, new { Error = "exercise_id_invalid" }, cancellationToken);
        }

        var response = await _progressTrackingService.CompleteAsync(auth.UserId, body, cancellationToken);

        _logger.LogInformation(
            "Progress complete request processed. UserId={UserId}, ExerciseId={ExerciseId}, Status={Status}, TrackingEnabled={TrackingEnabled}.",
            auth.UserId,
            body.ExerciseId,
            response.Status,
            response.TrackingEnabled);

        return await CreateJsonAsync(request, HttpStatusCode.OK, response, cancellationToken);
    }

    [Function("ProgressSummary")]
    public async Task<HttpResponseData> SummaryAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/progress/summary")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Progress summary request received. Method={Method}, Path={Path}.",
            request.Method,
            request.Url.AbsolutePath);

        var auth = _authenticationService.Authenticate(request);
        if (!auth.IsAuthenticated || string.IsNullOrWhiteSpace(auth.UserId))
        {
            _logger.LogWarning(
                "Progress summary request rejected as unauthorized. Path={Path}.",
                request.Url.AbsolutePath);

            return await CreateJsonAsync(request, HttpStatusCode.Unauthorized, new { Error = "unauthorized" }, cancellationToken);
        }

        var response = await _progressTrackingService.GetSummaryAsync(auth.UserId, cancellationToken);

        _logger.LogInformation(
            "Progress summary request processed. UserId={UserId}, TotalItems={TotalItems}, InProgressCount={InProgressCount}, CompletedCount={CompletedCount}, TotalAttempts={TotalAttempts}.",
            auth.UserId,
            response.TotalItems,
            response.InProgressCount,
            response.CompletedCount,
            response.TotalAttempts);

        return await CreateJsonAsync(request, HttpStatusCode.OK, response, cancellationToken);
    }

    [Function("ProgressState")]
    public async Task<HttpResponseData> StateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/progress/state")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Progress state request received. Method={Method}, Path={Path}.",
            request.Method,
            request.Url.AbsolutePath);

        var auth = _authenticationService.Authenticate(request);
        if (!auth.IsAuthenticated || string.IsNullOrWhiteSpace(auth.UserId))
        {
            _logger.LogWarning(
                "Progress state request rejected as unauthorized. Path={Path}.",
                request.Url.AbsolutePath);

            return await CreateJsonAsync(request, HttpStatusCode.Unauthorized, new { Error = "unauthorized" }, cancellationToken);
        }

        var query = QueryHelpers.ParseQuery(request.Url.Query);
        if (!query.TryGetValue("exerciseId", out var exerciseIdValue)
            || !int.TryParse(exerciseIdValue, out var exerciseId)
            || exerciseId <= 0)
        {
            _logger.LogWarning(
                "Progress state request rejected due to invalid query string. UserId={UserId}, ExerciseIdValue={ExerciseIdValue}.",
                auth.UserId,
                exerciseIdValue.ToString());

            return await CreateJsonAsync(request, HttpStatusCode.BadRequest, new { Error = "exercise_id_invalid" }, cancellationToken);
        }

        var response = await _progressTrackingService.GetStateAsync(auth.UserId, exerciseId, cancellationToken);

        _logger.LogInformation(
            "Progress state request processed. UserId={UserId}, ExerciseId={ExerciseId}, HasServerState={HasServerState}, TrackingEnabled={TrackingEnabled}.",
            auth.UserId,
            exerciseId,
            response.HasServerState,
            response.TrackingEnabled);

        return await CreateJsonAsync(request, HttpStatusCode.OK, response, cancellationToken);
    }

    [Function("ProgressItems")]
    public async Task<HttpResponseData> ItemsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/progress/items")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Progress items request received. Method={Method}, Path={Path}.",
            request.Method,
            request.Url.AbsolutePath);

        var auth = _authenticationService.Authenticate(request);
        if (!auth.IsAuthenticated || string.IsNullOrWhiteSpace(auth.UserId))
        {
            _logger.LogWarning(
                "Progress items request rejected as unauthorized. Path={Path}.",
                request.Url.AbsolutePath);

            return await CreateJsonAsync(request, HttpStatusCode.Unauthorized, new { Error = "unauthorized" }, cancellationToken);
        }

        var response = await _progressTrackingService.GetItemsAsync(auth.UserId, cancellationToken);

        _logger.LogInformation(
            "Progress items request processed. UserId={UserId}, ItemCount={ItemCount}.",
            auth.UserId,
            response.Count);

        return await CreateJsonAsync(request, HttpStatusCode.OK, response, cancellationToken);
    }

    [Function("ProgressAttempts")]
    public async Task<HttpResponseData> AttemptsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/progress/attempts")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Progress attempts request received. Method={Method}, Path={Path}.",
            request.Method,
            request.Url.AbsolutePath);

        var auth = _authenticationService.Authenticate(request);
        if (!auth.IsAuthenticated || string.IsNullOrWhiteSpace(auth.UserId))
        {
            _logger.LogWarning(
                "Progress attempts request rejected as unauthorized. Path={Path}.",
                request.Url.AbsolutePath);

            return await CreateJsonAsync(request, HttpStatusCode.Unauthorized, new { Error = "unauthorized" }, cancellationToken);
        }

        var query = QueryHelpers.ParseQuery(request.Url.Query);
        int? exerciseId = null;
        if (query.TryGetValue("exerciseId", out var exerciseIdValue)
            && int.TryParse(exerciseIdValue, out var parsedExerciseId))
        {
            exerciseId = parsedExerciseId;
        }

        var response = await _progressTrackingService.GetAttemptsAsync(auth.UserId, exerciseId, cancellationToken);

        _logger.LogInformation(
            "Progress attempts request processed. UserId={UserId}, ExerciseId={ExerciseId}, AttemptCount={AttemptCount}.",
            auth.UserId,
            exerciseId,
            response.Count);

        return await CreateJsonAsync(request, HttpStatusCode.OK, response, cancellationToken);
    }

    [Function("ProgressStateSave")]
    public async Task<HttpResponseData> SaveStateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users/progress/state")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Progress save-state request received. Method={Method}, Path={Path}.",
            request.Method,
            request.Url.AbsolutePath);

        var auth = _authenticationService.Authenticate(request);
        if (!auth.IsAuthenticated || string.IsNullOrWhiteSpace(auth.UserId))
        {
            _logger.LogWarning(
                "Progress save-state request rejected as unauthorized. Path={Path}.",
                request.Url.AbsolutePath);

            return await CreateJsonAsync(request, HttpStatusCode.Unauthorized, new { Error = "unauthorized" }, cancellationToken);
        }

        if (!IsRequestFromAllowedOrigin(request, _corsOptions.CurrentValue.AllowedOrigins))
        {
            _logger.LogWarning(
                "Progress save-state request rejected by origin validation. UserId={UserId}, Origin={Origin}, Referer={Referer}.",
                auth.UserId,
                ReadSingleHeader(request, "Origin"),
                ReadSingleHeader(request, "Referer"));

            return await CreateJsonAsync(request, HttpStatusCode.Forbidden, new { Error = "csrf_origin_invalid" }, cancellationToken);
        }

        var body = await request.ReadFromJsonAsync<SaveProgressStateRequest>(cancellationToken);
        if (body is null || body.ExerciseId <= 0)
        {
            _logger.LogWarning(
                "Progress save-state request rejected due to invalid payload. UserId={UserId}, HasPayload={HasPayload}, ExerciseId={ExerciseId}.",
                auth.UserId,
                body is not null,
                body?.ExerciseId);

            return await CreateJsonAsync(request, HttpStatusCode.BadRequest, new { Error = "exercise_id_invalid" }, cancellationToken);
        }

        var response = await _progressTrackingService.SaveStateAsync(auth.UserId, body, cancellationToken);

        _logger.LogInformation(
            "Progress save-state request processed. UserId={UserId}, ExerciseId={ExerciseId}, Status={Status}, TrackingEnabled={TrackingEnabled}.",
            auth.UserId,
            body.ExerciseId,
            response.Status,
            response.TrackingEnabled);

        return await CreateJsonAsync(request, HttpStatusCode.OK, response, cancellationToken);
    }

    [Function("UsersProgressHealth")]
    public async Task<HttpResponseData> HealthAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Progress health request received. Method={Method}, Path={Path}.",
            request.Method,
            request.Url.AbsolutePath);

        return await CreateJsonAsync(request, HttpStatusCode.OK, new { status = "healthy" }, cancellationToken);
    }

    private static bool IsRequestFromAllowedOrigin(HttpRequestData request, IReadOnlyCollection<string>? configuredOrigins)
    {
        var allowedOrigins = (configuredOrigins ?? [])
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (allowedOrigins.Count == 0)
        {
            return false;
        }

        var originHeader = ReadSingleHeader(request, "Origin");
        if (!string.IsNullOrWhiteSpace(originHeader))
        {
            return IsAllowedOrigin(originHeader, allowedOrigins);
        }

        var refererHeader = ReadSingleHeader(request, "Referer");
        if (!string.IsNullOrWhiteSpace(refererHeader))
        {
            return IsAllowedOrigin(refererHeader, allowedOrigins);
        }

        return false;
    }

    private static bool IsAllowedOrigin(string uriLikeHeaderValue, HashSet<string> allowedOrigins)
    {
        if (!Uri.TryCreate(uriLikeHeaderValue, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var normalizedOrigin = $"{uri.Scheme}://{uri.Authority}".TrimEnd('/');
        return allowedOrigins.Contains(normalizedOrigin);
    }

    private static string? ReadSingleHeader(HttpRequestData request, string headerName)
    {
        if (!request.Headers.TryGetValues(headerName, out var values))
        {
            return null;
        }

        return values.FirstOrDefault();
    }

    private static async Task<HttpResponseData> CreateJsonAsync(
        HttpRequestData request,
        HttpStatusCode statusCode,
        object payload,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(payload, cancellationToken);
        return response;
    }
}
