namespace WriteFluency.Users.WebApi.Email;

public static class EmailTemplateBuilder
{
    public static EmailContent BuildConfirmationEmail(string confirmationLink)
    {
        var contentHtml = $"""
                           <p style="margin:0 0 16px 0;">Please confirm your WriteFluency account to start tracking your progress and keep your learning data safe.</p>
                           {BuildPrimaryButton(confirmationLink, "Confirm my email")}
                           <p style="margin:0;color:#4B5563;font-size:14px;line-height:1.5;">
                             If the button does not work, copy and paste this link in your browser:<br />
                             <a href="{confirmationLink}" style="color:#3A7DFF;word-break:break-all;">{confirmationLink}</a>
                           </p>
                           """;

        var contentText = $"""
                          Please confirm your WriteFluency account to start tracking your progress and keep your learning data safe.

                          Confirm your email:
                          {confirmationLink}
                          """;

        return BuildLayout(
            title: "Confirm your email",
            subtitle: "One quick step to activate your account",
            contentHtml: contentHtml,
            contentText: contentText);
    }

    public static EmailContent BuildPasswordResetLinkEmail(string resetLink)
    {
        var contentHtml = $"""
                           <p style="margin:0 0 16px 0;">We received a request to reset your WriteFluency password.</p>
                           {BuildPrimaryButton(resetLink, "Reset password")}
                           <p style="margin:0;color:#4B5563;font-size:14px;line-height:1.5;">
                             If you did not request this, you can ignore this email.
                           </p>
                           """;

        var contentText = $"""
                          We received a request to reset your WriteFluency password.

                          Reset your password:
                          {resetLink}

                          If you did not request this, you can ignore this email.
                          """;

        return BuildLayout(
            title: "Reset your password",
            subtitle: "Secure your account access",
            contentHtml: contentHtml,
            contentText: contentText);
    }

    public static EmailContent BuildPasswordResetCodeEmail(string resetCode)
    {
        var contentHtml = $"""
                           <p style="margin:0 0 14px 0;">Use this code to reset your WriteFluency password:</p>
                           <p style="margin:0 0 20px 0;font-size:30px;line-height:1;">
                             <span style="letter-spacing:4px;background:#E8F0FF;border:1px solid #B8CEFF;color:#1A1A1A;border-radius:10px;padding:12px 16px;display:inline-block;"><strong>{resetCode}</strong></span>
                           </p>
                           <p style="margin:0;color:#4B5563;font-size:14px;">Do not share this code with anyone.</p>
                           """;

        var contentText = $"""
                          Use this code to reset your WriteFluency password:

                          {resetCode}

                          Do not share this code with anyone.
                          """;

        return BuildLayout(
            title: "Password reset code",
            subtitle: "Use this code in the reset form",
            contentHtml: contentHtml,
            contentText: contentText);
    }

    public static EmailContent BuildPasswordlessOtpEmail(string code)
    {
        var contentHtml = $"""
                           <p style="margin:0 0 14px 0;">Your WriteFluency verification code is:</p>
                           <p style="margin:0 0 20px 0;font-size:34px;line-height:1;">
                             <span style="letter-spacing:6px;background:#E8F0FF;border:1px solid #B8CEFF;color:#1A1A1A;border-radius:10px;padding:12px 16px;display:inline-block;"><strong>{code}</strong></span>
                           </p>
                           <p style="margin:0 0 8px 0;color:#4B5563;font-size:14px;">Use this code to sign in or create your account.</p>
                           <p style="margin:0;color:#4B5563;font-size:14px;">This code expires in 10 minutes and can only be used once.</p>
                           """;

        var contentText = $"""
                          Your WriteFluency verification code is:

                          {code}

                          Use this code to sign in or create your account.
                          This code expires in 10 minutes and can only be used once.
                          """;

        return BuildLayout(
            title: "Your sign-in code",
            subtitle: "Passwordless access to WriteFluency",
            contentHtml: contentHtml,
            contentText: contentText);
    }

    private static EmailContent BuildLayout(string title, string subtitle, string contentHtml, string contentText)
    {
        var htmlBody = $$"""
                         <!doctype html>
                         <html lang="en">
                         <head>
                           <meta charset="utf-8" />
                           <meta name="viewport" content="width=device-width, initial-scale=1" />
                           <title>{{title}}</title>
                         </head>
                         <body style="margin:0;padding:0;background-color:#F7FAFC;font-family:'Segoe UI',Arial,sans-serif;color:#1A1A1A;">
                           <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" bgcolor="#F7FAFC" style="background-color:#F7FAFC;padding:28px 12px;">
                             <tr>
                               <td align="center">
                                 <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="max-width:620px;">
                                   <tr>
                                     <td style="padding:0 0 14px 0;font-size:13px;color:#6B7280;text-align:left;">WriteFluency</td>
                                   </tr>
                                   <tr>
                                     <td bgcolor="#ffffff" style="background-color:#ffffff;border:1px solid #E5E7EB;border-radius:16px;overflow:hidden;">
                                       <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%">
                                         <tr>
                                           <td bgcolor="#3A7DFF" style="padding:18px 22px;background-color:#3A7DFF;">
                                             <p style="margin:0;font-size:13px;color:#DDEAFF;letter-spacing:.4px;text-transform:uppercase;font-weight:700;">WriteFluency</p>
                                             <h1 style="margin:6px 0 0 0;font-size:28px;line-height:1.2;color:#ffffff;">{{title}}</h1>
                                             <p style="margin:8px 0 0 0;font-size:16px;line-height:1.4;color:#EAF2FF;">{{subtitle}}</p>
                                           </td>
                                         </tr>
                                         <tr>
                                           <td style="padding:24px 22px;font-size:16px;line-height:1.55;color:#1A1A1A;">
                                             <p style="margin:0 0 16px 0;color:#1A1A1A;">Hi,</p>
                                             {{contentHtml}}
                                           </td>
                                         </tr>
                                       </table>
                                     </td>
                                   </tr>
                                   <tr>
                                     <td style="padding:14px 6px 0 6px;text-align:center;font-size:12px;color:#6B7280;">
                                       This email was sent by WriteFluency. If this was not you, you can safely ignore it.
                                     </td>
                                   </tr>
                                 </table>
                               </td>
                             </tr>
                           </table>
                         </body>
                         </html>
                         """;

        var textBody = $$"""
                         WriteFluency
                         {{title}}
                         {{subtitle}}

                         Hi,

                         {{contentText}}

                         This email was sent by WriteFluency. If this was not you, you can safely ignore it.
                         """;

        return new EmailContent(htmlBody, textBody.Trim());
    }

    private static string BuildPrimaryButton(string url, string label)
    {
        return $"""
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 24px 0;">
                  <tr>
                    <td bgcolor="#3A7DFF" style="background-color:#3A7DFF;border-radius:10px;">
                      <a href="{url}" style="display:inline-block;padding:12px 20px;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;">{label}</a>
                    </td>
                  </tr>
                </table>
                """;
    }
}
