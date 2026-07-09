using WriteFluency.Common;
using WriteFluency.Application.Propositions.Interfaces;
using WriteFluency.TextComparisons;
using FluentResults.Extensions;
using FluentResults;
using WriteFluency.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WriteFluency.Propositions;

public class CreatePropositionService
{
    private readonly INewsClient _newsClient;
    private readonly IArticleExtractor _articleExtractor;
    private readonly IArticleContentPolicyValidator _articleContentPolicyValidator;
    private readonly IGenerativeAIClient _generativeAIClient;
    private readonly ITextToSpeechClient _textToSpeechClient;
    private readonly IFileService _fileService;
    private readonly IPropositionImageService _propositionImageService;
    private readonly IAppDbContext _context;
    private readonly ILogger<CreatePropositionService> _logger; 

    public CreatePropositionService(
        INewsClient newsClient,
        IArticleExtractor articleExtractor,
        IArticleContentPolicyValidator articleContentPolicyValidator,
        IGenerativeAIClient generativeAIClient,
        ITextToSpeechClient textToSpeechClient,
        IFileService fileService,
        IPropositionImageService propositionImageService,
        IAppDbContext context,
        ILogger<CreatePropositionService> logger)
    {
        _articleExtractor = articleExtractor;
        _newsClient = newsClient;
        _articleContentPolicyValidator = articleContentPolicyValidator;
        _generativeAIClient = generativeAIClient;
        _textToSpeechClient = textToSpeechClient;
        _fileService = fileService;
        _propositionImageService = propositionImageService;
        _context = context;
        _logger = logger;
    }

    public async Task<CreatePropositionsResult> CreatePropositionsAsync(
        CreatePropositionDto dto, 
        int quantity, 
        CancellationToken cancellationToken = default)
    {
        // switch news search page number based on complexity to avoid duplicated news
        var page = dto.Complexity switch
        {
            ComplexityEnum.Beginner => 1,
            ComplexityEnum.Intermediate => 2,
            ComplexityEnum.Advanced => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(dto.Complexity), "Invalid complexity level")
        };

        var newsResult = await _newsClient.GetNewsAsync(dto.Subject, dto.PublishedOn, quantity, page, cancellationToken);
        if (newsResult.IsFailed) return CreatePropositionsResult.Empty;

        var fetchedNews = newsResult.Value
            .OrderByDescending(n => n.PublishedOn)
            .ToList();
        var oldestFetchedPublishedOn = fetchedNews.Count > 0
            ? fetchedNews.Min(n => n.PublishedOn)
            : (DateTime?)null;

        var newsIds = fetchedNews.Select(n => n.ExternalId).ToList();
        
        // Check for existing propositions in database (includes soft delete filter automatically)
        var existingNewsIds = await _context.Propositions.AsNoTracking()
            .Where(p => newsIds.Contains(p.NewsInfo.Id))
            .Select(p => p.NewsInfo.Id)
            .ToListAsync(cancellationToken);

        // Filter out news that already have propositions
        var news = fetchedNews
            .Where(n => !existingNewsIds.Contains(n.ExternalId))
            .ToList();

        if(!news.Any())
        {
            _logger.LogWarning($"No new articles found for subject '{dto.Subject}' before {dto.PublishedOn:O} after filtering existing propositions.");
            return new CreatePropositionsResult(Array.Empty<Proposition>(), oldestFetchedPublishedOn, fetchedNews.Count);
        }

        var propositions = new List<Proposition>();
        foreach (var newsArticle in news)
        {
            var propositionResult = await BuildPropositionAsync(dto, newsArticle, cancellationToken);
            if (propositionResult.IsSuccess) propositions.Add(propositionResult.Value);
            else
            {
                _logger.LogError(
                    "Skipped article because proposition generation failed. Url={Url}. Errors={Errors}",
                    newsArticle.Url,
                    string.Join(", ", propositionResult.Errors.Select(e => e.Message)));
            }
        }
        return new CreatePropositionsResult(propositions, oldestFetchedPublishedOn, fetchedNews.Count);
    }

    private async Task<Result<Proposition>> BuildPropositionAsync(CreatePropositionDto dto, NewsDto newsArticle, CancellationToken cancellationToken = default)
    {
        var builder = new PropositionBuilder();

        var imageFileResult = await _propositionImageService.ProcessAndUploadImageAsync(newsArticle.ImageUrl, cancellationToken);
        if (imageFileResult.IsFailed)
        {
            return Result.Fail(new Error("Failed to process image").CausedBy(imageFileResult.Errors));
        }

        builder.SetImageFileId(imageFileResult.Value);

        return await _articleExtractor.GetVisibleTextAsync(newsArticle.Url, cancellationToken)
            .Map(articleText => articleText.Length > 3000 ? articleText[..3000] : articleText)
            .Bind(articleText => ValidateArticleContentPolicy(articleText, newsArticle.Url))
            .Bind(articleText => builder.SetArticleText(articleText))
            .Bind(articleText => _generativeAIClient.GenerateTextAsync(dto.Complexity, articleText, cancellationToken))
            .Bind(propositionText => builder.SetPropositionText(propositionText))
            .Bind(propositionText => _textToSpeechClient.GenerateAudioAsync(propositionText, cancellationToken))
            .Bind(audio => builder.SetAudioVoice(audio))
            .Bind(audio =>
            {
                return _fileService.UploadFileAsync(Proposition.AudioBucketName, audio.Audio, "mp3", "audio/mpeg", cancellationToken);
            })
            .Bind(fileId => builder.SetAudioFileId(fileId))
            .Bind(_ => builder.Build(dto, newsArticle));
    }

    private Result<string> ValidateArticleContentPolicy(string articleText, string articleUrl)
    {
        var validationResult = _articleContentPolicyValidator.Validate(articleText);
        if (validationResult.IsSuccess)
            return Result.Ok(articleText);

        var errorMessage = string.Join(", ", validationResult.Errors.Select(error => error.Message));
        _logger.LogWarning("Article rejected by deterministic content policy. Url: {Url}. Reason: {Reason}",
            articleUrl, errorMessage);

        return Result.Fail(new Error($"Article content rejected by policy: {errorMessage}"));
    }

}
