using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WriteFluency.Data;
using Shouldly;
using WriteFluency.Application;

namespace WriteFluency.Propositions;

/// <summary>
/// Testing a scenario where a specific subject (Health) returns no news from the external API.
/// The worker should pass through this and keep generating for the other subjects.
/// </summary>
public class DailyPropositionGeneratorNoNewsTests : ApplicationTestBase
{
    private readonly DailyPropositionGenerator _dailyPropositionGenerator;
    private readonly PropositionOptions _options;
    private readonly IAppDbContext _context;
    private const SubjectEnum _ignoredSubject = SubjectEnum.Politics;

    public DailyPropositionGeneratorNoNewsTests()
    {
        _dailyPropositionGenerator = GetService<DailyPropositionGenerator>();
        _options = GetService<IOptionsMonitor<PropositionOptions>>().CurrentValue;
        _context = GetService<IAppDbContext>();
    }

    protected override void ConfigureMocks(IServiceCollection services, SubjectEnum? subjectWithoutNews = null)
    {
        base.ConfigureMocks(services, subjectWithoutNews: _ignoredSubject);
    }

    [Fact]
    public async Task ShouldKeepGeneratingOtherSubjectsIfOneHasNoNews()
    {
        await _dailyPropositionGenerator.GenerateDailyPropositionsAsync();
        var createdPropositionsCount = await _context.Propositions.CountAsync();
        createdPropositionsCount.ShouldBe(81);
        var propositions = await _context.Propositions.OrderBy(x => x.PublishedOn).ToListAsync();
        VerifyDistributions(propositions);
    }
    
    private void VerifyDistributions(IEnumerable<Proposition> propositions)
    {
        var propositionsCountPerSubject = propositions.GroupBy(p => p.SubjectId)
            .Select(g => new { SubjectId = g.Key, Count = g.Count() });

        // Verify subjects distribution
        var minCount = propositionsCountPerSubject.Min(s => s.Count);
        var maxCount = propositionsCountPerSubject.Max(s => s.Count);
        var diff = maxCount - minCount;
        var complexities = Enum.GetValues<ComplexityEnum>();
        diff.ShouldBeLessThanOrEqualTo(_options.NewsRequestLimit * complexities.Length);

        var propositionsCountPerSubjectAndComplexity = propositions.GroupBy(p => new { p.SubjectId, p.ComplexityId })
            .Select(g => new { SubjectId = g.Key.SubjectId, ComplexityId = g.Key.ComplexityId, Count = g.Count() });

        var subjects = Enum.GetValues<SubjectEnum>().Where(x => x != _ignoredSubject);
        foreach (var subject in subjects)
        {
            // Verify complexities distribution
            var subjectCounts = propositionsCountPerSubjectAndComplexity.Where(s => s.SubjectId == subject);
            var minSubjectCount = subjectCounts.Min(s => s.Count);
            var maxSubjectCount = subjectCounts.Max(s => s.Count);
            diff = maxSubjectCount - minSubjectCount;
            diff.ShouldBeLessThanOrEqualTo(_options.NewsRequestLimit);
        }

        // Verify newest-first cursor behavior
        var orderedPropositions = propositions.OrderByDescending(x => x.PublishedOn).ToList();
        for (var i = 1; i < orderedPropositions.Count; i++)
        {
            orderedPropositions[i].PublishedOn.ShouldBeLessThanOrEqualTo(orderedPropositions[i - 1].PublishedOn);
        }

        orderedPropositions.ShouldAllBe(proposition => proposition.PublishedOn <= DateTime.UtcNow);
    }
}
