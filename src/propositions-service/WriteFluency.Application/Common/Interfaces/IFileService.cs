using FluentResults;

namespace WriteFluency.Common;

public interface IFileService
{
    Task<Result<string>> UploadFileAsync(
        string bucketName,
        byte[] file,
        string? fileExtension = null,
        string? contentType = null,
        CancellationToken cancellationToken = default);

    Task<Result<string>> UploadFileAsync(
        string bucketName,
        byte[] file,
        string url,
        CancellationToken cancellationToken = default);

    Task<Result<string>> UploadFileWithObjectNameAsync(
        string bucketName,
        byte[] file,
        string objectName,
        string? contentType = null,
        CancellationToken cancellationToken = default);

    Task<byte[]> GetFileAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default);
}
