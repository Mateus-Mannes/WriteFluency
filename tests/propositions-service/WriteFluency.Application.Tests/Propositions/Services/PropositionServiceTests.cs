using Microsoft.EntityFrameworkCore;
using Shouldly;
using WriteFluency.Application;
using WriteFluency.Data;

namespace WriteFluency.Propositions;

public class PropositionServiceTests : ApplicationTestBase
{
    private readonly IAppDbContext _context;
    private readonly PropositionService _service;

    public PropositionServiceTests()
    {
        _context = GetService<IAppDbContext>();
        _service = GetService<PropositionService>();
    }

    [Fact]
    public async Task GetExercisesAsync_ShouldOrderNewestByCreatedAt()
    {
        var olderCreatedNewerPublished = CreateProposition(
            title: "Newer published article",
            publishedOn: new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc),
            createdAt: new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc));

        var newerCreatedOlderPublished = CreateProposition(
            title: "Newer exercise",
            publishedOn: new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
            createdAt: new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc));

        await _context.Propositions.AddRangeAsync(olderCreatedNewerPublished, newerCreatedOlderPublished);
        await _context.SaveChangesAsync();

        var result = await _service.GetExercisesAsync(new ExerciseFilterDto(
            Topic: SubjectEnum.Business,
            SortBy: "newest"));

        result.Items.Select(x => x.Id).ShouldBe(new[]
        {
            newerCreatedOlderPublished.Id,
            olderCreatedNewerPublished.Id
        });
    }

    [Fact]
    public async Task GetExercisesAsync_ShouldOrderOldestByCreatedAt()
    {
        var olderCreatedNewerPublished = CreateProposition(
            title: "Older exercise",
            publishedOn: new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc),
            createdAt: new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc));

        var newerCreatedOlderPublished = CreateProposition(
            title: "Older published article",
            publishedOn: new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
            createdAt: new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc));

        await _context.Propositions.AddRangeAsync(olderCreatedNewerPublished, newerCreatedOlderPublished);
        await _context.SaveChangesAsync();

        var result = await _service.GetExercisesAsync(new ExerciseFilterDto(
            Topic: SubjectEnum.Business,
            SortBy: "oldest"));

        result.Items.Select(x => x.Id).ShouldBe(new[]
        {
            olderCreatedNewerPublished.Id,
            newerCreatedOlderPublished.Id
        });
    }

    [Fact]
    public async Task GetExercisesAsync_ShouldMaximizeTopicDistanceWhenNoFiltersAreApplied()
    {
        var createdAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var propositions = new[]
        {
            CreateProposition("Politics 1", createdAt: createdAt.AddMinutes(8), subject: SubjectEnum.Politics),
            CreateProposition("Science 1", createdAt: createdAt.AddMinutes(7), subject: SubjectEnum.Science),
            CreateProposition("Politics 2", createdAt: createdAt.AddMinutes(6), subject: SubjectEnum.Politics),
            CreateProposition("Science 2", createdAt: createdAt.AddMinutes(5), subject: SubjectEnum.Science),
            CreateProposition("Politics 3", createdAt: createdAt.AddMinutes(4), subject: SubjectEnum.Politics),
            CreateProposition("Science 3", createdAt: createdAt.AddMinutes(3), subject: SubjectEnum.Science),
            CreateProposition("Sports 1", createdAt: createdAt.AddMinutes(2), subject: SubjectEnum.Sports),
            CreateProposition("General 1", createdAt: createdAt.AddMinutes(1), subject: SubjectEnum.General),
        };

        await _context.Propositions.AddRangeAsync(propositions);
        await _context.SaveChangesAsync();

        var result = await _service.GetExercisesAsync(new ExerciseFilterDto(PageSize: 8));

        result.Items.Select(x => x.Topic).ShouldBe(new[]
        {
            SubjectEnum.Politics,
            SubjectEnum.Science,
            SubjectEnum.Sports,
            SubjectEnum.General,
            SubjectEnum.Politics,
            SubjectEnum.Science,
            SubjectEnum.Politics,
            SubjectEnum.Science
        });
    }

    [Fact]
    public async Task GetExercisesAsync_ShouldMarkOnlyLatestEighteenGlobalExercisesAsFree()
    {
        var createdAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var propositions = Enumerable.Range(1, 20)
            .Select(index => CreateProposition(
                title: $"Exercise {index}",
                createdAt: createdAt.AddMinutes(index)))
            .ToArray();

        await _context.Propositions.AddRangeAsync(propositions);
        await _context.SaveChangesAsync();

        var result = await _service.GetExercisesAsync(new ExerciseFilterDto(
            Topic: SubjectEnum.Business,
            PageSize: 20));

        var proItems = result.Items.Where(item => item.RequiresPro).Select(item => item.Title).ToArray();
        var freeItems = result.Items.Where(item => !item.RequiresPro).Select(item => item.Title).ToArray();

        proItems.ShouldBe(new[] { "Exercise 2", "Exercise 1" });
        freeItems.ShouldBe(Enumerable.Range(3, 18).Reverse().Select(index => $"Exercise {index}").ToArray());
    }

    [Fact]
    public async Task GetExercisesAsync_ShouldKeepGlobalFreeWindowWhenTopicFilterIsApplied()
    {
        var createdAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var businessPropositions = new[]
        {
            CreateProposition("Business 1", createdAt: createdAt.AddMinutes(1)),
            CreateProposition("Business 2", createdAt: createdAt.AddMinutes(2))
        };
        var sciencePropositions = Enumerable.Range(1, 18)
            .Select(index => CreateProposition(
                title: $"Science {index}",
                createdAt: createdAt.AddMinutes(index + 2),
                subject: SubjectEnum.Science))
            .ToArray();

        await _context.Propositions.AddRangeAsync(businessPropositions.Concat(sciencePropositions));
        await _context.SaveChangesAsync();

        var result = await _service.GetExercisesAsync(new ExerciseFilterDto(
            Topic: SubjectEnum.Business,
            PageSize: 20));

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldAllBe(item => item.RequiresPro);
    }

    [Fact]
    public async Task GetExercisesAsync_ShouldKeepFreeWindowWhenOldestSortIsApplied()
    {
        var createdAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var propositions = Enumerable.Range(1, 20)
            .Select(index => CreateProposition(
                title: $"Exercise {index}",
                createdAt: createdAt.AddMinutes(index)))
            .ToArray();

        await _context.Propositions.AddRangeAsync(propositions);
        await _context.SaveChangesAsync();

        var result = await _service.GetExercisesAsync(new ExerciseFilterDto(
            Topic: SubjectEnum.Business,
            SortBy: "oldest",
            PageSize: 20));

        result.Items.Select(item => item.Title).Take(3).ShouldBe(new[] { "Exercise 1", "Exercise 2", "Exercise 3" });
        result.Items.Single(item => item.Title == "Exercise 1").RequiresPro.ShouldBeTrue();
        result.Items.Single(item => item.Title == "Exercise 2").RequiresPro.ShouldBeTrue();
        result.Items.Single(item => item.Title == "Exercise 3").RequiresPro.ShouldBeFalse();
    }

    [Fact]
    public async Task GetExercisesAsync_ShouldKeepFullFilteredTotalCountWithProAnnotations()
    {
        var createdAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var propositions = Enumerable.Range(1, 20)
            .Select(index => CreateProposition(
                title: $"Exercise {index}",
                createdAt: createdAt.AddMinutes(index)))
            .ToArray();

        await _context.Propositions.AddRangeAsync(propositions);
        await _context.SaveChangesAsync();

        var result = await _service.GetExercisesAsync(new ExerciseFilterDto(
            Topic: SubjectEnum.Business,
            PageNumber: 2,
            PageSize: 10));

        result.TotalCount.ShouldBe(20);
        result.Items.Count.ShouldBe(10);
        result.Items.Select(item => item.Title).ShouldBe(Enumerable.Range(1, 10).Reverse().Select(index => $"Exercise {index}").ToArray());
        result.Items.Count(item => item.RequiresPro).ShouldBe(2);
    }

    [Fact]
    public async Task GetMetadataAsync_ShouldReturnSafeMetadataWithProRequirement()
    {
        var createdAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var propositions = Enumerable.Range(1, 19)
            .Select(index => CreateProposition(
                title: $"Exercise {index}",
                createdAt: createdAt.AddMinutes(index)))
            .ToArray();

        await _context.Propositions.AddRangeAsync(propositions);
        await _context.SaveChangesAsync();

        var metadata = await _service.GetMetadataAsync(propositions[0].Id);

        metadata.ShouldNotBeNull();
        metadata.Id.ShouldBe(propositions[0].Id);
        metadata.Title.ShouldBe("Exercise 1");
        metadata.NewsUrl.ShouldBe(propositions[0].NewsInfo.Url);
        metadata.RequiresPro.ShouldBeTrue();
        metadata.OriginalWordCount.ShouldBe(3);
    }

    [Fact]
    public async Task BeginExerciseAsync_WhenFreeExercise_ShouldReturnGrantedWithPresignedAudioUrl()
    {
        var proposition = CreateProposition("Free exercise");
        await _context.Propositions.AddAsync(proposition);
        await _context.SaveChangesAsync();

        var result = await _service.BeginExerciseAsync(proposition.Id, isPro: false);

        result.ShouldNotBeNull();
        result.Access.ShouldBe("granted");
        result.AudioUrl!.ShouldContain($"/{Proposition.AudioBucketName}/{proposition.AudioFileId}");
        result.AudioExpiresAtUtc.ShouldNotBeNull();
        result.Metadata.RequiresPro.ShouldBeFalse();
    }

    [Fact]
    public async Task BeginExerciseAsync_WhenRestrictedAndAnonymousHasNoFingerprint_ShouldReturnLoginRequiredWithoutAudio()
    {
        var createdAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var propositions = Enumerable.Range(1, 19)
            .Select(index => CreateProposition(
                title: $"Exercise {index}",
                createdAt: createdAt.AddMinutes(index)))
            .ToArray();

        await _context.Propositions.AddRangeAsync(propositions);
        await _context.SaveChangesAsync();

        var result = await _service.BeginExerciseAsync(propositions[0].Id, isPro: false);

        result.ShouldNotBeNull();
        result.Access.ShouldBe(CatalogAccessStatuses.LoginRequiredToUnlockExercise);
        result.AudioUrl.ShouldBeNull();
        result.AudioExpiresAtUtc.ShouldBeNull();
        result.Metadata.RequiresPro.ShouldBeTrue();
    }

    [Fact]
    public async Task PreviewExerciseAccessAsync_WhenAnonymousRestrictedQuotaUnused_ShouldReturnAudioWithoutConsumingQuota()
    {
        var propositions = await SeedCatalogPropositionsAsync(19);
        var accessContext = AnonymousContext();

        var result = await _service.PreviewExerciseAccessAsync(propositions[0].Id, accessContext);

        result.ShouldNotBeNull();
        result.AccessStatus.ShouldBe(CatalogAccessStatuses.PreviewAvailableAnonymousSample);
        result.AudioUrl!.ShouldContain($"/{Proposition.AudioBucketName}/{propositions[0].AudioFileId}");
        result.AudioExpiresAtUtc.ShouldNotBeNull();
        result.Metadata.RequiresPro.ShouldBeTrue();
        (await _context.CatalogAccessCounters.CountAsync()).ShouldBe(0);
        (await _context.CatalogExerciseGrants.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task BeginExerciseAsync_WhenAnonymousRestrictedQuotaUnused_ShouldConsumeQuotaAndCreatePermanentGrant()
    {
        var propositions = await SeedCatalogPropositionsAsync(20);
        var accessContext = AnonymousContext();

        var result = await _service.BeginExerciseAsync(propositions[0].Id, accessContext);

        result.ShouldNotBeNull();
        result.Access.ShouldBe("granted");
        result.AudioUrl.ShouldNotBeNullOrWhiteSpace();
        result.Metadata.RequiresPro.ShouldBeTrue();

        var counter = await _context.CatalogAccessCounters.SingleAsync();
        counter.SubjectType.ShouldBe("anonymous");
        counter.SubjectKey.ShouldBe("anon-fingerprint-1");
        counter.Feature.ShouldBe(CatalogAccessFeatures.AnonymousSample);
        counter.UsedCount.ShouldBe(1);

        var grant = await _context.CatalogExerciseGrants.SingleAsync();
        grant.SubjectType.ShouldBe("anonymous");
        grant.SubjectKey.ShouldBe("anon-fingerprint-1");
        grant.PropositionId.ShouldBe(propositions[0].Id);
        grant.Source.ShouldBe(CatalogAccessFeatures.AnonymousSample);
    }

    [Fact]
    public async Task BeginExerciseAsync_WhenAnonymousQuotaUsed_ShouldAllowGrantedExerciseAndRequireLoginForAnother()
    {
        var propositions = await SeedCatalogPropositionsAsync(20);
        var accessContext = AnonymousContext();

        await _service.BeginExerciseAsync(propositions[0].Id, accessContext);

        var sameExerciseResult = await _service.BeginExerciseAsync(propositions[0].Id, accessContext);
        var otherRestrictedResult = await _service.BeginExerciseAsync(propositions[1].Id, accessContext);

        sameExerciseResult.ShouldNotBeNull();
        sameExerciseResult.Access.ShouldBe("granted");
        sameExerciseResult.AudioUrl.ShouldNotBeNullOrWhiteSpace();
        otherRestrictedResult.ShouldNotBeNull();
        otherRestrictedResult.Access.ShouldBe(CatalogAccessStatuses.LoginRequiredToUnlockExercise);
        otherRestrictedResult.AudioUrl.ShouldBeNull();
        (await _context.CatalogAccessCounters.SingleAsync()).UsedCount.ShouldBe(1);
        (await _context.CatalogExerciseGrants.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task BeginExerciseAsync_WhenLoggedInFreeQuotaUsed_ShouldAllowGrantedExerciseAndRequireUpgradeForAnother()
    {
        var propositions = await SeedCatalogPropositionsAsync(20);
        var accessContext = LoggedInFreeContext();

        var preview = await _service.PreviewExerciseAccessAsync(propositions[0].Id, accessContext);
        var firstBegin = await _service.BeginExerciseAsync(propositions[0].Id, accessContext);
        var sameExerciseResult = await _service.BeginExerciseAsync(propositions[0].Id, accessContext);
        var otherRestrictedResult = await _service.BeginExerciseAsync(propositions[1].Id, accessContext);

        preview.ShouldNotBeNull();
        preview.AccessStatus.ShouldBe(CatalogAccessStatuses.PreviewAvailableFreeIntro);
        firstBegin.ShouldNotBeNull();
        firstBegin.Access.ShouldBe("granted");
        sameExerciseResult.ShouldNotBeNull();
        sameExerciseResult.Access.ShouldBe("granted");
        otherRestrictedResult.ShouldNotBeNull();
        otherRestrictedResult.Access.ShouldBe(CatalogAccessStatuses.UpgradeRequiredToUnlockExercise);
        otherRestrictedResult.AudioUrl.ShouldBeNull();

        var counter = await _context.CatalogAccessCounters.SingleAsync();
        counter.SubjectType.ShouldBe("user");
        counter.SubjectKey.ShouldBe("user-1");
        counter.Feature.ShouldBe(CatalogAccessFeatures.FreeIntro);
        counter.UsedCount.ShouldBe(1);
        (await _context.CatalogExerciseGrants.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task BeginExerciseAsync_WhenRestrictedAndProUser_ShouldReturnGrantedWithAudio()
    {
        var createdAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var propositions = Enumerable.Range(1, 19)
            .Select(index => CreateProposition(
                title: $"Exercise {index}",
                createdAt: createdAt.AddMinutes(index)))
            .ToArray();

        await _context.Propositions.AddRangeAsync(propositions);
        await _context.SaveChangesAsync();

        var result = await _service.BeginExerciseAsync(propositions[0].Id, isPro: true);

        result.ShouldNotBeNull();
        result.Access.ShouldBe("granted");
        result.AudioUrl.ShouldNotBeNullOrWhiteSpace();
        result.Metadata.RequiresPro.ShouldBeTrue();
    }

    [Fact]
    public async Task GetExerciseForComparisonAsync_WhenAllowed_ShouldReturnServerOriginalText()
    {
        var proposition = CreateProposition("Free exercise", text: "Server owned original text.");
        await _context.Propositions.AddAsync(proposition);
        await _context.SaveChangesAsync();

        var result = await _service.GetExerciseForComparisonAsync(proposition.Id, isPro: false);

        result.ShouldNotBeNull();
        result.IsGranted.ShouldBeTrue();
        result.OriginalText.ShouldBe("Server owned original text.");
    }

    [Fact]
    public async Task GetExerciseForComparisonAsync_WhenRestrictedAndFreeUser_ShouldNotReturnOriginalText()
    {
        var createdAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var propositions = Enumerable.Range(1, 19)
            .Select(index => CreateProposition(
                title: $"Exercise {index}",
                createdAt: createdAt.AddMinutes(index),
                text: $"Server text {index}."))
            .ToArray();

        await _context.Propositions.AddRangeAsync(propositions);
        await _context.SaveChangesAsync();

        var result = await _service.GetExerciseForComparisonAsync(propositions[0].Id, isPro: false);

        result.ShouldNotBeNull();
        result.IsGranted.ShouldBeFalse();
        result.OriginalText.ShouldBeNull();
        result.Metadata.RequiresPro.ShouldBeTrue();
    }

    [Fact]
    public async Task GetExerciseForComparisonAsync_WhenRestrictedAndGranted_ShouldReturnOriginalText()
    {
        var propositions = await SeedCatalogPropositionsAsync(19, index => $"Server text {index}.");
        var accessContext = LoggedInFreeContext();

        await _service.BeginExerciseAsync(propositions[0].Id, accessContext);

        var result = await _service.GetExerciseForComparisonAsync(propositions[0].Id, accessContext);

        result.ShouldNotBeNull();
        result.IsGranted.ShouldBeTrue();
        result.OriginalText.ShouldBe("Server text 1.");
        result.Metadata.RequiresPro.ShouldBeTrue();
    }

    private static Proposition CreateProposition(
        string title,
        DateTime? publishedOn = null,
        DateTime? createdAt = null,
        SubjectEnum subject = SubjectEnum.Business,
        ComplexityEnum complexity = ComplexityEnum.Beginner,
        string text = "Test proposition text.") =>
        new()
        {
            PublishedOn = publishedOn ?? new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc),
            SubjectId = subject,
            ComplexityId = complexity,
            AudioFileId = Guid.NewGuid().ToString("N"),
            Voice = "test-voice",
            AudioDurationSeconds = 60,
            Text = text,
            TextLength = text.Length,
            Title = title,
            CreatedAt = createdAt ?? new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc),
            NewsInfo = new NewsInfo
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title,
                Description = "Test news description.",
                Url = $"https://example.com/{Guid.NewGuid():N}",
                ImageUrl = "https://example.com/image.jpg",
                Text = "Test article text.",
                TextLength = "Test article text.".Length
            }
        };

    private async Task<Proposition[]> SeedCatalogPropositionsAsync(
        int count,
        Func<int, string>? textFactory = null)
    {
        var createdAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var propositions = Enumerable.Range(1, count)
            .Select(index => CreateProposition(
                title: $"Exercise {index}",
                createdAt: createdAt.AddMinutes(index),
                text: textFactory?.Invoke(index) ?? $"Server text {index}."))
            .ToArray();

        await _context.Propositions.AddRangeAsync(propositions);
        await _context.SaveChangesAsync();
        return propositions;
    }

    private static PropositionAccessContext AnonymousContext(string fingerprint = "anon-fingerprint-1") =>
        new(
            IsAuthenticated: false,
            IsPro: false,
            UserId: null,
            AnonymousFingerprintHash: fingerprint,
            AnonymousClientIpAddress: "203.0.113.42");

    private static PropositionAccessContext LoggedInFreeContext(string userId = "user-1") =>
        new(
            IsAuthenticated: true,
            IsPro: false,
            UserId: userId,
            AnonymousFingerprintHash: null,
            AnonymousClientIpAddress: null);
}
