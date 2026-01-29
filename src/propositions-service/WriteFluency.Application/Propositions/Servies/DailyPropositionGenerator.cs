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
        _logger.LogInformation($"Starting daily proposition generation");
        try
        {
            await GenerateAsync(cancellationToken);
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
        var targetDate = DateTime.UtcNow.Date.AddDays(-2);

        // Get generation statistics for each subject/complexity combination
        var generationStats = await GetGenerationStatsAsync(cancellationToken);

        for (int i = 0; i < _options.DailyRequestsLimit; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Find the subject/complexity combination with the least generation attempts
            var targetParameters = generationStats
                .OrderBy(x => x.LogCount)
                .ThenBy(x => x.SubjectId)
                .ThenBy(x => x.ComplexityId)
                .First();

            // Check if we're over the proposition limit for this subject (count actual propositions)
            var subjectPropositionCount = await _context.Propositions
                .Where(x => x.SubjectId == targetParameters.SubjectId)
                .CountAsync(cancellationToken);
            
            var isOverLimit = subjectPropositionCount >= _options.PropositionsLimitPerTopic;

            // Find the most recent date that hasn't been generated yet for this combination
            var dateToGenerate = await GetNextDateToGenerateAsync(
                targetParameters.SubjectId, 
                targetParameters.ComplexityId, 
                targetDate,
                isOverLimit,
                cancellationToken);

            if (dateToGenerate == null)
            {
                _logger.LogWarning($"No available date found for {targetParameters.SubjectId} - {targetParameters.ComplexityId}");
                // Remove this combination from consideration
                generationStats.Remove(targetParameters);
                
                if (!generationStats.Any())
                {
                    _logger.LogWarning("No more combinations available for generation");
                    break;
                }
                continue;
            }

            _logger.LogInformation($"Generating proposition for {targetParameters.SubjectId} - {targetParameters.ComplexityId} - {dateToGenerate}");

            var dto = new CreatePropositionDto(dateToGenerate.Value, targetParameters.ComplexityId, targetParameters.SubjectId);
            
            PropositionGenerationLog? generationLog = null;
            try
            {
                // Create log entry first to get the ID
                generationLog = new PropositionGenerationLog
                {
                    GenerationDate = dto.PublishedOn.Date,
                    SubjectId = dto.Subject,
                    ComplexityId = dto.Complexity,
                    SuccessCount = 0,
                    Success = false,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.PropositionGenerationLogs.AddAsync(generationLog, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                var result = await _createPropositionService.CreatePropositionsAsync(
                    dto, 
                    _options.NewsRequestLimit, 
                    cancellationToken);

                var successCount = result.Count();
                var success = successCount > 0;

                // Update the log with results
                generationLog.SuccessCount = successCount;
                generationLog.Success = success;
                await _context.SaveChangesAsync(cancellationToken);

                // Always increment log count (tracks attempts, not success)
                targetParameters.LogCount++;
                
                if (success)
                {
                    // Link propositions to the generation log and save immediately
                    foreach (var proposition in result)
                    {
                        proposition.PropositionGenerationLogId = generationLog.Id;
                    }
                    
                    await _context.Propositions.AddRangeAsync(result, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    
                    // Check and soft delete if over limit
                    await CleanupOldPropositionsAsync(dto.Subject, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating proposition for {targetParameters.SubjectId} - {targetParameters.ComplexityId} - {dateToGenerate}");
                
                // Update log as failed if it was created
                if (generationLog != null)
                {
                    generationLog.Success = false;
                    generationLog.SuccessCount = 0;
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }

    private async Task<List<GenerationStatsDto>> GetGenerationStatsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying generation statistics");
        
        // Count generation attempts (logs) for each combination
        var stats = await _context.PropositionGenerationLogs
            .GroupBy(x => new { x.SubjectId, x.ComplexityId })
            .Select(g => new GenerationStatsDto
            {
                SubjectId = g.Key.SubjectId,
                ComplexityId = g.Key.ComplexityId,
                LogCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Add combinations that have never been attempted
        foreach (var (subject, complexity) in Proposition.Parameters)
        {
            if (!stats.Any(x => x.SubjectId == subject && x.ComplexityId == complexity))
            {
                stats.Add(new GenerationStatsDto
                {
                    SubjectId = subject,
                    ComplexityId = complexity,
                    LogCount = 0
                });
            }
        }

        return stats;
    }

    private async Task<DateTime?> GetNextDateToGenerateAsync(
        SubjectEnum subjectId, 
        ComplexityEnum complexityId, 
        DateTime targetDate,
        bool isOverLimit,
        CancellationToken cancellationToken = default)
    {
        // Check if target date has already been attempted
        var targetDateHasLog = await _context.PropositionGenerationLogs
            .AnyAsync(x => x.SubjectId == subjectId 
                && x.ComplexityId == complexityId 
                && x.GenerationDate.Date == targetDate,
                cancellationToken);

        if (!targetDateHasLog)
        {
            return targetDate;
        }

        // If over limit and target date already done, stop
        if (isOverLimit)
        {
            return null;
        }

        // Target date already done, go back from oldest log date
        var oldestLogDate = await _context.PropositionGenerationLogs
            .Where(x => x.SubjectId == subjectId && x.ComplexityId == complexityId)
            .MinAsync(x => (DateTime?)x.GenerationDate, cancellationToken);

        return oldestLogDate?.Date.AddDays(-1) ?? targetDate;
    }

    private async Task CleanupOldPropositionsAsync(
        SubjectEnum subjectId,
        CancellationToken cancellationToken = default)
    {
        var count = await _context.Propositions
            .Where(p => p.SubjectId == subjectId)
            .CountAsync(cancellationToken);

        if (count > _options.PropositionsLimitPerTopic)
        {
            var toDelete = count - _options.PropositionsLimitPerTopic;
            
            var propositionsToDelete = await _context.Propositions
                .Where(p => p.SubjectId == subjectId)
                .OrderBy(x => x.PublishedOn)
                .Take(toDelete)
                .ToListAsync(cancellationToken);
            
            foreach (var proposition in propositionsToDelete)
            {
                proposition.IsDeleted = true;
                proposition.DeletedAt = DateTime.UtcNow;
            }
            
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation($"Soft deleted {toDelete} oldest propositions for subject {subjectId}");
        }
    }

    private class GenerationStatsDto
    {
        public SubjectEnum SubjectId { get; set; }
        public ComplexityEnum ComplexityId { get; set; }
        public int LogCount { get; set; }
    }
}
