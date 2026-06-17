using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using StackExchange.Redis;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.WebApi.Authentication;

public sealed class PasswordSetupService
{
    private static readonly TimeSpan SetupTokenTtl = TimeSpan.FromMinutes(30);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDatabase _redis;
    private readonly ILogger<PasswordSetupService> _logger;

    public PasswordSetupService(
        UserManager<ApplicationUser> userManager,
        IConnectionMultiplexer redis,
        ILogger<PasswordSetupService> logger)
    {
        _userManager = userManager;
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<string> CreateSetupTokenAsync(ApplicationUser user, string password)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var key = BuildSetupKey(token);
        var passwordHash = _userManager.PasswordHasher.HashPassword(user, password);

        await _redis.HashSetAsync(key, [
            new HashEntry("userId", user.Id),
            new HashEntry("passwordHash", passwordHash)
        ]);
        await _redis.KeyExpireAsync(key, SetupTokenTtl);

        return token;
    }

    public async Task<bool> ConfirmSetupAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var key = BuildSetupKey(token);
        var entries = await _redis.HashGetAllAsync(key);
        if (entries.Length == 0)
        {
            return false;
        }

        await _redis.KeyDeleteAsync(key);

        var values = entries.ToDictionary(entry => entry.Name.ToString(), entry => entry.Value.ToString());
        if (!values.TryGetValue("userId", out var userId)
            || !values.TryGetValue("passwordHash", out var passwordHash)
            || string.IsNullOrWhiteSpace(userId)
            || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            return true;
        }

        user.PasswordHash = passwordHash;
        user.EmailConfirmed = true;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            _logger.LogWarning(
                "Failed to confirm password setup for {UserId}. Errors: {Errors}",
                user.Id,
                string.Join(", ", updateResult.Errors.Select(error => $"{error.Code}:{error.Description}")));
            return false;
        }

        await _userManager.UpdateSecurityStampAsync(user);
        return true;
    }

    private static string BuildSetupKey(string token)
    {
        return $"auth:password-setup:{token}";
    }
}
