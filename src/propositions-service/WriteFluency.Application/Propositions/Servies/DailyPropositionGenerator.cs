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

    private DateTime _startDate;
    private HashSet<CreatePropositionDto> _generatedParameters = new();

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
        _logger.LogInformation($"Starting daily proposition generation");
        try
        {
            await GenerateAsync(cancellationToken);
            _generatedParameters.Clear();
            _logger.LogInformation($"Daily proposition generation completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Daily proposition generation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during daily proposition generation");
        }
    }

    private async Task GenerateAsync(CancellationToken cancellationToken = default)
    {
        _startDate = DateTime.UtcNow.Date.AddDays(-2);

        var todayGeneratedParameters = await GetTodayGeneratedParametersAsync(cancellationToken);
        var summary = await CreateSummaryAsync(cancellationToken);
        var newPropositions = new List<Proposition>();
        
        // Start with parameters with least propositions
        var parameters = summary.OrderBy(x => x.Count).ThenBy(x => x.SubjectId).First();
        var date = _startDate;

        for (int i = 0; i < _options.DailyRequestsLimit; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation($"Generating proposition for {parameters.SubjectId} - {parameters.ComplexityId} - {date}");

            var countPerSubject = summary.Where(x => x.SubjectId == parameters.SubjectId).Sum(x => x.Count);
            var isUnderLimit = countPerSubject < _options.PropositionsLimitPerTopic;
            var isForToday = date == _startDate;
            
            // Generate if under limit OR if generating for today (today always gets priority)
            if (isUnderLimit || isForToday)
            {
                // Skip if already generated for today
                var alreadyDoneForToday = isForToday && todayGeneratedParameters.Any(g => 
                    g.SubjectId == parameters.SubjectId && g.ComplexityId == parameters.ComplexityId);
                
                if (!alreadyDoneForToday)
                {
                    var dto = new CreatePropositionDto(date, parameters.ComplexityId, parameters.SubjectId);
                    var result = await _createPropositionService.CreatePropositionsAsync(dto, _options.NewsRequestLimit, cancellationToken);
                    
                    parameters.Count += result.Count();
                    newPropositions.AddRange(result);
                    _generatedParameters.Add(dto);
                    
                    if (isForToday)
                    {
                        todayGeneratedParameters.Add((parameters.SubjectId, parameters.ComplexityId));
                    }
                }
            }

            // Select next parameters (lowest count that's still under limit)
            var parametersList = summary
                .OrderBy(x => x.Count)
                .ThenBy(x => x.SubjectId)
                .Where(x => summary.Where(y => y.SubjectId == x.SubjectId).Sum(y => y.Count) < _options.PropositionsLimitPerTopic);
            
            if (!parametersList.Any()) break;
            
            parameters = parametersList.First();
            
            // Prefer parameters not yet generated in this run
            var notYetGenerated = parametersList.FirstOrDefault(x => 
                !_generatedParameters.Contains(new CreatePropositionDto(date, x.ComplexityId, x.SubjectId)));
            if (notYetGenerated != null) parameters = notYetGenerated;
            
            // Determine date for next iteration
            var isDoneForToday = todayGeneratedParameters.Any(g => 
                g.SubjectId == parameters.SubjectId && g.ComplexityId == parameters.ComplexityId);
            
            if (isDoneForToday)
            {
                // This combination is done for today, use previous days
                date = parameters.OldestPublishedOn?.Date.AddDays(-1) ?? _startDate.AddDays(-1);
                parameters.OldestPublishedOn = date;
            }
            else
            {
                // Still need to generate for today
                date = _startDate;
            }

            if (newPropositions.Count % 1000 == 0)
            {
                await SavePropositionsAsync(newPropositions, summary, cancellationToken);
                newPropositions.Clear();
            }
        }

        await SavePropositionsAsync(newPropositions, summary, cancellationToken);
    }

    private async Task<List<(SubjectEnum SubjectId, ComplexityEnum ComplexityId)>> GetTodayGeneratedParametersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Checking what has been generated for today's date");
        var todayParameters = await _context.Propositions.AsNoTracking()
            .Where(p => p.PublishedOn == _startDate)
            .GroupBy(p => new { p.SubjectId, p.ComplexityId })
            .Select(g => new { g.Key.SubjectId, g.Key.ComplexityId })
            .ToListAsync(cancellationToken);
        
        return todayParameters.Select(p => (p.SubjectId, p.ComplexityId)).ToList();
    }

    private async Task<List<PropositionSummaryDto>> CreateSummaryAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Querying propositions summary");
        // Need to now how many propositions I have for each subject
        var counter = await _context.Propositions.AsNoTracking()
            .GroupBy(p => new { p.SubjectId, p.ComplexityId })
            .OrderBy(g => g.Key.SubjectId).ThenBy(g => g.Key.ComplexityId)
            .Select(g => new PropositionSummaryDto
            {
                SubjectId = g.Key.SubjectId,
                ComplexityId = g.Key.ComplexityId,
                OldestPublishedOn = g.Min(p => p.PublishedOn),
                Count = g.Count()
            }).ToListAsync(cancellationToken);

        // Adding subjets/complexities that have not been created yet
        foreach (var (subject, complexity) in Proposition.Parameters)
        {
            if (!counter.Any(x => x.SubjectId == subject && x.ComplexityId == complexity))
            {
                counter.Add(new PropositionSummaryDto
                {
                    SubjectId = subject,
                    ComplexityId = complexity,
                    Count = 0
                });
            }
        }

        return counter;
    }

    private async Task SavePropositionsAsync(
        IEnumerable<Proposition> propositions,
        List<PropositionSummaryDto> summary,
        CancellationToken cancellationToken = default)
    {
        if (!propositions.Any()) return;

        try
        {
            // Delete oldest propositions to add new ones without exceeding the limit
            _logger.LogInformation($"Deleting oldest propositions to keep the limit of {_options.PropositionsLimitPerTopic} per subject");
            foreach (var subjectEnum in Enum.GetValues<SubjectEnum>())
            {
                var countPerSubject = summary.Where(x => x.SubjectId == subjectEnum).Sum(x => x.Count);
                if (countPerSubject > _options.PropositionsLimitPerTopic)
                {
                    var ids = await _context.Propositions.AsNoTracking()
                        .Where(p => p.SubjectId == subjectEnum)
                        .OrderBy(x => x.PublishedOn).Take(countPerSubject - _options.PropositionsLimitPerTopic)
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
