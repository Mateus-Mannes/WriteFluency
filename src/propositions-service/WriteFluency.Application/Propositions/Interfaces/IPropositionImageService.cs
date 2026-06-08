using FluentResults;

namespace WriteFluency.Propositions;

public interface IPropositionImageService
{
    Task<Result<string>> ProcessAndUploadImageAsync(
        string imageUrl,
        CancellationToken cancellationToken = default);
}
