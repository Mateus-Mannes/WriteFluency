using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Shouldly;
using WriteFluency.Application;
using WriteFluency.Data;

namespace WriteFluency.Propositions;

public class DailyPropositionGeneratorCursorTests : ApplicationTestBase
{
    private readonly IAppDbContext _context;
    private RecordingNewsClient _newsClient = null!;
    private PropositionOptions _propositionOptions = null!;

    public DailyPropositionGeneratorCursorTests()
    {
        _context = GetService<IAppDbContext>();
    }

    protected override void ConfigureMocks(IServiceCollection services, SubjectEnum? subjectWithoutNews = null)
    {
        base.ConfigureMocks(services, subjectWithoutNews);

        _newsClient = new RecordingNewsClient();
        _propositionOptions = new PropositionOptions
        {
            DailyRequestsLimit = 2,
            PropositionsLimitPerTopic = 1000,
            NewsRequestLimit = 3
        };

        services.RemoveAll<INewsClient>();
        services.AddSingleton<INewsClient>(_newsClient);

        services.RemoveAll<IOptionsMonitor<PropositionOptions>>();
        services.AddSingleton<IOptionsMonitor<PropositionOptions>>(new TestOptionsMonitor<PropositionOptions>(_propositionOptions));
    }

    [Fact]
    public async Task ShouldBackfillFromOldestFetchedArticleInSameRun()
    {
        var target = (SubjectEnum.General, ComplexityEnum.Beginner);
        await SeedCompetingGenerationLogsAsync(target);

        _newsClient.GetNews = request => CreateBatch(request, $"cursor-{request.Sequence}");

        await GetService<DailyPropositionGenerator>().GenerateDailyPropositionsAsync();

        _newsClient.Calls.Count.ShouldBe(2);
        _newsClient.Calls.ShouldAllBe(request => request.Subject == target.Item1 && request.Page == 1);

        var expectedSecondCursor = _newsClient.ReturnedBatches[0].Min(article => article.PublishedOn).AddTicks(-1);
        _newsClient.Calls[1].PublishedBefore.ShouldBe(expectedSecondCursor);

        var generatedCount = await _context.Propositions
            .CountAsync(p => p.SubjectId == target.Item1 && p.ComplexityId == target.Item2);
        generatedCount.ShouldBe(_propositionOptions.DailyRequestsLimit * _propositionOptions.NewsRequestLimit);
    }

    [Fact]
    public async Task ShouldMoveCursorWhenFetchedArticlesAreDuplicates()
    {
        var target = (SubjectEnum.General, ComplexityEnum.Beginner);
        await SeedCompetingGenerationLogsAsync(target);
        await SeedExistingPropositionsAsync(target, ["duplicate-0", "duplicate-1", "duplicate-2"]);

        _newsClient.GetNews = request => request.Sequence == 0
            ? CreateBatch(request, "duplicate")
            : CreateBatch(request, "backfill");

        await GetService<DailyPropositionGenerator>().GenerateDailyPropositionsAsync();

        _newsClient.Calls.Count.ShouldBe(2);
        var oldestDuplicateFetched = _newsClient.ReturnedBatches[0].Min(article => article.PublishedOn);
        _newsClient.Calls[1].PublishedBefore.ShouldBe(oldestDuplicateFetched.AddTicks(-1));

        var latestWindowLog = await _context.PropositionGenerationLogs
            .Where(log => log.SubjectId == target.Item1 && log.ComplexityId == target.Item2)
            .OrderByDescending(log => log.RequestedPublishedBefore)
            .FirstAsync();

        latestWindowLog.Success.ShouldBeFalse();
        latestWindowLog.SuccessCount.ShouldBe(0);
        latestWindowLog.GenerationDate.ShouldBe(oldestDuplicateFetched);
    }

    [Fact]
    public async Task ShouldGenerateSparseTopicFromOldAvailableNews()
    {
        var target = (SubjectEnum.General, ComplexityEnum.Beginner);
        var oldPublishedOn = new DateTime(2024, 1, 15, 9, 30, 0, DateTimeKind.Utc);

        _propositionOptions.DailyRequestsLimit = 1;
        _propositionOptions.NewsRequestLimit = 1;
        await SeedCompetingGenerationLogsAsync(target);

        _newsClient.GetNews = request => [CreateNews(request, "sparse-old-news", oldPublishedOn)];

        await GetService<DailyPropositionGenerator>().GenerateDailyPropositionsAsync();

        var proposition = await _context.Propositions.SingleAsync(p => p.NewsInfo.Id == "sparse-old-news");
        proposition.PublishedOn.ShouldBe(oldPublishedOn);

        var log = await _context.PropositionGenerationLogs.SingleAsync(log =>
            log.SubjectId == target.Item1 && log.ComplexityId == target.Item2);
        log.GenerationDate.ShouldBe(oldPublishedOn);
        log.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldStillCheckLatestNewsWhenSubjectIsOverLimit()
    {
        var target = (SubjectEnum.General, ComplexityEnum.Beginner);

        _propositionOptions.DailyRequestsLimit = 1;
        _propositionOptions.NewsRequestLimit = 1;
        _propositionOptions.PropositionsLimitPerTopic = 2;

        await SeedCompetingGenerationLogsAsync(target);
        await SeedExistingPropositionsAsync(target, ["old-1", "old-2"]);

        _newsClient.GetNews = request => [CreateNews(request, "latest-over-limit", request.PublishedBefore.AddMinutes(-1))];

        await GetService<DailyPropositionGenerator>().GenerateDailyPropositionsAsync();

        _newsClient.Calls.Count.ShouldBe(1);

        var activeCount = await _context.Propositions.CountAsync(p => p.SubjectId == target.Item1);
        activeCount.ShouldBe(_propositionOptions.PropositionsLimitPerTopic);

        var allTargetPropositions = await _context.Propositions
            .IgnoreQueryFilters()
            .Where(p => p.SubjectId == target.Item1)
            .ToListAsync();

        allTargetPropositions.ShouldContain(p => p.NewsInfo.Id == "latest-over-limit" && !p.IsDeleted);
        allTargetPropositions.Count(p => p.IsDeleted).ShouldBe(1);
    }

    [Fact]
    public async Task ShouldStopCombinationsThatFetchNoArticles()
    {
        _propositionOptions.DailyRequestsLimit = 100;
        _newsClient.GetNews = _ => [];

        await GetService<DailyPropositionGenerator>().GenerateDailyPropositionsAsync();

        _newsClient.Calls.Count.ShouldBe(Proposition.Parameters.Count);

        var logCount = await _context.PropositionGenerationLogs.CountAsync();
        logCount.ShouldBe(Proposition.Parameters.Count);

        var propositionCount = await _context.Propositions.CountAsync();
        propositionCount.ShouldBe(0);
    }

    private async Task SeedCompetingGenerationLogsAsync((SubjectEnum Subject, ComplexityEnum Complexity) target)
    {
        var generationDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        foreach (var parameters in Proposition.Parameters.Where(parameters => parameters != target))
        {
            _context.PropositionGenerationLogs.Add(new PropositionGenerationLog
            {
                GenerationDate = generationDate,
                RequestedPublishedBefore = generationDate,
                SubjectId = parameters.Item1,
                ComplexityId = parameters.Item2,
                SuccessCount = 0,
                Success = false,
                CreatedAt = generationDate
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedExistingPropositionsAsync(
        (SubjectEnum Subject, ComplexityEnum Complexity) target,
        IReadOnlyList<string> newsIds)
    {
        for (var index = 0; index < newsIds.Count; index++)
        {
            _context.Propositions.Add(CreateProposition(
                newsIds[index],
                target,
                new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index)));
        }

        await _context.SaveChangesAsync();
    }

    private static List<NewsDto> CreateBatch(RecordedNewsRequest request, string idPrefix)
    {
        return Enumerable.Range(0, request.Quantity)
            .Select(index => CreateNews(request, $"{idPrefix}-{index}", request.PublishedBefore.AddMinutes(-(index + 1))))
            .ToList();
    }

    private static NewsDto CreateNews(RecordedNewsRequest request, string id, DateTime publishedOn)
    {
        return new NewsDto(
            id,
            $"Title {id}",
            $"Description {id}",
            $"https://example.com/news/{id}",
            $"https://example.com/images/{id}.jpg",
            request.Subject,
            publishedOn);
    }

    private static Proposition CreateProposition(
        string newsId,
        (SubjectEnum Subject, ComplexityEnum Complexity) target,
        DateTime publishedOn)
    {
        return new Proposition
        {
            PublishedOn = publishedOn,
            SubjectId = target.Subject,
            ComplexityId = target.Complexity,
            AudioFileId = $"audio-{newsId}"[..Math.Min($"audio-{newsId}".Length, 50)],
            Voice = "test",
            AudioDurationSeconds = 1,
            Text = "Generated proposition text",
            TextLength = "Generated proposition text".Length,
            Title = $"Generated {newsId}",
            CreatedAt = DateTime.UtcNow,
            NewsInfo = new NewsInfo
            {
                Id = newsId,
                Title = $"News {newsId}",
                Description = $"Description {newsId}",
                Url = $"https://example.com/news/{newsId}",
                ImageUrl = $"https://example.com/images/{newsId}.jpg",
                Text = "Article text",
                TextLength = "Article text".Length
            }
        };
    }

    private sealed class RecordingNewsClient : INewsClient
    {
        public List<RecordedNewsRequest> Calls { get; } = [];
        public List<IReadOnlyList<NewsDto>> ReturnedBatches { get; } = [];
        public Func<RecordedNewsRequest, IReadOnlyList<NewsDto>> GetNews { get; set; } =
            request => CreateBatch(request, $"news-{request.Sequence}");

        public Task<Result<IEnumerable<NewsDto>>> GetNewsAsync(
            SubjectEnum subject,
            DateTime publishedBefore,
            int quantity = 3,
            int page = 1,
            CancellationToken cancellationToken = default)
        {
            var request = new RecordedNewsRequest(Calls.Count, subject, publishedBefore, quantity, page);
            Calls.Add(request);

            var batch = GetNews(request);
            ReturnedBatches.Add(batch);

            return Task.FromResult(Result.Ok<IEnumerable<NewsDto>>(batch));
        }
    }

    private sealed record RecordedNewsRequest(
        int Sequence,
        SubjectEnum Subject,
        DateTime PublishedBefore,
        int Quantity,
        int Page);

    private sealed class TestOptionsMonitor<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue => currentValue;
        public TOptions Get(string? name) => currentValue;
        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }
}
