using Bogus;
using FluentResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.Core;
using WriteFluency.Application.Propositions.Interfaces;
using WriteFluency.Common;
using WriteFluency.Data;
using WriteFluency.Propositions;
using WriteFluency.TextComparisons;

namespace WriteFluency.Application;

public class ApplicationTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    protected readonly Dictionary<Guid, byte[]> UploadedFiles = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;

    protected ApplicationTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();

        services.AddLogging();

        services.AddDbContext<IAppDbContext, AppDbContext>(opts =>
            opts.UseSqlite(_connection));

        services.AddTransient<CreatePropositionService>();
        services.AddTransient<DailyPropositionGenerator>();

        ConfigureMocks(services);

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();

        var context = (AppDbContext)_scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        context.Database.EnsureCreated();
    }

    protected T GetService<T>() where T : class
    {
        return _scope.ServiceProvider.GetRequiredService<T>();
    }

    private void ConfigureMocks(IServiceCollection services)
    {
        var faker = new Faker();

        var newsClientMock = Substitute.For<INewsClient>();
        newsClientMock.GetNewsAsync(Arg.Any<SubjectEnum>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(NewsDtoFaker.Generate(3)));
        services.AddSingleton(newsClientMock);

        var articleExtractorMock = Substitute.For<IArticleExtractor>();
        articleExtractorMock.DownloadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(Array.Empty<byte>()));
        articleExtractorMock.GetVisibleTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(faker.Lorem.Paragraph(3000)));
        services.AddSingleton(articleExtractorMock);

        var generativeAIClientMock = Substitute.For<IGenerativeAIClient>();
        generativeAIClientMock.GenerateTextAsync(Arg.Any<ComplexityEnum>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new AIGeneratedTextDto(faker.Lorem.Paragraph(10), faker.Lorem.Paragraph(3000))));
        generativeAIClientMock.GenerateAudioAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new AudioDto(faker.Random.Bytes(1000), faker.Random.Guid().ToString())));
        services.AddSingleton(generativeAIClientMock);

        var fileServiceMock = Substitute.For<IFileService>();
        
        Result<string> UploadBehavior(CallInfo x)
        {
            var fileId = Guid.NewGuid();
            UploadedFiles[fileId] = (byte[])x[1];
            return Result.Ok(fileId.ToString());
        }

        fileServiceMock
            .UploadFileAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UploadBehavior);

        fileServiceMock
            .UploadFileAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UploadBehavior);

        services.AddSingleton(fileServiceMock);

        var propositionOptions = new PropositionOptions
        {
            DailyRequestsLimit = 100,
            PropositionsLimitPerTopic = 300,
            NewsRequestLimit = 3,
            DailyRunCron = "0 0 * * *"
        };
        var optionsMonitor = Substitute.For<IOptionsMonitor<PropositionOptions>>();
        optionsMonitor.CurrentValue.Returns(propositionOptions);
        services.AddSingleton(optionsMonitor);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        _scope.Dispose();
    }
}