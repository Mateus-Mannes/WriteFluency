namespace WriteFluency.Users.WebApi.Email;

public interface IAppEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
