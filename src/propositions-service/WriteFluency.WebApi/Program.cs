using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using WriteFluency.Authentication;
using WriteFluency.Data;
using WriteFluency.Infrastructure.ExternalApis;
using WriteFluency.Propositions;
using WriteFluency.TextComparisons;
using WriteFluency.WebApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddAppSwagger();

builder.AddAppAuthentication();

// Options configuration
builder.Services.AddOptions<OpenAIOptions>().Bind(builder.Configuration.GetSection(OpenAIOptions.Section)).ValidateOnStart();
builder.Services.AddOptions<TextToSpeechOptions>().Bind(builder.Configuration.GetSection(TextToSpeechOptions.Section)).ValidateOnStart();
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection(JwtOptions.Section)).ValidateOnStart();

// Adds the database context and identity configuration
builder.Services.AddDbContext<IAppDbContext, AppDbContext>(opts => opts.UseNpgsql(builder.Configuration.GetConnectionString("postgresdb")));
builder.Services.AddIdentityCore<IdentityUser>()
    .AddApiEndpoints()
    .AddEntityFrameworkStores<AppDbContext>();

builder.EnrichNpgsqlDbContext<AppDbContext>(
    configureSettings: settings =>
    {
        settings.DisableRetry = false;
        settings.CommandTimeout = 30;
    });

builder.Services.AddTransient<LevenshteinDistanceService>();
builder.Services.AddTransient<TokenAlignmentService>();
builder.Services.AddTransient<TokenizeTextService>();
builder.Services.AddTransient<NeedlemanWunschAlignmentService>();
builder.Services.AddTransient<TextComparisonService>();
builder.Services.AddTransient<TextAlignmentService>();
builder.Services.AddTransient<TokenComparisonService>();

builder.Services.AddTransient<JwtTokenService>();

// Adding http clients
var openAIOptions = builder.Configuration.GetSection(OpenAIOptions.Section).Get<OpenAIOptions>();
ArgumentNullException.ThrowIfNull(openAIOptions);

builder.Services.AddHttpClient<IGenerativeAIClient, OpenAIClient>(client =>
{
    client.BaseAddress = new Uri(openAIOptions.BaseAddress);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAIOptions.Key);
});

// Using new microsoft AI abstraction
builder.Services.AddChatClient(services =>
{
    return new ChatClientBuilder(new OpenAI.OpenAIClient(openAIOptions.Key).GetChatClient("gpt-4.1-nano").AsIChatClient())
        .UseFunctionInvocation()
        // TODO: Add logging and telemetry
        .Build();
}, ServiceLifetime.Scoped);

builder.Services.AddHttpClient<ITextToSpeechClient, TextToSpeechClient>(client =>
{
    var options = builder.Configuration.GetSection(TextToSpeechOptions.Section).Get<TextToSpeechOptions>();
    ArgumentNullException.ThrowIfNull(options);
    client.BaseAddress = new Uri(options.BaseAddress);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Add(options.KeyName, options.Key);
});

builder.Services.AddCors();

builder.AddMinioClient("minio", configureSettings: options =>
{
    options.Endpoint = new Uri(options.Endpoint!.OriginalString!.Replace("localhost", "127.0.0.1"));
    options.UseSsl = false;
});
builder.AddMinioHealthChecks();

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache();

var app = builder.Build();

var clients = app.Configuration.GetValue<string>("AllowedClients")?.Split(',');
app.UseCors(opts =>
{
    if (clients![0] != "*")
        opts.WithOrigins(clients)
        .AllowAnyMethod()
        .AllowAnyHeader();
    else opts.AllowAnyOrigin();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // Configures the swagger ui to use the google login
        c.OAuthClientId(builder.Configuration["Authentication:Google:ClientId"]);
        c.OAuthClientSecret(builder.Configuration["Authentication:Google:ClientSecret"]);
        c.OAuthUsePkce();
        c.OAuthScopes("openid", "profile", "email");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseRequestTimeouts();
app.UseOutputCache();
app.MapDefaultEndpoints();

app.MapControllers();

app.Run();
