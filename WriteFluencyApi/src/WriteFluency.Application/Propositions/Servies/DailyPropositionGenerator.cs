using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        IOptionsMonitor<PropositionOptions> options,
        ILogger<DailyPropositionGenerator> logger,
        IAppDbContext context)
    {
        _createPropositionService = createPropositionService;
        _options = options.CurrentValue;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Create proposition based on the current date, considering the state of the database and configurations.
    /// </summary>
    public async Task GenerateDailyPropositionsAsync(CancellationToken cancellationToken = default)
    {
        await DeleteTodayPropositionsAsync(cancellationToken);

        var summary = await CreateSummaryAsync(cancellationToken);

        var latestProposition = await _context.Propositions.OrderByDescending(x => x.Id)
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        var oldestProposition = await _context.Propositions.OrderBy(p => p.PublishedOn)
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        var (subject, complexity) = GetNextParameters(latestProposition?.SubjectId, latestProposition?.ComplexityId);
        var initialParamters = (subject, complexity);
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
            if ((subject, complexity) == initialParamters)
            {
                var newOldestProposition = newPropositions.OrderBy(p => p.PublishedOn).FirstOrDefault();
                var oldestOption =
                    (newOldestProposition?.PublishedOn ?? DateTime.MaxValue) < (oldestProposition?.PublishedOn ?? DateTime.MaxValue) ?
                    newOldestProposition?.PublishedOn : oldestProposition?.PublishedOn;
                if (oldestOption != null) date =
                    date == DateTime.UtcNow.Date && !(initialParamters == Proposition.Parameters.First()) ?
                        oldestOption.Value : oldestOption.Value.AddDays(-1);

                // Reset the initial parameters mark to the first ones
                initialParamters = Proposition.Parameters.First();
            }

            if (newPropositions.Count % 1000 == 0)
            {
                await SavePropositionsAsync(newPropositions, summary, cancellationToken);
                newPropositions.Clear();
            }
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

    private async Task DeleteTodayPropositionsAsync(CancellationToken cancellationToken = default)
    {
        var todayIds = await _context.Propositions.AsNoTracking()
            .Where(p => p.PublishedOn == DateTime.UtcNow.Date)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        await _context.Propositions.Where(x => todayIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
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
        if (!propositions.Any()) return;

        // Remove duplicated propositions or propositions that have already been created
        propositions = propositions.Where(x => x.Id == default).GroupBy(p => p.NewsInfo.Id).Select(g => g.First());

        try
        {
            // Delete oldest propositions to add new ones without exceeding the limit
            foreach (var subjectEnum in Enum.GetValues<SubjectEnum>())
            {
                var itemSummary = summary.First(s => s.SubjectId == subjectEnum);

                if (itemSummary.Count > _options.PropositionsLimitPerTopic)
                {
                    var ids = await _context.Propositions.AsNoTracking()
                        .Where(p => p.SubjectId == subjectEnum)
                        .OrderBy(x => x.PublishedOn).Take(itemSummary.Count - _options.PropositionsLimitPerTopic)
                        .Select(x => x.Id)
                        .ToListAsync(cancellationToken);
                    await _context.Propositions.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
                }
            }

            _logger.LogInformation($"Saving {propositions.Count()} new propositions");
            // Saves the last proposition separately to keep the order and be able to indentify it
            if (propositions.Count() > 1)
            {
                await _context.Propositions.AddRangeAsync(propositions.Take(propositions.Count() - 1), cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
            await _context.Propositions.AddAsync(propositions.Last(), cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving propositions");
        }
    }

}
