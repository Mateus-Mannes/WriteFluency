using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriteFluency.Data;
using Shouldly;
using WriteFluency.Application;

namespace WriteFluency.Propositions;

public class DailyPropositionGeneratorTests : ApplicationTestBase
{
    private readonly DailyPropositionGenerator _dailyPropositionGenerator;
    private readonly PropositionOptions _options;
    private readonly IAppDbContext _context;

    public DailyPropositionGeneratorTests()
    {
        _dailyPropositionGenerator = GetService<DailyPropositionGenerator>();
        _options = GetService<IOptionsMonitor<PropositionOptions>>().CurrentValue;
        _context = GetService<IAppDbContext>();
    }

    [Fact]
    public async Task ShouldGeneratePropositionsFromZero()
    {
        await _dailyPropositionGenerator.GenerateDailyPropositionsAsync();

        var createdPropositionsCount = await _context.Propositions.CountAsync();
        createdPropositionsCount.ShouldBe(_options.DailyRequestsLimit * _options.NewsRequestLimit);

        var propositions = await _context.Propositions.OrderBy(x => x.PublishedOn).ToListAsync();

        VerifyDistributions(propositions);
    }
    
    [Fact]
    public async Task ShouldGeneratePropositionsUntilLimit()
    {
        var subjects = Enum.GetValues<SubjectEnum>();
        var totalPropositionsOnLimit = subjects.Length * _options.PropositionsLimitPerTopic;

        var requestCountToLimit = totalPropositionsOnLimit /
            (_options.DailyRequestsLimit * _options.NewsRequestLimit - Proposition.Parameters.Count);

        // Initial Generation
        await _dailyPropositionGenerator.GenerateDailyPropositionsAsync();
        var propositions = await _context.Propositions.AsNoTracking().OrderBy(x => x.PublishedOn).ToListAsync();
        propositions.Count.ShouldBe(_options.DailyRequestsLimit * _options.NewsRequestLimit);
        VerifyDistributions(propositions);
        var propositionsCount = propositions.Count;

        while (propositionsCount < totalPropositionsOnLimit)
        {
            await _dailyPropositionGenerator.GenerateDailyPropositionsAsync();
            propositions = await _context.Propositions.AsNoTracking().OrderByDescending(x => x.PublishedOn).ToListAsync();
            var newPropositionsCount = propositions.Count - propositionsCount;
            var pendingToLimit = totalPropositionsOnLimit - propositionsCount;
            var expectedGeneration = _options.NewsRequestLimit * (_options.DailyRequestsLimit - Proposition.Parameters.Count);
            if (expectedGeneration < pendingToLimit) newPropositionsCount.ShouldBe(expectedGeneration);
            propositionsCount += newPropositionsCount;
            VerifyDistributions(propositions);
        }

        propositionsCount.ShouldBe(totalPropositionsOnLimit);

        // Do more requests to check if it doesn't generate more
        await _dailyPropositionGenerator.GenerateDailyPropositionsAsync();
        await _dailyPropositionGenerator.GenerateDailyPropositionsAsync();
        propositionsCount = await _context.Propositions.AsNoTracking().CountAsync();
        propositionsCount.ShouldBe(totalPropositionsOnLimit);
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

        var subjects = Enum.GetValues<SubjectEnum>();
        foreach (var subject in subjects)
        {
            // Verify complexities distribution
            var subjectCounts = propositionsCountPerSubjectAndComplexity.Where(s => s.SubjectId == subject);
            var minSubjectCount = subjectCounts.Min(s => s.Count);
            var maxSubjectCount = subjectCounts.Max(s => s.Count);
            diff = maxSubjectCount - minSubjectCount;
            diff.ShouldBeLessThanOrEqualTo(_options.NewsRequestLimit);
        }

        // Verify dates distribution
        var date = DateTime.UtcNow.Date.AddDays(-2);
        int count = 0;
        foreach (var proposition in propositions.OrderByDescending(x => x.PublishedOn))
        {
            if (count > 1 && count % (Proposition.Parameters.Count * _options.NewsRequestLimit) == 0)
                date = date.AddDays(-1).Date;
            proposition.PublishedOn.ShouldBe(date, customMessage: $"Proposition index {count} has wrong date");
            count++;
        }
    }
}
