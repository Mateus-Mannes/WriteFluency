using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NpgsqlTypes;
using System.Text.RegularExpressions;
using WriteFluency.Common;
using WriteFluency.Data;
using WriteFluency.TextComparisons;

namespace WriteFluency.Propositions;

public class PropositionService
{
    private const int FreeCatalogExerciseLimit = 18;
    private static readonly TimeSpan AudioPresignedUrlLifetime = TimeSpan.FromHours(8);
    private const string AccessGranted = "granted";
    private const string AccessProRequired = "pro_required";
    private const int MaxSearchTerms = 8;
    private const double TitleTrigramMatchThreshold = 0.35;
    private const double TextTrigramMatchThreshold = 0.45;
    private static readonly Regex SearchTermRegex = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);

    private readonly IAppDbContext _context;
    private readonly IFileService _fileService;
    private readonly IGenerativeAIClient _generativeAIClient;
    private readonly ITextToSpeechClient _textToSpeechClient;
    private readonly CatalogAccessTeaserService _catalogAccessTeaserService;
    private readonly ILogger<PropositionService> _logger;

    private sealed record PropositionAccessRow(
        int Id,
        DateTime PublishedOn,
        SubjectEnum SubjectId,
        ComplexityEnum ComplexityId,
        int AudioDurationSeconds,
        string Title,
        string? ImageFileId,
        string? NewsUrl,
        string AudioFileId,
        string Text);

    public PropositionService(
        IAppDbContext context, 
        IFileService fileService,
        IGenerativeAIClient generativeAIClient,
        ITextToSpeechClient textToSpeechClient,
        CatalogAccessTeaserService catalogAccessTeaserService,
        ILogger<PropositionService> logger)
    {
        _context = context;
        _fileService = fileService;
        _generativeAIClient = generativeAIClient;
        _textToSpeechClient = textToSpeechClient;
        _catalogAccessTeaserService = catalogAccessTeaserService;
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

    public async Task<PropositionMetadataDto?> GetMetadataAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var proposition = await GetAccessRowAsync(id, cancellationToken);

        if (proposition is null)
        {
            return null;
        }

        return ToMetadata(
            proposition,
            requiresPro: !await IsInFreeCatalogWindowAsync(id, cancellationToken));
    }

    public async Task<BeginExerciseResultDto?> BeginExerciseAsync(
        int id,
        bool isPro,
        CancellationToken cancellationToken = default)
    {
        var accessContext = new PropositionAccessContext(
            IsAuthenticated: isPro,
            IsPro: isPro,
            UserId: isPro ? "pro-user" : null,
            AnonymousFingerprintHash: null);
        return await BeginExerciseAsync(id, accessContext, cancellationToken);
    }

    public async Task<PreviewExerciseAccessResultDto?> PreviewExerciseAccessAsync(
        int id,
        PropositionAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var proposition = await GetAccessRowAsync(id, cancellationToken);

        if (proposition is null)
        {
            return null;
        }

        var requiresPro = !await IsInFreeCatalogWindowAsync(id, cancellationToken);
        var metadata = ToMetadata(proposition, requiresPro);
        var decision = await _catalogAccessTeaserService.DecidePreviewAsync(
            accessContext,
            id,
            requiresPro,
            cancellationToken);

        if (!decision.AllowsAudio)
        {
            return new PreviewExerciseAccessResultDto(
                decision.AccessStatus,
                AudioUrl: null,
                AudioExpiresAtUtc: null,
                metadata);
        }

        var audioAccess = await CreateAudioAccessAsync(proposition, cancellationToken);
        return new PreviewExerciseAccessResultDto(
            decision.AccessStatus,
            audioAccess.AudioUrl,
            audioAccess.AudioExpiresAtUtc,
            metadata);
    }

    public async Task<BeginExerciseResultDto?> BeginExerciseAsync(
        int id,
        PropositionAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var proposition = await GetAccessRowAsync(id, cancellationToken);

        if (proposition is null)
        {
            return null;
        }

        var requiresPro = !await IsInFreeCatalogWindowAsync(id, cancellationToken);
        var metadata = ToMetadata(proposition, requiresPro);
        var decision = await _catalogAccessTeaserService.ClaimBeginAsync(
            accessContext,
            id,
            requiresPro,
            cancellationToken);

        if (!decision.AllowsAudio)
        {
            return new BeginExerciseResultDto(decision.AccessStatus, null, null, metadata);
        }

        var audioAccess = await CreateAudioAccessAsync(proposition, cancellationToken);
        return new BeginExerciseResultDto(AccessGranted, audioAccess.AudioUrl, audioAccess.AudioExpiresAtUtc, metadata);
    }

    public async Task<ExerciseComparisonAccessResult?> GetExerciseForComparisonAsync(
        int id,
        bool isPro,
        CancellationToken cancellationToken = default)
    {
        var accessContext = new PropositionAccessContext(
            IsAuthenticated: isPro,
            IsPro: isPro,
            UserId: isPro ? "pro-user" : null,
            AnonymousFingerprintHash: null);
        return await GetExerciseForComparisonAsync(id, accessContext, cancellationToken);
    }

    public async Task<ExerciseComparisonAccessResult?> GetExerciseForComparisonAsync(
        int id,
        PropositionAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var proposition = await GetAccessRowAsync(id, cancellationToken);

        if (proposition is null)
        {
            return null;
        }

        var requiresPro = !await IsInFreeCatalogWindowAsync(id, cancellationToken);
        var isGranted = await _catalogAccessTeaserService.CanCompareAsync(
            accessContext,
            id,
            requiresPro,
            cancellationToken);

        return new ExerciseComparisonAccessResult(
            isGranted,
            ToMetadata(proposition, requiresPro),
            OriginalText: isGranted ? proposition.Text : null);
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
        var freeExerciseIds = await GetFreeExerciseIdsAsync(cancellationToken);

        var freeExerciseIdSet = freeExerciseIds.ToHashSet();
        var query = _context.Propositions.AsQueryable();
        
        // Apply filters
        bool hasSubjectFilter = filter.Topic.HasValue;
        bool hasLevelFilter = filter.Level.HasValue;
        var searchCriteria = BuildSearchCriteria(filter.SearchText);
        bool hasSearchFilter = searchCriteria is not null;
        
        if (hasSubjectFilter)
        {
            query = query.Where(p => p.SubjectId == filter.Topic!.Value);
        }
        
        if (hasLevelFilter)
        {
            query = query.Where(p => p.ComplexityId == filter.Level!.Value);
        }

        if (hasSearchFilter)
        {
            query = query.Where(p =>
                EF.Property<NpgsqlTsVector>(p, "SearchVector")
                    .Matches(EF.Functions.ToTsQuery("english", searchCriteria!.PrefixTsQuery))
                || EF.Functions.TrigramsWordSimilarity(p.Title, searchCriteria.TrigramText) >= TitleTrigramMatchThreshold
                || EF.Functions.TrigramsWordSimilarity(p.Text, searchCriteria.TrigramText) >= TextTrigramMatchThreshold);
        }
        
        // Order by creation date so the exercises grid shows the latest exercises added to WriteFluency.
        var sortBy = filter.SortBy.ToLowerInvariant();

        if (hasSearchFilter)
        {
            query = sortBy == "oldest"
                ? query
                    .OrderByDescending(p => EF.Property<NpgsqlTsVector>(p, "SearchVector")
                        .Rank(EF.Functions.ToTsQuery("english", searchCriteria!.PrefixTsQuery))
                        + EF.Functions.TrigramsWordSimilarity(p.Title, searchCriteria.TrigramText)
                        + (EF.Functions.TrigramsWordSimilarity(p.Text, searchCriteria.TrigramText) * 0.25))
                    .ThenBy(p => p.CreatedAt)
                    .ThenBy(p => p.Id)
                : query
                    .OrderByDescending(p => EF.Property<NpgsqlTsVector>(p, "SearchVector")
                        .Rank(EF.Functions.ToTsQuery("english", searchCriteria!.PrefixTsQuery))
                        + EF.Functions.TrigramsWordSimilarity(p.Title, searchCriteria.TrigramText)
                        + (EF.Functions.TrigramsWordSimilarity(p.Text, searchCriteria.TrigramText) * 0.25))
                    .ThenByDescending(p => p.CreatedAt)
                    .ThenByDescending(p => p.Id);
        }
        else
        {
            query = sortBy == "oldest"
                ? query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
                : query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id);
        }
        
        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);
        
        // Apply pagination and map to DTOs
        var itemRows = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => new
            {
                p.Id,
                p.Title,
                Topic = p.SubjectId,
                Level = p.ComplexityId,
                p.PublishedOn,
                p.ImageFileId,
                p.AudioDurationSeconds,
                NewsUrl = p.NewsInfo.Url
            })
            .ToListAsync(cancellationToken);

        var items = itemRows
            .Select(p => new ExerciseListItemDto(
                p.Id,
                p.Title,
                p.Topic,
                p.Level,
                p.PublishedOn,
                p.ImageFileId,
                p.AudioDurationSeconds,
                p.NewsUrl,
                RequiresPro: !freeExerciseIdSet.Contains(p.Id)
            ))
            .ToList();
        
        // When no filters are applied, reorder to alternate subjects and complexities
        if (!hasSubjectFilter && !hasLevelFilter && !hasSearchFilter)
        {
            items = SpreadExercisesByTopicAndLevel(items);
        }
        
        return new PagedResultDto<ExerciseListItemDto>(
            items,
            totalCount,
            filter.PageNumber,
            filter.PageSize
        );
    }

    private async Task<bool> IsInFreeCatalogWindowAsync(
        int propositionId,
        CancellationToken cancellationToken)
    {
        var freeExerciseIds = await GetFreeExerciseIdsAsync(cancellationToken);
        return freeExerciseIds.Contains(propositionId);
    }

    private Task<List<int>> GetFreeExerciseIdsAsync(CancellationToken cancellationToken)
    {
        return _context.Propositions
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(FreeCatalogExerciseLimit)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    private Task<PropositionAccessRow?> GetAccessRowAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _context.Propositions
            .Where(p => p.Id == id)
            .Select(p => new PropositionAccessRow(
                p.Id,
                p.PublishedOn,
                p.SubjectId,
                p.ComplexityId,
                p.AudioDurationSeconds,
                p.Title,
                p.ImageFileId,
                p.NewsInfo.Url,
                p.AudioFileId,
                p.Text))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static PropositionMetadataDto ToMetadata(
        PropositionAccessRow proposition,
        bool requiresPro)
    {
        return new PropositionMetadataDto(
            proposition.Id,
            proposition.PublishedOn,
            proposition.SubjectId,
            proposition.ComplexityId,
            proposition.AudioDurationSeconds,
            proposition.Title,
            proposition.ImageFileId,
            proposition.NewsUrl,
            requiresPro,
            CountWords(proposition.Text));
    }

    private static int CountWords(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private async Task<AudioAccess> CreateAudioAccessAsync(
        PropositionAccessRow proposition,
        CancellationToken cancellationToken)
    {
        var audioExpiresAtUtc = DateTimeOffset.UtcNow.Add(AudioPresignedUrlLifetime);
        var audioUrlResult = await _fileService.CreatePresignedGetUrlAsync(
            Proposition.AudioBucketName,
            proposition.AudioFileId,
            AudioPresignedUrlLifetime,
            cancellationToken);

        if (audioUrlResult.IsFailed)
        {
            _logger.LogError(
                "Failed to create presigned audio URL for proposition {PropositionId}: {Errors}",
                proposition.Id,
                string.Join(", ", audioUrlResult.Errors.Select(e => e.Message)));
            throw new InvalidOperationException("Unable to create exercise audio URL.");
        }

        return new AudioAccess(audioUrlResult.Value, audioExpiresAtUtc);
    }

    private sealed record AudioAccess(string AudioUrl, DateTimeOffset AudioExpiresAtUtc);

    private static List<ExerciseListItemDto> SpreadExercisesByTopicAndLevel(List<ExerciseListItemDto> items)
    {
        var remaining = new List<ExerciseListItemDto>(items);
        var reordered = new List<ExerciseListItemDto>(items.Count);
        var lastTopicIndex = new Dictionary<SubjectEnum, int>();
        var lastLevelIndex = new Dictionary<ComplexityEnum, int>();

        while (remaining.Any())
        {
            var currentIndex = reordered.Count;
            var next = remaining
                .OrderByDescending(item => DistanceSinceLastUse(item.Topic, lastTopicIndex, currentIndex))
                .ThenByDescending(item => DistanceSinceLastUse(item.Level, lastLevelIndex, currentIndex))
                .ThenByDescending(item => remaining.Count(candidate => candidate.Topic == item.Topic))
                .ThenByDescending(item => remaining.Count(candidate => candidate.Level == item.Level))
                .ThenBy(item => remaining.IndexOf(item))
                .First();

            reordered.Add(next);
            remaining.Remove(next);
            lastTopicIndex[next.Topic] = currentIndex;
            lastLevelIndex[next.Level] = currentIndex;
        }

        return reordered;
    }

    private static int DistanceSinceLastUse<T>(
        T key,
        IReadOnlyDictionary<T, int> lastUseIndex,
        int currentIndex)
        where T : notnull
    {
        return lastUseIndex.TryGetValue(key, out var index)
            ? currentIndex - index
            : int.MaxValue;
    }
    private static SearchCriteria? BuildSearchCriteria(string? searchText)
    {
        var normalized = searchText?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var terms = SearchTermRegex.Matches(normalized)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSearchTerms)
            .ToArray();

        if (terms.Length == 0)
        {
            return null;
        }

        var prefixTsQuery = string.Join(" | ", terms.Select(term => $"{term}:*"));
        return new SearchCriteria(prefixTsQuery, string.Join(' ', terms));
    }

    private sealed record SearchCriteria(string PrefixTsQuery, string TrigramText);
}
