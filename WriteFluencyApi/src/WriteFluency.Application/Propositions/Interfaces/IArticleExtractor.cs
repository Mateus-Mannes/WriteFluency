using FluentResults;

namespace WriteFluency.Application.Propositions.Interfaces;

public interface IArticleExtractor
{
    Task<Result<string>> GetVisibleTextAsync(string url, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken = default);
}
