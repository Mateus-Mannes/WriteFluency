using FluentResults;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using WriteFluency.Common;

namespace WriteFluency.Infrastructure.FileStorage;

public class FileService : IFileService
{
    private const string ImmutableAssetCacheControl = "public, max-age=31536000, immutable";

    private readonly IMinioClient _minioClient;
    private readonly ILogger<FileService> _logger;

    public FileService(IMinioClient minioClient, ILogger<FileService> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
    }

    public async Task<Result<string>> UploadFileAsync(
        string bucketName,
        byte[] file,
        string url,
        CancellationToken cancellationToken = default)
    {
        var (extension, contentType) = TryGetExtensionFromUrl(url);
        return await UploadFileAsync(
            bucketName,
            file,
            extension,
            contentType,
            cancellationToken);
    }

    public async Task<Result<string>> UploadFileAsync(
        string bucketName,
        byte[] file,
        string? fileExtension = null,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var objectName = string.IsNullOrWhiteSpace(fileExtension)
            ? Guid.NewGuid().ToString()
            : $"{Guid.NewGuid()}.{fileExtension}";

        return await UploadFileInternalAsync(bucketName, file, objectName, contentType);
    }

    public async Task<Result<string>> UploadFileWithObjectNameAsync(
        string bucketName,
        byte[] file,
        string objectName,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        return await UploadFileInternalAsync(bucketName, file, objectName, contentType);
    }

    private async Task<Result<string>> UploadFileInternalAsync(
        string bucketName,
        byte[] file,
        string objectName,
        string? contentType)
    {
        try
        {
            await EnsureBucketExistsAsync(bucketName);

            var headers = new Dictionary<string, string>
            {
                ["Cache-Control"] = ImmutableAssetCacheControl
            };

            using var stream = new MemoryStream(file);
            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(contentType ?? "application/octet-stream")
                .WithHeaders(headers));

            return Result.Ok(objectName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to MinIO: {Message}", ex.Message);
            return Result.Fail<string>($"Error uploading file to MinIO: {ex.Message}");
        }
    }

    private async Task EnsureBucketExistsAsync(string bucketName)
    {
        var bucketExists = await _minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucketName));

        if (!bucketExists)
        {
            await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
        }
    }

    private (string? extension, string? contentType) TryGetExtensionFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var extension = Path.GetExtension(path);

            if (!string.IsNullOrWhiteSpace(extension) && extension.Length <= 5)
            {
                extension = extension.TrimStart('.').ToLowerInvariant();
                string? contentType = extension switch
                {
                    "jpg" or "jpeg" => "image/jpeg",
                    "png" => "image/png",
                    "gif" => "image/gif",
                    "webp" => "image/webp",
                    "bmp" => "image/bmp",
                    "svg" => "image/svg+xml",
                    "tiff" or "tif" => "image/tiff",
                    _ => "application/octet-stream"
                };
                return (extension, contentType);
            }

            return (default, default);
        }
        catch
        {
            return (default, default);
        }
    }

    public async Task<byte[]> GetFileAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream => stream.CopyTo(memoryStream)), cancellationToken);
        return memoryStream.ToArray();
    }
}
