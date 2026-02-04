using Bogus;
using FluentResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using WriteFluency.Application.Propositions.Interfaces;
using WriteFluency.Common;
using WriteFluency.Data;
using WriteFluency.Propositions;
using WriteFluency.TextComparisons;

namespace WriteFluency.Application;

public class ApplicationTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    protected readonly Dictionary<string, byte[]> UploadedFiles = new();
    private static readonly byte[] SampleImageBytes = CreateSampleImageBytes();

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
        context.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
    }

    protected T GetService<T>() where T : class
    {
        return _scope.ServiceProvider.GetRequiredService<T>();
    }

    protected virtual void ConfigureMocks(IServiceCollection services, SubjectEnum? subjectWithoutNews = null)
    {
        var faker = new Faker();

        var newsClientMock = Substitute.For<INewsClient>();
        newsClientMock.GetNewsAsync(Arg.Any<SubjectEnum>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                if (x.Arg<SubjectEnum>() == subjectWithoutNews) return Result.Ok(Enumerable.Empty<NewsDto>());
                return Result.Ok(NewsDtoFaker.Generate(3));
            });
        services.AddSingleton(newsClientMock);

        var articleExtractorMock = Substitute.For<IArticleExtractor>();
        articleExtractorMock.DownloadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(SampleImageBytes));
        articleExtractorMock.GetVisibleTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(faker.Lorem.Paragraph(3000)));
        services.AddSingleton(articleExtractorMock);

        var generativeAIClientMock = Substitute.For<IGenerativeAIClient>();
        var textToSpeechClientMock = Substitute.For<ITextToSpeechClient>();
        generativeAIClientMock.GenerateTextAsync(Arg.Any<ComplexityEnum>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new AIGeneratedTextDto(faker.Lorem.Paragraph(10), faker.Lorem.Paragraph(3000))));
        generativeAIClientMock.ValidateImageAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true)); // By default, always return valid image
        textToSpeechClientMock.GenerateAudioAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new AudioDto(faker.Random.Bytes(1000), faker.Random.Guid().ToString(), 60)));
        services.AddSingleton(generativeAIClientMock);
        services.AddSingleton(textToSpeechClientMock);

        var fileServiceMock = Substitute.For<IFileService>();
        
        Result<string> UploadBehavior(CallInfo x)
        {
            var fileId = Guid.NewGuid().ToString();
            UploadedFiles[fileId] = (byte[])x[1];
            return Result.Ok(fileId);
        }

        fileServiceMock
            .UploadFileAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UploadBehavior);

        fileServiceMock
            .UploadFileAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UploadBehavior);

        fileServiceMock
            .UploadFileWithObjectNameAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var objectName = callInfo.ArgAt<string>(2);
                UploadedFiles[objectName] = callInfo.ArgAt<byte[]>(1);
                return Result.Ok(objectName);
            });

        services.AddSingleton(fileServiceMock);

        var propositionOptions = new PropositionOptions
        {
            DailyRequestsLimit = 30,
            PropositionsLimitPerTopic = 20,
            NewsRequestLimit = 3
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

    private static byte[] CreateSampleImageBytes()
    {
        using var image = new Image<Rgba32>(1280, 720, new Rgba32(40, 80, 120));
        using var outputStream = new MemoryStream();
        image.SaveAsJpeg(outputStream, new JpegEncoder { Quality = 80 });
        return outputStream.ToArray();
    }
}
