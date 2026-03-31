using Microsoft.AspNetCore.Identity;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.WebApi.Email;

public class IdentityEmailSender : IEmailSender<ApplicationUser>
{
    private readonly IAppEmailSender _emailSender;

    public IdentityEmailSender(IAppEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var content = EmailTemplateBuilder.BuildConfirmationEmail(confirmationLink);

        return _emailSender.SendAsync(email, "Confirm your WriteFluency email", content.HtmlBody, content.TextBody);
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var content = EmailTemplateBuilder.BuildPasswordResetLinkEmail(resetLink);

        return _emailSender.SendAsync(email, "Reset your WriteFluency password", content.HtmlBody, content.TextBody);
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var content = EmailTemplateBuilder.BuildPasswordResetCodeEmail(resetCode);

        return _emailSender.SendAsync(email, "Your WriteFluency reset code", content.HtmlBody, content.TextBody);
    }
}
