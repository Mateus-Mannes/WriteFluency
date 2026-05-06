using FluentResults;

namespace WriteFluency.Propositions;

public interface INewsClient
{
    Task<Result<IEnumerable<NewsDto>>> GetNewsAsync(SubjectEnum subject, DateTime publishedBefore, int quantity = 3, int page = 1, CancellationToken cancellationToken = default);
}
