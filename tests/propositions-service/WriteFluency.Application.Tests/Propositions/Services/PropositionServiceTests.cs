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

    private static Proposition CreateProposition(
        string title,
        DateTime? publishedOn = null,
        DateTime? createdAt = null,
        SubjectEnum subject = SubjectEnum.Business,
        ComplexityEnum complexity = ComplexityEnum.Beginner) =>
        new()
        {
            PublishedOn = publishedOn ?? new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc),
            SubjectId = subject,
            ComplexityId = complexity,
            AudioFileId = Guid.NewGuid().ToString("N"),
            Voice = "test-voice",
            AudioDurationSeconds = 60,
            Text = "Test proposition text.",
            TextLength = "Test proposition text.".Length,
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
}
