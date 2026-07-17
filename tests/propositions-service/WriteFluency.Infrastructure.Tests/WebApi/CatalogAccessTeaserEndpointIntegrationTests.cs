using System.Net;
using System.Net.Http.Json;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Testcontainers.PostgreSql;
using WriteFluency.Common;
using WriteFluency.Data;
using WriteFluency.Propositions;
using WriteFluency.TextComparisons;
using WriteFluency.WebApi.Users;

namespace WriteFluency.Infrastructure.Tests.WebApi;

public sealed class CatalogAccessTeaserEndpointIntegrationTests :
    IClassFixture<PropositionsApiIntegrationFixture>
{
    private readonly PropositionsApiIntegrationFixture _fixture;

    public CatalogAccessTeaserEndpointIntegrationTests(PropositionsApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AnonymousRestrictedCatalogFlow_ShouldPreviewConsumeGrantAndGateComparison()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await _fixture.ResetAsync();
        _fixture.UsersSessionClient.Session = UsersSession.Anonymous;

        var propositions = await _fixture.SeedCatalogPropositionsAsync(20);
        var grantedExercise = propositions[0];
        var otherRestrictedExercise = propositions[1];
        using var client = _fixture.CreateClient();
        AddAnonymousFingerprintHeaders(client);

        var preview = await PostJsonAsync<PreviewExerciseAccessResultDto>(
            client,
            $"/api/proposition/{grantedExercise.Id}/preview-access");

        preview.AccessStatus.ShouldBe(CatalogAccessStatuses.PreviewAvailableAnonymousSample);
        preview.AudioUrl!.ShouldContain($"/{Proposition.AudioBucketName}/{grantedExercise.AudioFileId}");
        preview.Metadata.RequiresPro.ShouldBeTrue();
        (await _fixture.CountAsync<CatalogAccessCounter>()).ShouldBe(0);
        (await _fixture.CountAsync<CatalogExerciseGrant>()).ShouldBe(0);

        var begin = await PostJsonAsync<BeginExerciseResultDto>(
            client,
            $"/api/proposition/{grantedExercise.Id}/begin");

        begin.Access.ShouldBe(CatalogAccessStatuses.Granted);
        begin.AudioUrl!.ShouldContain($"/{Proposition.AudioBucketName}/{grantedExercise.AudioFileId}");
        begin.Metadata.RequiresPro.ShouldBeTrue();

        var counter = await _fixture.SingleAsync<CatalogAccessCounter>();
        counter.SubjectType.ShouldBe("anonymous");
        counter.Feature.ShouldBe(CatalogAccessFeatures.AnonymousSample);
        counter.AnonymousClientIpAddress.ShouldBe("203.0.113.42");
        counter.UsedCount.ShouldBe(1);

        var grant = await _fixture.SingleAsync<CatalogExerciseGrant>();
        grant.SubjectType.ShouldBe("anonymous");
        grant.PropositionId.ShouldBe(grantedExercise.Id);
        grant.Source.ShouldBe(CatalogAccessFeatures.AnonymousSample);
        grant.AnonymousClientIpAddress.ShouldBe("203.0.113.42");

        var sameExerciseBegin = await PostJsonAsync<BeginExerciseResultDto>(
            client,
            $"/api/proposition/{grantedExercise.Id}/begin");
        sameExerciseBegin.Access.ShouldBe(CatalogAccessStatuses.Granted);
        (await _fixture.CountAsync<CatalogAccessCounter>()).ShouldBe(1);
        (await _fixture.CountAsync<CatalogExerciseGrant>()).ShouldBe(1);

        var deniedOtherBegin = await PostJsonAsync<BeginExerciseResultDto>(
            client,
            $"/api/proposition/{otherRestrictedExercise.Id}/begin");
        deniedOtherBegin.Access.ShouldBe(CatalogAccessStatuses.LoginRequiredToUnlockExercise);
        deniedOtherBegin.AudioUrl.ShouldBeNull();

        var deniedComparison = await client.PostAsJsonAsync(
            "/api/text-comparison/compare-texts",
            new CompareTextsDto(otherRestrictedExercise.Id, "different answer"));
        deniedComparison.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var deniedPayload = await deniedComparison.Content.ReadFromJsonAsync<ProRequiredResultDto>();
        deniedPayload.ShouldNotBeNull();
        deniedPayload.Access.ShouldBe("pro_required");
        deniedPayload.Metadata.RequiresPro.ShouldBeTrue();

        var grantedComparison = await client.PostAsJsonAsync(
            "/api/text-comparison/compare-texts",
            new CompareTextsDto(grantedExercise.Id, grantedExercise.Text));
        grantedComparison.StatusCode.ShouldBe(HttpStatusCode.OK);
        var comparisonPayload = await grantedComparison.Content.ReadFromJsonAsync<TextComparisonResult>();
        comparisonPayload.ShouldNotBeNull();
        comparisonPayload.OriginalText.ShouldBe(grantedExercise.Text);
        comparisonPayload.UserText.ShouldBe(grantedExercise.Text);
    }

    [Fact]
    public async Task LoggedInFreeRestrictedCatalogFlow_ShouldUseFreeIntroQuotaAndThenRequireUpgrade()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await _fixture.ResetAsync();
        _fixture.UsersSessionClient.Session = new UsersSession(
            UserId: "user-1",
            IsAuthenticated: true,
            IsPro: false,
            CurrentPeriodEndUtc: null);

        var propositions = await _fixture.SeedCatalogPropositionsAsync(20);
        var grantedExercise = propositions[0];
        var otherRestrictedExercise = propositions[1];
        using var client = _fixture.CreateClient();

        var preview = await PostJsonAsync<PreviewExerciseAccessResultDto>(
            client,
            $"/api/proposition/{grantedExercise.Id}/preview-access");
        preview.AccessStatus.ShouldBe(CatalogAccessStatuses.PreviewAvailableFreeIntro);
        preview.AudioUrl.ShouldNotBeNullOrWhiteSpace();
        (await _fixture.CountAsync<CatalogAccessCounter>()).ShouldBe(0);
        (await _fixture.CountAsync<CatalogExerciseGrant>()).ShouldBe(0);

        var begin = await PostJsonAsync<BeginExerciseResultDto>(
            client,
            $"/api/proposition/{grantedExercise.Id}/begin");
        begin.Access.ShouldBe(CatalogAccessStatuses.Granted);

        var counter = await _fixture.SingleAsync<CatalogAccessCounter>();
        counter.SubjectType.ShouldBe("user");
        counter.SubjectKey.ShouldBe("user-1");
        counter.Feature.ShouldBe(CatalogAccessFeatures.FreeIntro);
        counter.UsedCount.ShouldBe(1);

        var deniedOtherBegin = await PostJsonAsync<BeginExerciseResultDto>(
            client,
            $"/api/proposition/{otherRestrictedExercise.Id}/begin");
        deniedOtherBegin.Access.ShouldBe(CatalogAccessStatuses.UpgradeRequiredToUnlockExercise);
        deniedOtherBegin.AudioUrl.ShouldBeNull();
    }

    [Fact]
    public async Task AnonymousProReviewFlow_ShouldPersistNormalizedClientIpAddress()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await _fixture.ResetAsync();
        _fixture.UsersSessionClient.Session = UsersSession.Anonymous;

        var propositions = await _fixture.SeedCatalogPropositionsAsync(1);
        using var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.42:51234");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 Chrome/150");

        var response = await client.PostAsJsonAsync(
            "/api/text-comparison/compare-texts",
            new CompareTextsDto(propositions[0].Id, "Different text."));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var counter = await _fixture.SingleAsync<AiUsageCounter>();
        counter.Feature.ShouldBe(AiUsageFeatures.MistakePatternClassificationAnonymousSample);
        counter.AnonymousClientIpAddress.ShouldBe("203.0.113.42");
    }

    private static void AddAnonymousFingerprintHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.42");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 Chrome/150");
    }

    private static async Task<T> PostJsonAsync<T>(HttpClient client, string url)
        where T : class
    {
        using var response = await client.PostAsync(url, content: null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<T>();
        payload.ShouldNotBeNull();
        return payload;
    }
}

public sealed class PropositionsApiIntegrationFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    public PropositionsApiWebApplicationFactory? Factory { get; private set; }

    public TestingUsersSessionClient UsersSessionClient { get; } = new();

    public bool IsAvailable => Factory is not null;

    public string? UnavailableReason { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("wf-propositions-postgresdb")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _postgres.StartAsync();

            Factory = new PropositionsApiWebApplicationFactory(
                _postgres.GetConnectionString(),
                UsersSessionClient);

            await ResetAsync();
        }
        catch (Exception ex)
        {
            UnavailableReason = ex.Message;
        }
    }

    public async Task ResetAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        UsersSessionClient.Session = UsersSession.Anonymous;
        await using var scope = Factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await EnsureEnumRowsAsync(db);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    public HttpClient CreateClient()
    {
        if (Factory is null)
        {
            throw new InvalidOperationException("Integration factory is not available.");
        }

        return Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    public async Task<Proposition[]> SeedCatalogPropositionsAsync(int count)
    {
        await using var scope = Factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var createdAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var propositions = Enumerable.Range(1, count)
            .Select(index => CreateProposition(
                index,
                createdAt.AddMinutes(index)))
            .ToArray();

        await db.Propositions.AddRangeAsync(propositions);
        await db.SaveChangesAsync();

        return propositions;
    }

    public async Task<int> CountAsync<TEntity>()
        where TEntity : class
    {
        await using var scope = Factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Set<TEntity>().CountAsync();
    }

    public async Task<TEntity> SingleAsync<TEntity>()
        where TEntity : class
    {
        await using var scope = Factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Set<TEntity>().SingleAsync();
    }

    private static Proposition CreateProposition(int index, DateTime createdAt)
    {
        var text = $"Server text {index}.";
        return new Proposition
        {
            PublishedOn = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc),
            SubjectId = SubjectEnum.Business,
            ComplexityId = ComplexityEnum.Beginner,
            AudioFileId = $"audio-{index}",
            Voice = "test-voice",
            AudioDurationSeconds = 60,
            Text = text,
            TextLength = text.Length,
            Title = $"Exercise {index}",
            CreatedAt = createdAt,
            NewsInfo = new NewsInfo
            {
                Id = $"news-{index}",
                Title = $"Exercise {index}",
                Description = "Test news description.",
                Url = $"https://example.com/news-{index}",
                ImageUrl = "https://example.com/image.jpg",
                Text = "Test article text.",
                TextLength = "Test article text.".Length
            }
        };
    }

    private static async Task EnsureEnumRowsAsync(AppDbContext db)
    {
        foreach (var subject in Enum.GetValues<SubjectEnum>())
        {
            if (!await db.Subjects.AnyAsync(x => x.Id == subject))
            {
                db.Subjects.Add(new Subject { Id = subject, Description = subject.ToString() });
            }
        }

        foreach (var complexity in Enum.GetValues<ComplexityEnum>())
        {
            if (!await db.Complexities.AnyAsync(x => x.Id == complexity))
            {
                db.Complexities.Add(new Complexity { Id = complexity, Description = complexity.ToString() });
            }
        }

        await db.SaveChangesAsync();
    }
}

public sealed class PropositionsApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly TestingUsersSessionClient _usersSessionClient;

    public PropositionsApiWebApplicationFactory(
        string postgresConnectionString,
        TestingUsersSessionClient usersSessionClient)
    {
        _postgresConnectionString = postgresConnectionString;
        _usersSessionClient = usersSessionClient;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:wf-propositions-postgresdb"] = _postgresConnectionString,
                ["UsersService:BaseUrl"] = "https://users.test/",
                ["ExternalApis:OpenAI:BaseAddress"] = "https://api.openai.test/",
                ["ExternalApis:OpenAI:Key"] = "test-openai-key",
                ["ExternalApis:TextToSpeech:Key"] = "test-text-to-speech-key",
                ["ExternalApis:News:BaseAddress"] = "https://news.test/",
                ["ExternalApis:News:Key"] = "test-news-key",
                ["Propositions:CatalogAccessTeaser:Enabled"] = "true",
                ["Propositions:CatalogAccessTeaser:AnonymousFingerprintSalt"] = "catalog-access-integration-salt",
                ["Propositions:CatalogAccessTeaser:AnonymousSampleLifetimeLimit"] = "1",
                ["Propositions:CatalogAccessTeaser:FreeIntroLifetimeLimit"] = "1",
                ["TextComparison:MistakePatternClassification:Enabled"] = "true",
                ["TextComparison:ProReviewTeaser:Enabled"] = "true",
                ["TextComparison:ProReviewTeaser:AnonymousFingerprintSalt"] = "pro-review-integration-salt",
                ["FileStorage:Endpoint"] = "127.0.0.1:9000",
                ["FileStorage:AccessKey"] = "test",
                ["FileStorage:SecretKey"] = "test"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(
                    _postgresConnectionString,
                    npgsql => npgsql.EnableRetryOnFailure()));

            services.RemoveAll<IUsersSessionClient>();
            services.AddSingleton<IUsersSessionClient>(_usersSessionClient);

            services.RemoveAll<IFileService>();
            services.AddSingleton<IFileService, TestingFileService>();

            services.RemoveAll<IMistakePatternClassifier>();
            services.AddSingleton<IMistakePatternClassifier, TestingMistakePatternClassifier>();
        });
    }
}

public sealed class TestingUsersSessionClient : IUsersSessionClient
{
    public UsersSession Session { get; set; } = UsersSession.Anonymous;

    public Task<UsersSession> GetSessionAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Session);

    public Task<bool> IsProAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Session.IsPro);
}

public sealed class TestingFileService : IFileService
{
    public Task<Result<string>> UploadFileAsync(
        string bucketName,
        byte[] file,
        string? fileExtension = null,
        string? contentType = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Ok(Guid.NewGuid().ToString("N")));

    public Task<Result<string>> UploadFileAsync(
        string bucketName,
        byte[] file,
        string url,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Ok(Guid.NewGuid().ToString("N")));

    public Task<Result<string>> UploadFileWithObjectNameAsync(
        string bucketName,
        byte[] file,
        string objectName,
        string? contentType = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Ok(objectName));

    public Task<byte[]> GetFileAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Array.Empty<byte>());

    public Task<Result<string>> CreatePresignedGetUrlAsync(
        string bucketName,
        string objectName,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Ok($"https://audio.test/{bucketName}/{objectName}?signature=test"));
}

public sealed class TestingMistakePatternClassifier : IMistakePatternClassifier
{
    public bool IsEnabled => true;

    public Task<MistakePatternClassificationRun> ClassifyWithDiagnosticsAsync(
        MistakePatternClassificationRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new MistakePatternClassificationRun(
            request.Comparisons.Count == 0
                ? []
                :
                [
                    new MistakePatternAnnotation(
                        0,
                        request.Comparisons[0].SourceComparisonIndex,
                        ["test_pattern"],
                        request.Comparisons[0].UserText ?? string.Empty)
                ],
            []));
}
