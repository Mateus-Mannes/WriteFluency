using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Testcontainers.PostgreSql;
using WriteFluency.Application.Propositions.Interfaces;
using WriteFluency.Common;
using WriteFluency.Data;
using WriteFluency.TextComparisons;

namespace WriteFluency.Propositions;

public class PropositionServicePostgresSearchTests : IClassFixture<PropositionSearchPostgresFixture>
{
    private readonly PropositionSearchPostgresFixture _fixture;

    public PropositionServicePostgresSearchTests(PropositionSearchPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetExercisesAsync_ShouldSearchWithPostgresFullTextAndKeepFiltersRankingCountAndPagination()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await _fixture.ResetAsync();

        await using var context = _fixture.CreateContext();
        var olderBodyMatch = CreateProposition(
            title: "Urban planning update",
            text: "Climate resilience investments protect neighborhoods during floods.",
            subject: SubjectEnum.Science,
            complexity: ComplexityEnum.Beginner,
            createdAt: new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc));
        var titleMatch = CreateProposition(
            title: "Climate resilience plan",
            text: "City leaders presented a practical writing exercise.",
            subject: SubjectEnum.Science,
            complexity: ComplexityEnum.Beginner,
            createdAt: new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc));
        var wrongTopicMatch = CreateProposition(
            title: "Climate resilience funding",
            text: "The finance team discussed emergency reserves.",
            subject: SubjectEnum.Business,
            complexity: ComplexityEnum.Beginner,
            createdAt: new DateTime(2026, 4, 22, 10, 0, 0, DateTimeKind.Utc));
        var unrelated = CreateProposition(
            title: "A quiet technology update",
            text: "Developers shipped a small maintenance release.",
            subject: SubjectEnum.Science,
            complexity: ComplexityEnum.Beginner,
            createdAt: new DateTime(2026, 4, 23, 10, 0, 0, DateTimeKind.Utc));

        await context.Propositions.AddRangeAsync(olderBodyMatch, titleMatch, wrongTopicMatch, unrelated);
        await context.SaveChangesAsync();

        var service = _fixture.CreateService(context);

        var result = await service.GetExercisesAsync(new ExerciseFilterDto(
            Topic: SubjectEnum.Science,
            Level: ComplexityEnum.Beginner,
            PageNumber: 1,
            PageSize: 10,
            SortBy: "newest",
            SearchText: "climate resilience"));

        result.TotalCount.ShouldBe(2);
        result.Items.Select(x => x.Id).ShouldBe(new[] { titleMatch.Id, olderBodyMatch.Id });
        result.Items.Select(x => x.Id).ShouldNotContain(wrongTopicMatch.Id);
        result.Items.Select(x => x.Id).ShouldNotContain(unrelated.Id);

        var secondPage = await service.GetExercisesAsync(new ExerciseFilterDto(
            Topic: SubjectEnum.Science,
            Level: ComplexityEnum.Beginner,
            PageNumber: 2,
            PageSize: 1,
            SortBy: "newest",
            SearchText: "climate resilience"));

        secondPage.TotalCount.ShouldBe(2);
        secondPage.Items.Single().Id.ShouldBe(olderBodyMatch.Id);
    }

    [Fact]
    public async Task GetExercisesAsync_ShouldMatchAnySearchTermAndRankMoreCompleteMatchesFirst()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await _fixture.ResetAsync();

        await using var context = _fixture.CreateContext();
        var allTerms = CreateProposition(
            title: "Trump election problem",
            text: "A complete match should rank first.",
            subject: SubjectEnum.Politics,
            complexity: ComplexityEnum.Intermediate,
            createdAt: new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc));
        var twoTerms = CreateProposition(
            title: "Trump election update",
            text: "This result matches two of the query terms.",
            subject: SubjectEnum.Politics,
            complexity: ComplexityEnum.Intermediate,
            createdAt: new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc));
        var problemOnly = CreateProposition(
            title: "Problem analysis",
            text: "This result matches one query term.",
            subject: SubjectEnum.Politics,
            complexity: ComplexityEnum.Intermediate,
            createdAt: new DateTime(2026, 4, 23, 10, 0, 0, DateTimeKind.Utc));
        var electionOnly = CreateProposition(
            title: "Election calendar",
            text: "This result also matches one query term.",
            subject: SubjectEnum.Politics,
            complexity: ComplexityEnum.Intermediate,
            createdAt: new DateTime(2026, 4, 22, 10, 0, 0, DateTimeKind.Utc));
        var unrelated = CreateProposition(
            title: "Technology maintenance",
            text: "This result should not be returned.",
            subject: SubjectEnum.Politics,
            complexity: ComplexityEnum.Intermediate,
            createdAt: new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Utc));

        await context.Propositions.AddRangeAsync(allTerms, twoTerms, problemOnly, electionOnly, unrelated);
        await context.SaveChangesAsync();

        var service = _fixture.CreateService(context);

        var result = await service.GetExercisesAsync(new ExerciseFilterDto(
            Topic: SubjectEnum.Politics,
            Level: ComplexityEnum.Intermediate,
            PageNumber: 1,
            PageSize: 10,
            SortBy: "newest",
            SearchText: "Trump election problem"));

        result.TotalCount.ShouldBe(4);
        result.Items.Take(2).Select(x => x.Id).ShouldBe(new[] { allTerms.Id, twoTerms.Id });
        result.Items.Skip(2).Select(x => x.Id).ShouldBe(new[] { problemOnly.Id, electionOnly.Id }, ignoreOrder: true);
        result.Items.Select(x => x.Id).ShouldNotContain(unrelated.Id);
    }

    [Fact]
    public async Task GetExercisesAsync_ShouldMatchPrefixSearchTerms()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await _fixture.ResetAsync();

        await using var context = _fixture.CreateContext();
        var prefixMatch = CreateProposition(
            title: "Police response update",
            text: "Officials published a detailed report.",
            subject: SubjectEnum.Politics,
            complexity: ComplexityEnum.Intermediate,
            createdAt: new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc));
        var unrelated = CreateProposition(
            title: "Public transport update",
            text: "Officials published a detailed report.",
            subject: SubjectEnum.Politics,
            complexity: ComplexityEnum.Intermediate,
            createdAt: new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc));

        await context.Propositions.AddRangeAsync(prefixMatch, unrelated);
        await context.SaveChangesAsync();

        var service = _fixture.CreateService(context);

        var result = await service.GetExercisesAsync(new ExerciseFilterDto(
            Topic: SubjectEnum.Politics,
            Level: ComplexityEnum.Intermediate,
            PageNumber: 1,
            PageSize: 10,
            SortBy: "newest",
            SearchText: "polic"));

        result.TotalCount.ShouldBe(1);
        result.Items.Single().Id.ShouldBe(prefixMatch.Id);
    }

    [Fact]
    public async Task GetExercisesAsync_ShouldMatchSmallTyposWithTrigramSearch()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        await _fixture.ResetAsync();

        await using var context = _fixture.CreateContext();
        var typoMatch = CreateProposition(
            title: "Government budget report",
            text: "Lawmakers debated the spending plan.",
            subject: SubjectEnum.Business,
            complexity: ComplexityEnum.Advanced,
            createdAt: new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc));
        var unrelated = CreateProposition(
            title: "Technology maintenance",
            text: "Developers shipped a small release.",
            subject: SubjectEnum.Business,
            complexity: ComplexityEnum.Advanced,
            createdAt: new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc));

        await context.Propositions.AddRangeAsync(typoMatch, unrelated);
        await context.SaveChangesAsync();

        var service = _fixture.CreateService(context);

        var result = await service.GetExercisesAsync(new ExerciseFilterDto(
            Topic: SubjectEnum.Business,
            Level: ComplexityEnum.Advanced,
            PageNumber: 1,
            PageSize: 10,
            SortBy: "newest",
            SearchText: "goverment"));

        result.TotalCount.ShouldBe(1);
        result.Items.Single().Id.ShouldBe(typoMatch.Id);
    }

    private static Proposition CreateProposition(
        string title,
        string text,
        SubjectEnum subject,
        ComplexityEnum complexity,
        DateTime createdAt) =>
        new()
        {
            PublishedOn = createdAt.Date,
            SubjectId = subject,
            ComplexityId = complexity,
            AudioFileId = Guid.NewGuid().ToString("N"),
            Voice = "test-voice",
            AudioDurationSeconds = 60,
            Text = text,
            TextLength = text.Length,
            Title = title,
            CreatedAt = createdAt,
            NewsInfo = new NewsInfo
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title,
                Description = "Test news description.",
                Url = $"https://example.com/{Guid.NewGuid():N}",
                ImageUrl = "https://example.com/image.jpg",
                Text = "Original article content is not indexed by exercise search.",
                TextLength = "Original article content is not indexed by exercise search.".Length
            }
        };
}

public sealed class PropositionSearchPostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private DbContextOptions<AppDbContext>? _options;

    public bool IsAvailable => _options is not null;

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:14.3-alpine")
                .WithDatabase("wf-propositions-postgresdb")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _postgres.StartAsync();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options;

            await using var context = CreateContext();
            await context.Database.MigrateAsync();
            await EnsureEnumRowsAsync(context);
        }
        catch
        {
            _options = null;
        }
    }

    public async Task ResetAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("""DELETE FROM "Propositions";""");
        await EnsureEnumRowsAsync(context);
    }

    public AppDbContext CreateContext()
    {
        if (_options is null)
        {
            throw new InvalidOperationException("PostgreSQL test fixture is not available.");
        }

        return new AppDbContext(_options);
    }

    public PropositionService CreateService(AppDbContext context)
    {
        return new PropositionService(
            context,
            Substitute.For<IFileService>(),
            Substitute.For<IGenerativeAIClient>(),
            Substitute.For<ITextToSpeechClient>(),
            Substitute.For<ILogger<PropositionService>>());
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    private static async Task EnsureEnumRowsAsync(AppDbContext context)
    {
        foreach (var subject in Enum.GetValues<SubjectEnum>())
        {
            if (!await context.Subjects.AnyAsync(x => x.Id == subject))
            {
                await context.Subjects.AddAsync(new Subject { Id = subject, Description = subject.ToString() });
            }
        }

        foreach (var complexity in Enum.GetValues<ComplexityEnum>())
        {
            if (!await context.Complexities.AnyAsync(x => x.Id == complexity))
            {
                await context.Complexities.AddAsync(new Complexity { Id = complexity, Description = complexity.ToString() });
            }
        }

        await context.SaveChangesAsync();
    }
}
