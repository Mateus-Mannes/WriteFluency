using WriteFluency.Common;
using WriteFluency.Application.Propositions.Interfaces;
using WriteFluency.TextComparisons;
using FluentResults.Extensions;
using FluentResults;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using WriteFluency.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WriteFluency.Propositions;

public class CreatePropositionService
{
    private sealed record ImageVariant(string Suffix, int Width, int Height);

    private static readonly ImageVariant[] OptimizedImageVariants =
    [
        new("w320", 320, 180),
        new("w512", 512, 288),
        new("w640", 640, 360),
        new("w1024", 1024, 576),
    ];

    private const int MaxValidationImageBytes = 153600;
    private const int JpegCompressionQuality = 60;
    private const int WebpCompressionQuality = 50;
    private const int MaxOriginalJpegBytes = 122880; // 120 KB

    private static readonly ImageVariant BaseVariant = OptimizedImageVariants
        .OrderByDescending(variant => variant.Width)
        .First();

    private static readonly IReadOnlyDictionary<string, int> MaxVariantBytesBySuffix = new Dictionary<string, int>
    {
        ["w320"] = 61440,   // 60 KB (grid)
        ["w640"] = 61440,   // 60 KB (grid)
        ["w512"] = 81920,   // 80 KB (news)
        ["w1024"] = 122880, // 120 KB (news)
    };

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

        var newsIds = newsResult.Value.Select(n => n.ExternalId).ToList();
        
        // Check for existing propositions in database (includes soft delete filter automatically)
        var existingNewsIds = await _context.Propositions.AsNoTracking()
            .Where(p => newsIds.Contains(p.NewsInfo.Id))
            .Select(p => p.NewsInfo.Id)
            .ToListAsync(cancellationToken);

        // Filter out news that already have propositions
        var news = newsResult.Value
            .Where(n => !existingNewsIds.Contains(n.ExternalId))
            .ToList();

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
            var originalImageBytes = imageResult.Value;
            var processedImageResult = await ProcessImageAsync(originalImageBytes, cancellationToken);
            if (processedImageResult.IsFailed)
            {
                return Result.Fail(new Error("Failed to process image").CausedBy(processedImageResult.Errors));
            }

            using var processedImage = processedImageResult.Value;
            var processedJpegResult = await EncodeJpegAsync(processedImage, cancellationToken);
            if (processedJpegResult.IsFailed)
            {
                return Result.Fail(new Error("Failed to compress image").CausedBy(processedJpegResult.Errors));
            }

            var processedImageBytes = processedJpegResult.Value;
            if (processedImageBytes.Length > MaxOriginalJpegBytes)
            {
                return Result.Fail(new Error($"Image exceeds max original size limit ({processedImageBytes.Length / 1024} KB > {MaxOriginalJpegBytes / 1024} KB)"));
            }

            var imageValidation = await _generativeAIClient.ValidateImageAsync(processedImageBytes, newsArticle.Title, cancellationToken);
            if (imageValidation.IsFailed)
            {
                return Result.Fail(new Error("Failed to validate image").CausedBy(imageValidation.Errors));
            }
            if (!imageValidation.Value)
            {
                return Result.Fail(new Error($"Image is invalid or not coherent with article: {newsArticle.Title}"));
            }

            var imageBaseId = Guid.NewGuid().ToString("N");
            var optimizedVariantsResult = await GenerateOptimizedVariantsAsync(processedImage, cancellationToken);
            if (optimizedVariantsResult.IsFailed)
            {
                return Result.Fail(new Error("Failed to generate optimized image variants").CausedBy(optimizedVariantsResult.Errors));
            }

            if (optimizedVariantsResult.Value.Count != OptimizedImageVariants.Length)
            {
                return Result.Fail(new Error($"Expected {OptimizedImageVariants.Length} optimized variants but generated {optimizedVariantsResult.Value.Count}"));
            }

            var oversizedVariant = optimizedVariantsResult.Value
                .FirstOrDefault(item =>
                    MaxVariantBytesBySuffix.TryGetValue(item.Variant.Suffix, out var maxBytes)
                    && item.Bytes.Length > maxBytes);

            if (oversizedVariant.Variant is not null)
            {
                var maxBytes = MaxVariantBytesBySuffix[oversizedVariant.Variant.Suffix];
                return Result.Fail(new Error($"Variant {oversizedVariant.Variant.Suffix} is too large ({oversizedVariant.Bytes.Length / 1024} KB > {maxBytes / 1024} KB)"));
            }

            var originalObjectName = $"{imageBaseId}.jpg";
            var uploadOriginalResult = await _fileService.UploadFileWithObjectNameAsync(
                Proposition.ImageBucketName,
                processedImageBytes,
                originalObjectName,
                "image/jpeg",
                cancellationToken);

            if (uploadOriginalResult.IsFailed)
            {
                return Result.Fail(new Error("Failed to upload image").CausedBy(uploadOriginalResult.Errors));
            }

            builder.SetImageFileId(uploadOriginalResult.Value);

            foreach (var (variant, variantBytes) in optimizedVariantsResult.Value)
            {
                var variantObjectName = $"{imageBaseId}_{variant.Suffix}.webp";
                var uploadVariantResult = await _fileService.UploadFileWithObjectNameAsync(
                    Proposition.ImageBucketName,
                    variantBytes,
                    variantObjectName,
                    "image/webp",
                    cancellationToken);

                if (uploadVariantResult.IsFailed)
                {
                    return Result.Fail(new Error($"Failed to upload optimized image variant {variantObjectName}").CausedBy(uploadVariantResult.Errors));
                }
            }
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

    private async Task<Result<Image>> ProcessImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        Image? image = null;

        try
        {
            using var inputStream = new MemoryStream(imageBytes);
            image = await Image.LoadAsync(inputStream, cancellationToken);

            image.Mutate(x => x.AutoOrient());
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Top,
                Size = new Size(BaseVariant.Width, BaseVariant.Height)
            }));

            return Result.Ok(image);
        }
        catch (Exception ex)
        {
            image?.Dispose();
            _logger.LogWarning(ex, "Failed to process image");
            return Result.Fail(new Error("Failed to process image").CausedBy(ex));
        }
    }

    private async Task<Result<byte[]>> EncodeJpegAsync(Image image, CancellationToken cancellationToken = default)
    {
        try
        {
            using var outputStream = new MemoryStream();
            await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = JpegCompressionQuality }, cancellationToken);
            var compressedBytes = outputStream.ToArray();

            if (compressedBytes.Length > MaxValidationImageBytes)
            {
                return Result.Fail(new Error($"Image exceeds maximum size of {MaxValidationImageBytes / 1024} KB after compression ({compressedBytes.Length / 1024} KB)"));
            }

            return Result.Ok(compressedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to encode image");
            return Result.Fail(new Error("Failed to compress image").CausedBy(ex));
        }
    }

    private async Task<Result<IReadOnlyList<(ImageVariant Variant, byte[] Bytes)>>> GenerateOptimizedVariantsAsync(
        Image baseImage,
        CancellationToken cancellationToken = default)
    {
        var variants = new List<(ImageVariant, byte[])>();

        try
        {
            foreach (var variant in OptimizedImageVariants)
            {
                using var resized = baseImage.Clone(ctx =>
                    ctx.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(variant.Width, variant.Height)
                    }));

                using var outputStream = new MemoryStream();
                await resized.SaveAsWebpAsync(outputStream, new WebpEncoder { Quality = WebpCompressionQuality }, cancellationToken);
                variants.Add((variant, outputStream.ToArray()));
            }

            return Result.Ok<IReadOnlyList<(ImageVariant, byte[])>>(variants);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate optimized image variants");
            return Result.Fail(new Error("Failed to generate optimized image variants").CausedBy(ex));
        }
    }

}
