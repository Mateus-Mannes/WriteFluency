namespace WriteFluency.WebApi.Users;

public interface IUsersSessionClient
{
    Task<bool> IsProAsync(HttpRequest request, CancellationToken cancellationToken = default);
}
