using Shouldly;
using WriteFluency.Users.WebApi.Email;

namespace WriteFluency.Users.Tests.Email;

public class EmailTemplateBuilderTests
{
    [Fact]
    public void BuildConfirmationEmail_ShouldContainLinkInHtmlAndTextBodies()
    {
        const string link = "https://writefluency.com/auth/confirm-email?code=abc";

        var content = EmailTemplateBuilder.BuildConfirmationEmail(link);

        content.HtmlBody.ShouldContain(link);
        content.TextBody.ShouldContain(link);
        content.TextBody.ShouldContain("Confirm your email");
    }

    [Fact]
    public void BuildPasswordlessOtpEmail_ShouldContainCodeInHtmlAndTextBodies()
    {
        const string code = "123456";

        var content = EmailTemplateBuilder.BuildPasswordlessOtpEmail(code);

        content.HtmlBody.ShouldContain(code);
        content.TextBody.ShouldContain(code);
        content.TextBody.ShouldContain("expires in 10 minutes");
    }
}
