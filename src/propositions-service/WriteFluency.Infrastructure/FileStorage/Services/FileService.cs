using FluentResults;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using WriteFluency.Common;

namespace WriteFluency.Infrastructure.FileStorage;

public class FileService : IFileService
{
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
        try
        {
            var bucketExists = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));
            if (!bucketExists)
            {
                await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
            }
            var objectName = Guid.NewGuid().ToString() + "." + fileExtension;
            using var stream = new MemoryStream(file);
            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName.ToString())
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(contentType ?? "application/octet-stream"));
            return Result.Ok(objectName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to MinIO: {Message}", ex.Message);
            return Result.Fail<string>($"Error uploading file to MinIO: {ex.Message}");
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
