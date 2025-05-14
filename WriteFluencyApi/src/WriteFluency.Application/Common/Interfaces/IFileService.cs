using FluentResults;

namespace WriteFluency.Common;

public interface IFileService
{
    Task<Result<Guid>> UploadFileAsync(string bucketName, Stream fileStream)
}
