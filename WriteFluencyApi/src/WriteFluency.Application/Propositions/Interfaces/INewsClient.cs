using FluentResults;

namespace WriteFluency.Propositions;

public interface INewsClient
{
    Task<Result<IEnumerable<NewsDto>>> GetNewsAsync(SubjectEnum subject, DateTime publishedOn);
}
