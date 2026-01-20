using WriteFluency.Common;
using WriteFluency.Application.Propositions.Interfaces;
using WriteFluency.TextComparisons;
using FluentResults.Extensions;
using FluentResults;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using WriteFluency.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WriteFluency.Propositions;

public class CreatePropositionService
{
    private readonly INewsClient _newsClient;
    private readonly IArticleExtractor _articleExtractor;
    private readonly IGenerativeAIClient _generativeAIClient;
    private readonly ITextToSpeechClient _textToSpeechClient;
    private readonly IFileService _fileService;
    private readonly IAppDbContext _context;
    private readonly ILogger<CreatePropositionService> _logger; 

    public CreatePropositionService(
        INewsClient newsClient,
        IArticleExtractor articleExtractor,
        IGenerativeAIClient generativeAIClient,
        ITextToSpeechClient textToSpeechClient,
        IFileService fileService,
        IAppDbContext context,
        ILogger<CreatePropositionService> logger)
    {
        _articleExtractor = articleExtractor;
        _newsClient = newsClient;
        _generativeAIClient = generativeAIClient;
        _textToSpeechClient = textToSpeechClient;
        _fileService = fileService;
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Proposition>> CreatePropositionsAsync(
        CreatePropositionDto dto, 
        int quantity, 
        IEnumerable<Proposition> generatedPropositions,
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
        if (newsResult.IsFailed) return Enumerable.Empty<Proposition>();

        var existingPropositions = (await _context.Propositions
            .Where(p => newsResult.Value.Select(n => n.ExternalId).Contains(p.NewsInfo.Id))
            .ToListAsync(cancellationToken)).Concat(generatedPropositions);

        var news = newsResult.Value.Where(n => !existingPropositions.Any(p => p.NewsInfo.Id == n.ExternalId));

        if(!news.Any())
        {
            _logger.LogWarning($"No new articles found for subject '{dto.Subject}' on {dto.PublishedOn:yyyy-MM-dd} after filtering existing propositions.");
            return Enumerable.Empty<Proposition>();
        }

        var propositions = new List<Proposition>();
        foreach (var newsArticle in news)
        {
            var propositionResult = await BuildPropositionAsync(dto, newsArticle, cancellationToken);
            if (propositionResult.IsSuccess) propositions.Add(propositionResult.Value);
            else
            {
                _logger.LogError($"Failed to build proposition for article '{newsArticle.Url}': {string.Join(", ", propositionResult.Errors.Select(e => e.Message))}");
            }
        }
        return propositions;
    }

    private async Task<Result<Proposition>> BuildPropositionAsync(CreatePropositionDto dto, NewsDto newsArticle, CancellationToken cancellationToken = default)
    {
        var builder = new PropositionBuilder();

        // Download and validate image before uploading
        var imageResult = await _articleExtractor.DownloadImageAsync(newsArticle.ImageUrl, cancellationToken);
        if (imageResult.IsSuccess)
        {
            var imageBytes = imageResult.Value;
            
            // Check and compress image if it exceeds 150 KB
            if (imageBytes.Length > 153600)
            {
                imageBytes = await CompressImageAsync(imageBytes, cancellationToken);
                
                // If still too large after compression, reject it
                if (imageBytes.Length > 153600)
                {
                    return Result.Fail(new Error($"Image exceeds maximum size of 150 KB even after compression ({imageBytes.Length / 1024} KB): {newsArticle.Title}"));
                }
            }

            var imageValidation = await _generativeAIClient.ValidateImageAsync(imageBytes, newsArticle.Title, cancellationToken);
            if (imageValidation.IsFailed)
            {
                return Result.Fail(new Error("Failed to validate image").CausedBy(imageValidation.Errors));
            }
            if (!imageValidation.Value)
            {
                return Result.Fail(new Error($"Image is invalid or not coherent with article: {newsArticle.Title}"));
            }

            // If valid, upload the image
            var uploadResult = await _fileService.UploadFileAsync(Proposition.ImageBucketName, imageBytes, newsArticle.ImageUrl, cancellationToken: cancellationToken);
            if (uploadResult.IsFailed)
            {
                return Result.Fail(new Error("Failed to upload image").CausedBy(uploadResult.Errors));
            }
            builder.SetImageFileId(uploadResult.Value);
        }
        else
        {
            return Result.Fail(new Error("Failed to download image").CausedBy(imageResult.Errors));
        }

        return await _articleExtractor.GetVisibleTextAsync(newsArticle.Url, cancellationToken)
            .Map(articleText => articleText.Length > 3000 ? articleText[..3000] : articleText)
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

    private async Task<byte[]> CompressImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        using var inputStream = new MemoryStream(imageBytes);
        using var image = await Image.LoadAsync(inputStream, cancellationToken);
        using var outputStream = new MemoryStream();
        
        // Start with quality 75 and reduce dimensions if needed
        var quality = 75;
        var scaleFactor = 1.0;
        
        // Try different compression levels until we get under 150 KB
        while (quality >= 50)
        {
            outputStream.SetLength(0);
            outputStream.Position = 0;
            
            // Resize if needed
            if (scaleFactor < 1.0)
            {
                var newWidth = (int)(image.Width * scaleFactor);
                var newHeight = (int)(image.Height * scaleFactor);
                image.Mutate(x => x.Resize(newWidth, newHeight));
            }
            
            await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = quality }, cancellationToken);
            
            if (outputStream.Length <= 153600)
            {
                return outputStream.ToArray();
            }
            
            // Try lower quality
            quality -= 10;
            
            // If quality is too low, try reducing dimensions
            if (quality < 50 && scaleFactor == 1.0)
            {
                scaleFactor = 0.8;
                quality = 75;
            }
            else if (quality < 50 && scaleFactor > 0.5)
            {
                scaleFactor -= 0.1;
                quality = 75;
            }
        }
        
        // Return best effort
        return outputStream.ToArray();
    }
}
