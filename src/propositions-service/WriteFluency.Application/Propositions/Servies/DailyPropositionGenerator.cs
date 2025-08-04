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

        await DeleteTodayPropositionsAsync(cancellationToken);

        var summary = await CreateSummaryAsync(cancellationToken);
        var parameters = summary.OrderBy(x => x.Count).ThenBy(x => x.SubjectId).First(); // Always generates for the subjects/complexities with less propositions
        var date = _startDate;
        var newPropositions = new List<Proposition>();

        for (int i = 0; i < _options.DailyRequestsLimit; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation($"Generating proposition for {parameters.SubjectId} - {parameters.ComplexityId} - {date}");

            // Generates more propositions if is is still under the limit, or if it is a generation for the currente date
            var countPerSubject = summary.Where(x => x.SubjectId == parameters.SubjectId).Sum(x => x.Count);
            if (countPerSubject < _options.PropositionsLimitPerTopic || date == _startDate)
            {
                var dto = new CreatePropositionDto(date, parameters.ComplexityId, parameters.SubjectId);
                var result = await _createPropositionService.CreatePropositionsAsync(dto, _options.NewsRequestLimit, cancellationToken);
                parameters.Count += result.Count();
                newPropositions.AddRange(result);
                _generatedParameters.Add(dto);
            }

            // Updating parameters for the next iteration:

            // Always generates for the subjects/complexities with less propositions, looking for options under the limit
            var parametersList = summary.OrderBy(x => x.Count).ThenBy(x => x.SubjectId).Where(x =>
                summary.Where(y => y.SubjectId == x.SubjectId).Sum(y => y.Count) < _options.PropositionsLimitPerTopic);
            if (!parametersList.Any()) break;
            
            parameters = parametersList.First();
            // Prioritizing parameters that have not been generated yet
            var newParameters = parametersList.FirstOrDefault(x => !_generatedParameters.Contains(new CreatePropositionDto(date, x.ComplexityId, x.SubjectId)));
            if(newParameters is not null) parameters = newParameters;
            
            // If the next parameters with less propositions have already been generated for today, keep going to the previous day
            // or the previous day to the oldest one generated previously
            if ((_generatedParameters.Any(x => x.Subject == parameters.SubjectId && x.Complexity == parameters.ComplexityId &&
                x.PublishedOn == _startDate)))
            {
                date = _startDate.AddDays(-1);
                if (parameters.OldestPublishedOn.HasValue)
                    date = parameters.OldestPublishedOn.Value.Date.AddDays(-1);
                parameters.OldestPublishedOn = date;
            }
            else
            {
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

    private async Task DeleteTodayPropositionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Deleting today's propositions");
        var todayIds = await _context.Propositions.AsNoTracking()
            .Where(p => p.PublishedOn == _startDate)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        await _context.Propositions.Where(x => todayIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
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
