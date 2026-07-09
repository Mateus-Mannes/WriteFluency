using FluentResults;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WriteFluency.Application.Propositions.Interfaces;
using WriteFluency.Common;

namespace WriteFluency.Propositions;

public sealed class PropositionImageService(
    IArticleExtractor articleExtractor,
    IFileService fileService,
    ILogger<PropositionImageService> logger) : IPropositionImageService
{
    private sealed record ImageVariant(string Suffix, int Width, int Height);

    private static readonly ImageVariant[] OptimizedImageVariants =
    [
        new("w320", 320, 180),
        new("w512", 512, 288),
        new("w640", 640, 360),
        new("w1024", 1024, 576),
    ];

    private const int MaxValidationImageBytes = 184320; // 180 KB
    private const int JpegCompressionQuality = 60;
    private const int WebpCompressionQuality = 50;
    private const int MaxOriginalJpegBytes = 153600; // 150 KB
    private const int ImageAnalysisStep = 8;
    private const double MinimumLuminanceStdDev = 8.0;
    private const int MinimumQuantizedColorBuckets = 8;
    private const double MaximumDominantColorRatio = 0.92;

    private static readonly ImageVariant BaseVariant = OptimizedImageVariants
        .OrderByDescending(variant => variant.Width)
        .First();

    private static readonly IReadOnlyDictionary<string, int> MaxVariantBytesBySuffix = new Dictionary<string, int>
    {
        ["w320"] = 92160,
        ["w640"] = 92160,
        ["w512"] = 112640,
        ["w1024"] = 153600,
    };

    public async Task<Result<string>> ProcessAndUploadImageAsync(
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        var imageResult = await articleExtractor.DownloadImageAsync(imageUrl, cancellationToken);
        if (imageResult.IsFailed)
        {
            return Result.Fail(new Error("Failed to download image").CausedBy(imageResult.Errors));
        }

        var processedImageResult = await ProcessImageAsync(imageUrl, imageResult.Value, cancellationToken);
        if (processedImageResult.IsFailed)
        {
            return Result.Fail(new Error($"Failed to process image. ImageUrl={imageUrl}").CausedBy(processedImageResult.Errors));
        }

        using var processedImage = processedImageResult.Value;
        var imageContentValidation = ValidateImageContent(imageUrl, processedImage);
        if (imageContentValidation.IsFailed)
        {
            return Result.Fail(new Error($"Image failed deterministic content validation. ImageUrl={imageUrl}").CausedBy(imageContentValidation.Errors));
        }

        var processedJpegResult = await EncodeJpegAsync(imageUrl, processedImage, cancellationToken);
        if (processedJpegResult.IsFailed)
        {
            return Result.Fail(new Error($"Failed to compress image. ImageUrl={imageUrl}").CausedBy(processedJpegResult.Errors));
        }

        var processedImageBytes = processedJpegResult.Value;
        if (processedImageBytes.Length > MaxOriginalJpegBytes)
        {
            return Result.Fail(new Error($"Image exceeds max original size limit ({processedImageBytes.Length / 1024} KB > {MaxOriginalJpegBytes / 1024} KB). ImageUrl={imageUrl}"));
        }

        var imageBaseId = Guid.NewGuid().ToString("N");
        var optimizedVariantsResult = await GenerateOptimizedVariantsAsync(imageUrl, processedImage, cancellationToken);
        if (optimizedVariantsResult.IsFailed)
        {
            return Result.Fail(new Error($"Failed to generate optimized image variants. ImageUrl={imageUrl}").CausedBy(optimizedVariantsResult.Errors));
        }

        if (optimizedVariantsResult.Value.Count != OptimizedImageVariants.Length)
        {
            return Result.Fail(new Error($"Expected {OptimizedImageVariants.Length} optimized variants but generated {optimizedVariantsResult.Value.Count}. ImageUrl={imageUrl}"));
        }

        var oversizedVariant = optimizedVariantsResult.Value
            .FirstOrDefault(item =>
                MaxVariantBytesBySuffix.TryGetValue(item.Variant.Suffix, out var maxBytes)
                && item.Bytes.Length > maxBytes);

        if (oversizedVariant.Variant is not null)
        {
            var maxBytes = MaxVariantBytesBySuffix[oversizedVariant.Variant.Suffix];
            return Result.Fail(new Error($"Variant {oversizedVariant.Variant.Suffix} is too large ({oversizedVariant.Bytes.Length / 1024} KB > {maxBytes / 1024} KB). ImageUrl={imageUrl}"));
        }

        var originalObjectName = $"{imageBaseId}.jpg";
        var uploadOriginalResult = await fileService.UploadFileWithObjectNameAsync(
            Proposition.ImageBucketName,
            processedImageBytes,
            originalObjectName,
            "image/jpeg",
            cancellationToken);

        if (uploadOriginalResult.IsFailed)
        {
            return Result.Fail(new Error($"Failed to upload image. ImageUrl={imageUrl}").CausedBy(uploadOriginalResult.Errors));
        }

        foreach (var (variant, variantBytes) in optimizedVariantsResult.Value)
        {
            var variantObjectName = $"{imageBaseId}_{variant.Suffix}.webp";
            var uploadVariantResult = await fileService.UploadFileWithObjectNameAsync(
                Proposition.ImageBucketName,
                variantBytes,
                variantObjectName,
                "image/webp",
                cancellationToken);

            if (uploadVariantResult.IsFailed)
            {
                return Result.Fail(new Error($"Failed to upload optimized image variant {variantObjectName}. ImageUrl={imageUrl}").CausedBy(uploadVariantResult.Errors));
            }
        }

        return Result.Ok(uploadOriginalResult.Value);
    }

    private async Task<Result<Image>> ProcessImageAsync(string imageUrl, byte[] imageBytes, CancellationToken cancellationToken = default)
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
        catch (UnknownImageFormatException)
        {
            image?.Dispose();
            logger.LogWarning("Skipping image processing because the source image format is unsupported. ImageUrl={ImageUrl}", imageUrl);
            return Result.Fail(new Error($"Unsupported image format. ImageUrl={imageUrl}"));
        }
        catch (Exception ex)
        {
            image?.Dispose();
            logger.LogWarning(
                "Failed to process image. ImageUrl={ImageUrl} ExceptionType={ExceptionType} ExceptionMessage={ExceptionMessage}",
                imageUrl,
                ex.GetType().Name,
                ex.Message);
            return Result.Fail(new Error($"Failed to process image. ImageUrl={imageUrl}").CausedBy(ex));
        }
    }

    private Result ValidateImageContent(string imageUrl, Image image)
    {
        try
        {
            using var rgbaImage = image.CloneAs<Rgba32>();
            var sampleCount = 0;
            var luminanceSum = 0.0;
            var luminanceSquaredSum = 0.0;
            var colorBuckets = new Dictionary<int, int>();

            rgbaImage.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y += ImageAnalysisStep)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x += ImageAnalysisStep)
                    {
                        var pixel = row[x];
                        var luminance = GetLuminance(pixel);
                        luminanceSum += luminance;
                        luminanceSquaredSum += luminance * luminance;
                        sampleCount++;

                        var bucket = QuantizeColor(pixel);
                        colorBuckets[bucket] = colorBuckets.GetValueOrDefault(bucket) + 1;
                    }
                }
            });

            if (sampleCount == 0)
            {
                return Result.Fail(new Error($"Image has no analyzable pixels. ImageUrl={imageUrl}"));
            }

            var mean = luminanceSum / sampleCount;
            var variance = Math.Max(0, (luminanceSquaredSum / sampleCount) - (mean * mean));
            var stdDev = Math.Sqrt(variance);
            var dominantColorRatio = colorBuckets.Values.Max() / (double)sampleCount;

            if (stdDev < MinimumLuminanceStdDev)
            {
                return Result.Fail(new Error($"Image appears blank or nearly uniform. LuminanceStdDev={stdDev:F2}. ImageUrl={imageUrl}"));
            }

            if (colorBuckets.Count < MinimumQuantizedColorBuckets && dominantColorRatio > MaximumDominantColorRatio)
            {
                return Result.Fail(new Error($"Image appears to be a logo, placeholder, or low-content graphic. ColorBuckets={colorBuckets.Count}, DominantColorRatio={dominantColorRatio:F2}. ImageUrl={imageUrl}"));
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Failed to run deterministic image content validation. ImageUrl={ImageUrl} ExceptionType={ExceptionType} ExceptionMessage={ExceptionMessage}",
                imageUrl,
                ex.GetType().Name,
                ex.Message);
            return Result.Fail(new Error($"Failed to validate image content. ImageUrl={imageUrl}").CausedBy(ex));
        }
    }

    private static double GetLuminance(Rgba32 pixel)
        => (0.2126 * pixel.R) + (0.7152 * pixel.G) + (0.0722 * pixel.B);

    private static int QuantizeColor(Rgba32 pixel)
    {
        var r = pixel.R >> 5;
        var g = pixel.G >> 5;
        var b = pixel.B >> 5;
        return (r << 6) | (g << 3) | b;
    }

    private async Task<Result<byte[]>> EncodeJpegAsync(string imageUrl, Image image, CancellationToken cancellationToken = default)
    {
        try
        {
            using var outputStream = new MemoryStream();
            await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = JpegCompressionQuality }, cancellationToken);
            var compressedBytes = outputStream.ToArray();

            if (compressedBytes.Length > MaxValidationImageBytes)
            {
                return Result.Fail(new Error($"Image exceeds maximum size of {MaxValidationImageBytes / 1024} KB after compression ({compressedBytes.Length / 1024} KB). ImageUrl={imageUrl}"));
            }

            return Result.Ok(compressedBytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Failed to encode image. ImageUrl={ImageUrl} ExceptionType={ExceptionType} ExceptionMessage={ExceptionMessage}",
                imageUrl,
                ex.GetType().Name,
                ex.Message);
            return Result.Fail(new Error($"Failed to compress image. ImageUrl={imageUrl}").CausedBy(ex));
        }
    }

    private async Task<Result<IReadOnlyList<(ImageVariant Variant, byte[] Bytes)>>> GenerateOptimizedVariantsAsync(
        string imageUrl,
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
            logger.LogWarning(
                "Failed to generate optimized image variants. ImageUrl={ImageUrl} ExceptionType={ExceptionType} ExceptionMessage={ExceptionMessage}",
                imageUrl,
                ex.GetType().Name,
                ex.Message);
            return Result.Fail(new Error($"Failed to generate optimized image variants. ImageUrl={imageUrl}").CausedBy(ex));
        }
    }
}
