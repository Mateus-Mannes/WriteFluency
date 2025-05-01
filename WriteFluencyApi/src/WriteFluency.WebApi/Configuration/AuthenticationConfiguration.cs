using System.Text;
using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace WriteFluency.WebApi;

public static class AuthenticationConfiguration
{
    public static void AddAppAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication()
        .AddJwtBearer(GoogleOpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            // Sets the authority so the token can be validated through Google public keys from well-known/openid-configuration
            options.Authority = "https://accounts.google.com";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = new List<string> { "https://accounts.google.com", "accounts.google.com" },
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Authentication:Google:ClientId"],
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };
        })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
            };
        });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AppAuthorizeAttribute.AuthorizationPolicyName, policy =>
                policy.RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(
                        GoogleOpenIdConnectDefaults.AuthenticationScheme,
                        JwtBearerDefaults.AuthenticationScheme));
        });
    }
}
