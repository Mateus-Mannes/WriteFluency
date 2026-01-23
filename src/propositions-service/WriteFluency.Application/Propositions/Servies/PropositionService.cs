using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriteFluency.Common;
using WriteFluency.Data;
using WriteFluency.TextComparisons;

namespace WriteFluency.Propositions;

public class PropositionService
{
    private readonly IAppDbContext _context;
    private readonly IFileService _fileService;
    private readonly IGenerativeAIClient _generativeAIClient;
    private readonly ITextToSpeechClient _textToSpeechClient;
    private readonly ILogger<PropositionService> _logger;

    public PropositionService(
        IAppDbContext context, 
        IFileService fileService,
        IGenerativeAIClient generativeAIClient,
        ITextToSpeechClient textToSpeechClient,
        ILogger<PropositionService> logger)
    {
        _context = context;
        _fileService = fileService;
        _generativeAIClient = generativeAIClient;
        _textToSpeechClient = textToSpeechClient;
        _logger = logger;
    }

    public async Task<Proposition?> GetAsync(int id)
    {
        var proposition = await _context.Propositions.FindAsync(id);
        
        if (proposition is null)
        {
            return null;
        }

        return proposition;
    }

    public async Task<PropositionDto> GetAsync(GetPropositionDto dto)
    {
        var propositionQuery = _context.Propositions
            .Where(p => p.SubjectId == dto.Subject && p.ComplexityId == dto.Complexity);
        
        if(dto.AlreadyGeneratedIds is not null && dto.AlreadyGeneratedIds.Any())
        {
            propositionQuery = propositionQuery.Where(p => !dto.AlreadyGeneratedIds.Contains(p.Id));
        }

        var proposition = await propositionQuery.OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        
        if (proposition is null)
        {
            proposition = await _context.Propositions.FirstAsync();
        }

        var audio = await _fileService.GetFileAsync(Proposition.AudioBucketName, proposition.AudioFileId);

        return new PropositionDto(audio, proposition);
    }

    public async Task<Result<Proposition>> RegeneratePropositionAsync(int propositionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var proposition = await _context.Propositions.FindAsync(propositionId);
            if (proposition is null)
            {
                return Result.Fail(new Error($"Proposition with ID {propositionId} not found"));
            }

            _logger.LogInformation("Regenerating proposition {PropositionId} using existing article text", propositionId);

            // Validate existing image if it exists
            if (!string.IsNullOrEmpty(proposition.ImageFileId))
            {
                var imageBytes = await _fileService.GetFileAsync(Proposition.ImageBucketName, proposition.ImageFileId, cancellationToken);
                var imageValidation = await _generativeAIClient.ValidateImageAsync(imageBytes, proposition.NewsInfo.Title, cancellationToken);
                
                if (imageValidation.IsFailed)
                {
                    _logger.LogError("Failed to validate image for proposition {PropositionId}: {Errors}", propositionId, string.Join(", ", imageValidation.Errors));
                    return Result.Fail(new Error("Failed to validate image").CausedBy(imageValidation.Errors));
                }

                if (!imageValidation.Value)
                {
                    _logger.LogWarning("Image validation failed for proposition {PropositionId}. Image is invalid or not coherent with article.", propositionId);
                    return Result.Fail(new Error($"Image is invalid or not coherent with article: {proposition.NewsInfo.Title}"));
                }

                _logger.LogInformation("Image validation passed for proposition {PropositionId}", propositionId);
            }

            // Use the stored article text to regenerate content
            var articleText = proposition.NewsInfo.Text;

            // Regenerate text using AI
            var textResult = await _generativeAIClient.GenerateTextAsync(proposition.ComplexityId, articleText, cancellationToken);
            if (textResult.IsFailed)
            {
                _logger.LogError("Failed to regenerate text for proposition {PropositionId}: {Errors}", propositionId, string.Join(", ", textResult.Errors));
                return Result.Fail(new Error("Failed to regenerate text").CausedBy(textResult.Errors));
            }

            // Update proposition text and title
            proposition.Text = textResult.Value.Content;
            proposition.TextLength = textResult.Value.Content.Length;
            proposition.Title = textResult.Value.Title;

            // Regenerate audio
            var audioResult = await _textToSpeechClient.GenerateAudioAsync(textResult.Value.Content, cancellationToken);
            if (audioResult.IsFailed)
            {
                _logger.LogError("Failed to regenerate audio for proposition {PropositionId}: {Errors}", propositionId, string.Join(", ", audioResult.Errors));
                return Result.Fail(new Error("Failed to regenerate audio").CausedBy(audioResult.Errors));
            }

            // Upload new audio file (will overwrite if using same filename)
            var uploadResult = await _fileService.UploadFileAsync(
                Proposition.AudioBucketName, 
                audioResult.Value.Audio, 
                "mp3", 
                "audio/mpeg", 
                cancellationToken);
            
            if (uploadResult.IsFailed)
            {
                _logger.LogError("Failed to upload new audio for proposition {PropositionId}: {Errors}", propositionId, string.Join(", ", uploadResult.Errors));
                return Result.Fail(new Error("Failed to upload new audio").CausedBy(uploadResult.Errors));
            }

            proposition.AudioFileId = uploadResult.Value;
            proposition.Voice = audioResult.Value.Voice;
            proposition.AudioDurationSeconds = audioResult.Value.DurationSeconds;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully regenerated proposition {PropositionId}", propositionId);
            return Result.Ok(proposition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating proposition {PropositionId}", propositionId);
            return Result.Fail(new Error($"Error regenerating proposition: {ex.Message}"));
        }
    }

    public async Task<PagedResultDto<ExerciseListItemDto>> GetExercisesAsync(
        ExerciseFilterDto filter, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Propositions.AsQueryable();
        
        // Apply filters
        bool hasSubjectFilter = filter.Topic.HasValue;
        bool hasLevelFilter = filter.Level.HasValue;
        
        if (hasSubjectFilter)
        {
            query = query.Where(p => p.SubjectId == filter.Topic!.Value);
        }
        
        if (hasLevelFilter)
        {
            query = query.Where(p => p.ComplexityId == filter.Level!.Value);
        }
        
        // Order by date
        query = filter.SortBy.ToLower() == "oldest" 
            ? query.OrderBy(p => p.PublishedOn).ThenBy(p => p.Id)
            : query.OrderByDescending(p => p.PublishedOn).ThenBy(p => p.Id);
        
        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);
        
        // Apply pagination and map to DTOs
        var items = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => new ExerciseListItemDto(
                p.Id,
                p.Title,
                p.SubjectId,
                p.ComplexityId,
                p.PublishedOn,
                p.ImageFileId,
                p.AudioDurationSeconds
            ))
            .ToListAsync(cancellationToken);
        
        // When no filters are applied, reorder to alternate subjects and complexities
        if (!hasSubjectFilter && !hasLevelFilter)
        {
            var reordered = new List<ExerciseListItemDto>();
            var remaining = new List<ExerciseListItemDto>(items);
            
            while (remaining.Any())
            {
                ExerciseListItemDto? next = null;
                
                if (reordered.Any())
                {
                    var last = reordered.Last();
                    // Try to find an item with different subject AND complexity
                    next = remaining.FirstOrDefault(p => 
                        p.Topic != last.Topic && p.Level != last.Level);
                    
                    // If not possible, try to find different subject OR different complexity
                    next ??= remaining.FirstOrDefault(p => 
                        p.Topic != last.Topic || p.Level != last.Level);
                }
                
                // If still no match (first item or no different options), take the first available
                next ??= remaining.First();
                
                reordered.Add(next);
                remaining.Remove(next);
            }
            
            items = reordered;
        }
        
        return new PagedResultDto<ExerciseListItemDto>(
            items,
            totalCount,
            filter.PageNumber,
            filter.PageSize
        );
    }
}
