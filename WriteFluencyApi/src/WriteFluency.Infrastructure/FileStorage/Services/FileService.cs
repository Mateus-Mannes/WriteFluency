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

    public async Task<Result<Guid>> UploadFileAsync(string bucketName, byte[] file, CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketExists = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName), cancellationToken);
            if (!bucketExists)
            {
                await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), cancellationToken);
            }
            var objectName = Guid.NewGuid().ToString();
            using var stream = new MemoryStream(file);
            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType("application/octet-stream"), cancellationToken);
            return Result.Ok(Guid.NewGuid());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to MinIO: {Message}", ex.Message);
            return Result.Fail<Guid>($"Error uploading file to MinIO: {ex.Message}");
        }
    }
}
