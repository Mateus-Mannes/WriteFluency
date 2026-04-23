using System.Security.Claims;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.WebApi.Authentication;

internal sealed class LoginActivityRecorder : ILoginActivityRecorder
{
    private readonly UsersDbContext _dbContext;
    private readonly ILoginGeoLookupService _geoLookupService;
    private readonly ILogger<LoginActivityRecorder> _logger;

    public LoginActivityRecorder(
        UsersDbContext dbContext,
        ILoginGeoLookupService geoLookupService,
        ILogger<LoginActivityRecorder> logger)
    {
        _dbContext = dbContext;
        _geoLookupService = geoLookupService;
        _logger = logger;
    }

    public async Task RecordIfApplicableAsync(
        HttpContext httpContext,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (!LoginAuthContextResolver.TryResolve(httpContext.Request, out var authContext))
        {
            return;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Could not record login location because authenticated principal has no user identifier claim.");
            return;
        }

        var ipAddress = httpContext.Connection.RemoteIpAddress;
        var geoLookup = _geoLookupService.Lookup(ipAddress);
        var activity = new UserLoginActivity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            AuthMethod = authContext.AuthMethod,
            AuthProvider = authContext.AuthProvider,
            IpAddress = ipAddress?.ToString(),
            CountryIsoCode = geoLookup.CountryIsoCode,
            CountryName = geoLookup.CountryName,
            City = geoLookup.City,
            GeoLookupStatus = geoLookup.GeoLookupStatus
        };

        try
        {
            _dbContext.UserLoginActivities.Add(activity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist login activity for user {UserId} and method {AuthMethod}.",
                activity.UserId,
                activity.AuthMethod);
            return;
        }

        _logger.LogInformation(
            "User login activity recorded. UserId={UserId} AuthMethod={AuthMethod} AuthProvider={AuthProvider} IpAddress={IpAddress} CountryIsoCode={CountryIsoCode} CountryName={CountryName} City={City} GeoLookupStatus={GeoLookupStatus}",
            activity.UserId,
            activity.AuthMethod,
            activity.AuthProvider,
            activity.IpAddress,
            activity.CountryIsoCode,
            activity.CountryName,
            activity.City,
            activity.GeoLookupStatus);
    }
}
