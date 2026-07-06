namespace WriteFluency.WebApi.Users;

public interface IUsersSessionClient
{
    Task<UsersSession> GetSessionAsync(HttpRequest request, CancellationToken cancellationToken = default);

    Task<bool> IsProAsync(HttpRequest request, CancellationToken cancellationToken = default);
}
