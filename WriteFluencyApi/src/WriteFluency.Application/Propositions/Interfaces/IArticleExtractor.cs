using FluentResults;

namespace WriteFluency.Application.Propositions.Interfaces;

public interface IArticleExtractor
{
    Task<string> GetVisibleTextAsync(string url);
    Task<Result<byte[]>> DownloadImageAsync(string imageUrl);
}
