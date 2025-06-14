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

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<IAppDbContext, AppDbContext>(opts => opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddOptions<NewsOptions>().Bind(builder.Configuration.GetSection(NewsOptions.Section)).ValidateOnStart();
builder.Services.AddOptions<PropositionOptions>().Bind(builder.Configuration.GetSection(PropositionOptions.Section)).ValidateOnStart();
builder.Services.AddOptions<OpenAIOptions>().Bind(builder.Configuration.GetSection(OpenAIOptions.Section)).ValidateOnStart();

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

// Using new microsoft AI abstraction
builder.Services.AddChatClient(services =>
{
    return new ChatClientBuilder(new OpenAI.OpenAIClient(openAIOptions.Key).GetChatClient("gpt-4.1-nano").AsIChatClient())
        // TODO: Add logging and telemetry
        .Build();
}, ServiceLifetime.Scoped);

builder.Services.AddTransient<IArticleExtractor, ArticleExtractor>();
builder.Services.AddTransient<IFileService, FileService>();
builder.Services.AddTransient<CreatePropositionService>();
builder.Services.AddTransient<DailyPropositionGenerator>();

builder.Services.AddOptions<FileStorageOptions>().Bind(builder.Configuration.GetSection(FileStorageOptions.Section)).ValidateOnStart();

var fileStorageOptions = builder.Configuration.GetSection(FileStorageOptions.Section).Get<FileStorageOptions>();
ArgumentNullException.ThrowIfNull(fileStorageOptions);
builder.Services.AddMinio(options =>
    options.WithEndpoint(fileStorageOptions.Endpoint, 9000)
    .WithCredentials(fileStorageOptions.AccessKey, fileStorageOptions.SecretKey)
    .Build());

builder.Services.AddHostedService<NewsWorker>();

var host = builder.Build();
host.Run();
