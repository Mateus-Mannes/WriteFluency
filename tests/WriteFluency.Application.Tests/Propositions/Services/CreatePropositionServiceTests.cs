using Bogus;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using WriteFluency.Application;
using WriteFluency.Application.Propositions.Interfaces;
using WriteFluency.Common;
using WriteFluency.TextComparisons;

namespace WriteFluency.Propositions;

public class CreatePropositionServiceTests : ApplicationTestBase
{
    private readonly CreatePropositionService _service;
    private IFileService _fileServiceMock = null!;
    private static readonly byte[] SampleImageBytes = CreateSampleImageBytes();

    public CreatePropositionServiceTests()
    {
        _service = GetService<CreatePropositionService>();
    }

    [Fact]
    public async Task ShouldUploadOriginalAndOptimizedVariants()
    {
        var dto = new CreatePropositionDto(DateTime.UtcNow.Date, ComplexityEnum.Beginner, SubjectEnum.Business);

        var result = await _service.CreatePropositionsAsync(dto, 1);

        result.Count().ShouldBe(1);
        var proposition = result.Single();
        proposition.ImageFileId.ShouldNotBeNull();

        var baseId = Path.GetFileNameWithoutExtension(proposition.ImageFileId);
        baseId.ShouldNotBeNullOrWhiteSpace();

        UploadedFiles.Keys.ShouldContain($"{baseId}_w320.webp");
        UploadedFiles.Keys.ShouldContain($"{baseId}_w512.webp");
        UploadedFiles.Keys.ShouldContain($"{baseId}_w640.webp");
        UploadedFiles.Keys.ShouldContain($"{baseId}_w1024.webp");
        UploadedFiles.Keys.ShouldContain($"{baseId}.jpg");
    }

    [Fact]
    public async Task ShouldFailWhenVariantUploadFails()
    {
        _fileServiceMock
            .UploadFileWithObjectNameAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var objectName = callInfo.ArgAt<string>(2);
                if (objectName.EndsWith("_w640.webp", StringComparison.Ordinal))
                {
                    return Result.Fail<string>("Failed to upload variant");
                }

                UploadedFiles[objectName] = callInfo.ArgAt<byte[]>(1);
                return Result.Ok(objectName);
            });

        var dto = new CreatePropositionDto(DateTime.UtcNow.Date, ComplexityEnum.Beginner, SubjectEnum.Business);

        var result = await _service.CreatePropositionsAsync(dto, 1);

        result.ShouldBeEmpty();
    }

    protected override void ConfigureMocks(IServiceCollection services, SubjectEnum? subjectWithoutNews = null)
    {
        var faker = new Faker();

        var newsClientMock = Substitute.For<INewsClient>();
        newsClientMock.GetNewsAsync(Arg.Any<SubjectEnum>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(NewsDtoFaker.Generate(1)));
        services.AddSingleton(newsClientMock);

        var articleExtractorMock = Substitute.For<IArticleExtractor>();
        articleExtractorMock.DownloadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(SampleImageBytes));
        articleExtractorMock.GetVisibleTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(faker.Lorem.Paragraph(3000)));
        services.AddSingleton(articleExtractorMock);

        var generativeAIClientMock = Substitute.For<IGenerativeAIClient>();
        generativeAIClientMock.GenerateTextAsync(Arg.Any<ComplexityEnum>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new AIGeneratedTextDto(faker.Lorem.Paragraph(10), faker.Lorem.Paragraph(3000))));
        generativeAIClientMock.ValidateImageAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        services.AddSingleton(generativeAIClientMock);

        var textToSpeechClientMock = Substitute.For<ITextToSpeechClient>();
        textToSpeechClientMock.GenerateAudioAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new AudioDto(faker.Random.Bytes(1000), faker.Random.Guid().ToString(), 60)));
        services.AddSingleton(textToSpeechClientMock);

        var fileServiceMock = Substitute.For<IFileService>();
        _fileServiceMock = fileServiceMock;

        fileServiceMock
            .UploadFileAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(Guid.NewGuid().ToString()));

        fileServiceMock
            .UploadFileAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(Guid.NewGuid().ToString()));

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
            DailyRequestsLimit = 100,
            PropositionsLimitPerTopic = 300,
            NewsRequestLimit = 3
        };
        var optionsMonitor = Substitute.For<IOptionsMonitor<PropositionOptions>>();
        optionsMonitor.CurrentValue.Returns(propositionOptions);
        services.AddSingleton(optionsMonitor);
    }

    private static byte[] CreateSampleImageBytes()
    {
        using var image = new Image<Rgba32>(1280, 720, new Rgba32(40, 80, 120));
        using var outputStream = new MemoryStream();
        image.SaveAsJpeg(outputStream, new JpegEncoder { Quality = 80 });
        return outputStream.ToArray();
    }
}
