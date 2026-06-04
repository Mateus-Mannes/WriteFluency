using System.Net.Http.Json;

namespace WriteFluency.WebApi.Users;

public class UsersSessionClient : IUsersSessionClient
{
    private const string SessionPath = "auth/session";
    private const string CookieHeaderName = "Cookie";

    private readonly HttpClient _httpClient;
    private readonly ILogger<UsersSessionClient> _logger;

    public UsersSessionClient(HttpClient httpClient, ILogger<UsersSessionClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> IsProAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, SessionPath);
            if (request.Headers.TryGetValue(CookieHeaderName, out var cookieHeader) && cookieHeader.Count > 0)
            {
                sessionRequest.Headers.Add(CookieHeaderName, cookieHeader.ToArray());
            }

            using var response = await _httpClient.SendAsync(sessionRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var session = await response.Content.ReadFromJsonAsync<UsersSessionResponse>(cancellationToken);
            return session?.IsPro == true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to resolve users-service session for proposition access check.");
            return false;
        }
    }

    private record UsersSessionResponse(bool IsPro);
}
