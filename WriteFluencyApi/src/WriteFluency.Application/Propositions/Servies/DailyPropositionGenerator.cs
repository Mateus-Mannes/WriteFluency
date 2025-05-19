using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriteFluency.Data;

namespace WriteFluency.Propositions;

public class DailyPropositionGenerator
{
    private readonly CreatePropositionService _createPropositionService;
    private readonly PropositionOptions _options;
    private readonly ILogger<DailyPropositionGenerator> _logger;
    private readonly IAppDbContext _context;

    public DailyPropositionGenerator(
        CreatePropositionService createPropositionService,
        PropositionOptions options,
        ILogger<DailyPropositionGenerator> logger,
        IAppDbContext context)
    {
        _createPropositionService = createPropositionService;
        _options = options;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Create proposition based on the current date, considering the state of the database and configurations.
    /// </summary>
    public async Task GenerateDailyPropositionsAsync(CancellationToken cancellationToken = default)
    {
        var summary = await CreateSummaryAsync(cancellationToken);
        var latestProposition = await _context.Propositions.OrderByDescending(p => p.CreatedAt)
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var oldestProposition = await _context.Propositions.OrderBy(p => p.CreatedAt)
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        var (subject, complexity) = GetNextParameters(latestProposition?.SubjectId, latestProposition?.ComplexityId);
        var date = DateTime.UtcNow.Date;

        var newPropositions = new List<Proposition>();
        var loopCounter = 0;

        _logger.LogInformation($"Starting daily proposition generation for {subject} - {complexity} - {date}");
        while (loopCounter < _options.DailyRequestLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation($"Generating proposition for {subject} - {complexity} - {date}");

            var itemSummary = summary.First(s => s.SubjectId == subject);

            // Generates more propositions if is is still under the limit, or if it is a generation for the currente date
            if (itemSummary.Count < _options.PropositionsLimitPerTopic || date == DateTime.UtcNow.Date)
            {
                var dto = new CreatePropositionDto(date, complexity, subject);
                var result = await _createPropositionService.CreatePropositionsAsync(dto, _options.NewsRequestLimit, cancellationToken);
                itemSummary.Count += result.Count();
                newPropositions.AddRange(result);
            }

            (subject, complexity) = GetNextParameters(subject, complexity);
            loopCounter++;

            // After a full parameter cycle, shift the date to one day before the earliest known article —
            // comparing both existing data and newly generated propositions to avoid overlaps.
            if (loopCounter % Proposition.Parameters.Count == 0)
            {
                var newOldestProposition = newPropositions.OrderBy(p => p.PublishedOn).FirstOrDefault();
                var oldestOption = newOldestProposition?.PublishedOn < oldestProposition?.PublishedOn
                    ? newOldestProposition : oldestProposition;
                if (oldestOption != null) date = oldestOption.PublishedOn.AddDays(-1);
            }

            if(newPropositions.Count % 1000 == 0) await SavePropositionsAsync(newPropositions, summary, cancellationToken);
        }

        await SavePropositionsAsync(newPropositions, summary, cancellationToken);
    }

    private (SubjectEnum, ComplexityEnum) GetNextParameters(SubjectEnum? subject, ComplexityEnum? complexity)
    {
        if (subject == null || complexity == null)
            return (Proposition.Parameters.First().Item1, Proposition.Parameters.First().Item2);
        var currentIndex = Proposition.Parameters.FindIndex(c => c.Item1 == subject && c.Item2 == complexity);
        var nextIndex = (currentIndex + 1) % Proposition.Parameters.Count;
        return (Proposition.Parameters[nextIndex].Item1, Proposition.Parameters[nextIndex].Item2);
    }

    private async Task<List<PropositionSummaryDto>> CreateSummaryAsync(CancellationToken cancellationToken = default)
    {
        // Need to now how many propositions I have for each subject
        var summary = await _context.Propositions.AsNoTracking()
            .GroupBy(p => p.SubjectId)
            .Select(g => new PropositionSummaryDto
            {
                SubjectId = g.Key,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Adding subjets that have not been created yet
        foreach (var subjectEnum in Enum.GetValues<SubjectEnum>())
        {
            if (!summary.Any(s => s.SubjectId == subjectEnum))
            {
                summary.Add(new PropositionSummaryDto
                {
                    SubjectId = subjectEnum,
                    Count = 0
                });
            }
        }

        return summary;
    }

    private async Task SavePropositionsAsync(IEnumerable<Proposition> propositions, List<PropositionSummaryDto> summary, CancellationToken cancellationToken = default)
    {
        // Remove duplicated propositions or propositions that have already been created
        propositions = propositions.Where(x => x.Id == default).GroupBy(p => p.NewsInfo.Id).Select(g => g.First());

        try
        {
            // Delete oldest propositions to add new ones without exceeding the limit
            foreach (var subjectEnum in Enum.GetValues<SubjectEnum>())
            {
                var itemSummary = summary.First(s => s.SubjectId == subjectEnum);
                if (itemSummary.Count >= _options.PropositionsLimitPerTopic)
                {
                    var ids = await _context.Propositions.AsNoTracking()
                        .Where(p => p.SubjectId == subjectEnum)
                        .OrderBy(x => x.CreatedAt).Take(itemSummary.Count - _options.PropositionsLimitPerTopic)
                        .Select(x => x.Id)
                        .ToListAsync(cancellationToken);
                    await _context.Propositions.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
                }
            }

            _logger.LogInformation($"Saving {propositions.Count()} new propositions");
            await _context.Propositions.AddRangeAsync(propositions, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving propositions");
        }
    }

}
