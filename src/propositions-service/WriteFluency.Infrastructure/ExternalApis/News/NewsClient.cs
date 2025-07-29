using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WriteFluency.Extensions;
using WriteFluency.Infrastructure.Http.Services;
using WriteFluency.Propositions;

namespace WriteFluency.Infrastructure.ExternalApis;

public class NewsClient : BaseHttpClientService, INewsClient
{
    private readonly NewsOptions _options;

    public NewsClient(HttpClient httpClient, ILogger<NewsClient> logger, IOptionsMonitor<NewsOptions> options)
        : base(httpClient, logger)
    {
        _options = options.CurrentValue;
    }

    public async Task<Result<IEnumerable<NewsDto>>> GetNewsAsync(
        SubjectEnum subject, DateTime publishedOn, int quantity, int page, CancellationToken cancellationToken = default)
    {
        var subjectParameter = subject.ToString().ToLowerInvariant();
        var dateParameter = publishedOn.ToString("yyyy-MM-dd");

        var query = $"api_token={_options.Key}" +
                    $"&published_on={dateParameter}" +
                    $"&categories={subjectParameter}" +
                    $"&language=en" +
                    $"&locale=au,ca,gb,us,nz,ie" +
                    $"&sort=relevance_score" +
                    $"&page={page}" +
                    $"&limit={quantity}";

        var requestUri = $"{_options.Routes.TopStories}?{query}";

        var requestResult = await GetAsync(requestUri, new NewsResponseValidator(), 1, cancellationToken);

        if (requestResult.IsFailed)
        {
            var errorMessage = requestResult.Errors.Message();
            _logger.LogError("Error fetching data from news API: {ErrorMessage}", errorMessage);
            return Result.Fail(new Error($"Error when calling news API. {errorMessage}"));
        }

        var newsArticles = requestResult.Value.Data?.Select(article => new NewsDto(
            article.Uuid!,
            article.Title!,
            article.Description!,
            article.Url!,
            article.ImageUrl!,
            subject,
            publishedOn
        )) ?? Enumerable.Empty<NewsDto>();

        return Result.Ok(newsArticles);
    }
}
