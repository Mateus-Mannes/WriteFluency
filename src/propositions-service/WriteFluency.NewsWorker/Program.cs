using System.Net.Http.Headers;
using Minio;
using WriteFluency.Common;
using WriteFluency.Application.Propositions.Interfaces;
using WriteFluency.Infrastructure.ExternalApis;
using WriteFluency.Infrastructure.FileStorage;
using WriteFluency.Infrastructure.Http;
using WriteFluency.NewsWorker;
using WriteFluency.Propositions;
using WriteFluency.TextComparisons;
using Microsoft.EntityFrameworkCore;
using WriteFluency.Data;
using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<IAppDbContext, AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("wf-postgresdb")));

builder.EnrichNpgsqlDbContext<AppDbContext>(
    configureSettings: settings =>
    {
        settings.DisableRetry = false;
        settings.CommandTimeout = 30;
    });

builder.Services.AddOptions<NewsOptions>().Bind(builder.Configuration.GetSection(NewsOptions.Section)).ValidateOnStart();
builder.Services.AddOptions<PropositionOptions>().Bind(builder.Configuration.GetSection(PropositionOptions.Section)).ValidateOnStart();
builder.Services.AddOptions<OpenAIOptions>().Bind(builder.Configuration.GetSection(OpenAIOptions.Section)).ValidateOnStart();
builder.Services.AddOptions<CloudflareOptions>().Bind(builder.Configuration.GetSection(CloudflareOptions.Section));

builder.Services.AddOptions<TextToSpeechOptions>().Bind(builder.Configuration.GetSection(TextToSpeechOptions.Section)).ValidateOnStart();
builder.Services.AddScoped<ITextToSpeechClient, TextToSpeechClient>();

builder.Services.AddHttpClient<INewsClient, NewsClient>(client =>
{
    var options = builder.Configuration.GetSection(NewsOptions.Section).Get<NewsOptions>();
    ArgumentNullException.ThrowIfNull(options);
    client.BaseAddress = new Uri(options.BaseAddress);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

var openAIOptions = builder.Configuration.GetSection(OpenAIOptions.Section).Get<OpenAIOptions>();
ArgumentNullException.ThrowIfNull(openAIOptions);

builder.Services.AddHttpClient<IGenerativeAIClient, OpenAIClient>(client =>
{
    client.BaseAddress = new Uri(openAIOptions.BaseAddress);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAIOptions.Key);
});

builder.Services.AddHttpClient<ICloudflareCachePurgeClient, CloudflareCachePurgeClient>(client =>
{
    var options = builder.Configuration.GetSection(CloudflareOptions.Section).Get<CloudflareOptions>() ?? new CloudflareOptions();
    var baseAddress = options.BaseAddress?.Trim();

    if (!string.IsNullOrWhiteSpace(baseAddress) && !baseAddress.EndsWith('/'))
    {
        baseAddress += "/";
    }

    if (Uri.TryCreate(baseAddress, UriKind.Absolute, out var baseUri))
    {
        client.BaseAddress = baseUri;
    }
    else
    {
        client.BaseAddress = new Uri("https://api.cloudflare.com/client/v4/");
    }

    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient("cache-warmup", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WriteFluency-NewsWorker/1.0");
});

// Using new microsoft AI abstraction
builder.Services.AddChatClient(services =>
{
    return new ChatClientBuilder(new OpenAI.OpenAIClient(openAIOptions.Key).GetChatClient("gpt-4.1-nano").AsIChatClient())
        .UseFunctionInvocation()
        // TODO: Add logging and telemetry
        .Build();
}, ServiceLifetime.Scoped);

builder.Services.AddTransient<IArticleExtractor, ArticleExtractor>();
builder.Services.AddTransient<IFileService, FileService>();
builder.Services.AddTransient<CreatePropositionService>();
builder.Services.AddTransient<DailyPropositionGenerator>();

builder.AddMinioClient("wf-minio", configureSettings: options =>
{
    options.Endpoint = new Uri(options.Endpoint!.OriginalString!.Replace("localhost", "127.0.0.1"));
    options.UseSsl = false;
});
builder.AddMinioHealthChecks();

builder.Services.AddHostedService<NewsWorker>();

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache();

var host = builder.Build();

host.UseRequestTimeouts();
host.UseOutputCache();
host.MapDefaultEndpoints();

host.Run();
