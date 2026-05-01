using Microsoft.AspNetCore.HttpOverrides;
using WriteFluency.Users.WebApi.Authentication;
using WriteFluency.Users.WebApi.Configuration;
using WriteFluency.Users.WebApi.Support;

var builder = WebApplication.CreateBuilder(args);
const string WebappCorsPolicy = "WebappCors";

builder.AddServiceDefaults();
builder.AddRedisClient(
    "wf-infra-redis",
    configureOptions: options =>
    {
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 5;
        options.ConnectTimeout = 5000;
        options.SyncTimeout = 5000;
    });
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddUsersPersistence(builder.Configuration, builder.Environment.IsProduction());
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Kubernetes ingress proxy addresses are dynamic, so trust the forwarded chain and limit exposure at network/ingress level.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = null;
});

var configuredCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var corsOrigins = configuredCorsOrigins
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
var allowedOriginSet = corsOrigins.ToHashSet(StringComparer.OrdinalIgnoreCase);

builder.Services.AddCors(options =>
{
    options.AddPolicy(WebappCorsPolicy, policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseForwardedHeaders();

var usersApi = app.MapGroup("/users");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/users/openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/users/openapi/v1.json", "WriteFluency Users API");
        options.RoutePrefix = "users/swagger";
    });
}

if (!app.Environment.IsEnvironment("Testing"))
{
    if (app.Environment.IsProduction())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
}

app.UseCors(WebappCorsPolicy);
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (!IsStateChangingUsersRequest(context.Request))
    {
        await next();
        return;
    }

    if (IsRequestFromAllowedOrigin(context.Request, allowedOriginSet))
    {
        await next();
        return;
    }

    context.Response.StatusCode = StatusCodes.Status403Forbidden;
    await context.Response.WriteAsJsonAsync(new
    {
        Error = "csrf_origin_invalid"
    });
});
app.UseAuthorization();

usersApi.MapAuthEndpoints();
usersApi.MapSupportRequestEndpoints();
app.MapDefaultEndpoints();

app.Run();

static bool IsStateChangingUsersRequest(HttpRequest request)
{
    if (!request.Path.StartsWithSegments("/users", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return HttpMethods.IsPost(request.Method)
        || HttpMethods.IsPut(request.Method)
        || HttpMethods.IsPatch(request.Method)
        || HttpMethods.IsDelete(request.Method);
}

static bool IsRequestFromAllowedOrigin(HttpRequest request, HashSet<string> allowedOrigins)
{
    var originHeader = request.Headers.Origin.ToString();
    if (!string.IsNullOrWhiteSpace(originHeader))
    {
        return IsAllowedOrigin(originHeader, allowedOrigins);
    }

    var refererHeader = request.Headers.Referer.ToString();
    if (!string.IsNullOrWhiteSpace(refererHeader))
    {
        return IsAllowedOrigin(refererHeader, allowedOrigins);
    }

    return false;
}

static bool IsAllowedOrigin(string uriLikeHeaderValue, HashSet<string> allowedOrigins)
{
    if (!Uri.TryCreate(uriLikeHeaderValue, UriKind.Absolute, out var uri))
    {
        return false;
    }

    var normalizedOrigin = $"{uri.Scheme}://{uri.Authority}".TrimEnd('/');
    return allowedOrigins.Contains(normalizedOrigin);
}

public partial class Program;
