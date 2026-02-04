using FluentResults;

namespace WriteFluency.Infrastructure.ExternalApis;

public interface ICloudflareCachePurgeClient
{
    Task<Result> PurgeConfiguredUrlsAsync(CancellationToken cancellationToken = default);
}
