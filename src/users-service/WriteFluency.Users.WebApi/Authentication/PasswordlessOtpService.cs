using Microsoft.AspNetCore.Identity;
using WriteFluency.Users.WebApi.Data;
using WriteFluency.Users.WebApi.Email;

namespace WriteFluency.Users.WebApi.Authentication;

public class PasswordlessOtpService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly PasswordlessOtpStore _otpStore;
    private readonly IAppEmailSender _emailSender;
    private readonly ILogger<PasswordlessOtpService> _logger;

    public PasswordlessOtpService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        PasswordlessOtpStore otpStore,
        IAppEmailSender emailSender,
        ILogger<PasswordlessOtpService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _otpStore = otpStore;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task RequestOtpAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = _userManager.NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return;
        }

        if (!await _otpStore.CanRequestAsync(normalizedEmail, ipAddress))
        {
            return;
        }

        var user = await EnsureUserForPasswordlessAsync(email);
        if (user is null)
        {
            return;
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return;
        }

        var code = await _otpStore.IssueCodeAsync(normalizedEmail);

        var content = EmailTemplateBuilder.BuildPasswordlessOtpEmail(code);

        await _emailSender.SendAsync(email, "Your WriteFluency sign-in code", content.HtmlBody, content.TextBody, cancellationToken);
        _logger.LogInformation("Passwordless OTP issued for {NormalizedEmail}", normalizedEmail);
    }

    public async Task<bool> VerifyOtpAndSignInAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return false;
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return false;
        }

        var normalizedEmail = _userManager.NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return false;
        }

        var validCode = await _otpStore.ValidateCodeAsync(normalizedEmail, code);
        if (!validCode)
        {
            return false;
        }

        if (!await _userManager.IsEmailConfirmedAsync(user))
        {
            var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmResult = await _userManager.ConfirmEmailAsync(user, confirmToken);
            if (!confirmResult.Succeeded)
            {
                return false;
            }
        }

        await _signInManager.SignInAsync(user, isPersistent: false, authenticationMethod: "passwordless_email_otp");
        _logger.LogInformation("Passwordless OTP sign-in succeeded for {NormalizedEmail}", normalizedEmail);
        return true;
    }

    private async Task<ApplicationUser?> EnsureUserForPasswordlessAsync(string email)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return existingUser;
        }

        var newUser = new ApplicationUser
        {
            UserName = email,
            Email = email
        };

        var createResult = await _userManager.CreateAsync(newUser);
        if (createResult.Succeeded)
        {
            return newUser;
        }

        _logger.LogWarning("Failed to create passwordless user for {Email}. Errors: {Errors}",
            email,
            string.Join(", ", createResult.Errors.Select(e => $"{e.Code}:{e.Description}")));
        return null;
    }
}
