using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using WriteFluency.Propositions;

namespace WriteFluency.Infrastructure.ExternalApis;

public class NewsClientTests : InfrastructureTestBase
{
    private HttpClient _httpClient = null!;
    private CapturingLogger<NewsClient> _logger = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        var optionsMock = Substitute.For<IOptionsMonitor<NewsOptions>>();
        var newsOptions = new NewsOptions
        {
            Key = "test-api-key",
            BaseAddress = "https://api.example.com",
            Routes = new NewsOptions.NewsRoutes { TopStories = "/top-stories" }
        };
        optionsMock.CurrentValue.Returns(newsOptions);
        services.AddSingleton(optionsMock);

        _logger = new CapturingLogger<NewsClient>();
        services.AddSingleton<ILogger<NewsClient>>(_logger);

        services.AddSingleton(_ => _httpClient);
        services.AddSingleton<INewsClient, NewsClient>();
    }

    [Fact]
    public async Task GetNewsAsync_ShouldFail_WhenNetworkErrorOccurs()
    {
        // Arrange
        _httpClient = CreateMockHttpClient((request, ct) =>
        {
            throw new HttpRequestException("Network error");
        });

        var client = GetService<INewsClient>();

        // Act
        var result = await client.GetNewsAsync(SubjectEnum.Science, DateTime.UtcNow);

        // Assert
        result.IsFailed.ShouldBe(true);
        result.Errors.ShouldContain(e => e.Message.Contains("Network error when calling /top-stories API"));
    }

    [Fact]
    public async Task GetNewsAsync_ShouldLogTimeoutCancellationAsErrorWithoutException()
    {
        var attempts = 0;
        _httpClient = CreateMockHttpClient((request, ct) =>
        {
            attempts++;
            throw new TaskCanceledException("request timed out");
        });

        var client = GetService<INewsClient>();

        var result = await client.GetNewsAsync(SubjectEnum.Science, DateTime.UtcNow);

        result.IsFailed.ShouldBe(true);
        attempts.ShouldBe(1);
        result.Errors.ShouldContain(e => e.Message.Contains("Network error when calling /top-stories API"));
        _logger.Entries.ShouldContain(entry =>
            entry.LogLevel == LogLevel.Error &&
            entry.Exception == null &&
            entry.Message.Contains("Network error when calling /top-stories API"));
        _logger.Entries.ShouldNotContain(entry =>
            entry.LogLevel == LogLevel.Error &&
            entry.Exception != null);
    }

    [Fact]
    public async Task GetNewsAsync_ShouldFail_WhenResponseIsNotSuccessful()
    {
        _httpClient = CreateMockHttpClient((request, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                ReasonPhrase = "Internal Server Error"
            });
        });

        var client = GetService<INewsClient>();

        var result = await client.GetNewsAsync(SubjectEnum.Science, DateTime.UtcNow);

        result.IsFailed.ShouldBe(true);
        result.Errors.ShouldContain(e => e.Message.Contains("Failed to fetch data from /top-stories"));
    }

    [Fact]
    public async Task GetNewsAsync_ShouldFail_WhenDeserializationFails()
    {
        _httpClient = CreateMockHttpClient((request, ct) =>
        {
            var invalidJson = "{ not-a-valid-json ";
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(invalidJson)
            });
        });

        var client = GetService<INewsClient>();

        var result = await client.GetNewsAsync(SubjectEnum.Politics, DateTime.UtcNow);

        result.IsFailed.ShouldBe(true);
        result.Errors.First().Message.ShouldContain("Failed to deserialize response");
    }

    [Fact]
    public async Task GetNewsAsync_ShouldFail_WhenDeserializedResponseIsNull()
    {
        _httpClient = CreateMockHttpClient((request, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("null")
            });
        });

        var client = GetService<INewsClient>();

        var result = await client.GetNewsAsync(SubjectEnum.Business, DateTime.UtcNow);

        result.IsFailed.ShouldBe(true);
        result.Errors.ShouldContain(e => e.Message.Contains("Deserialized /top-stories response is null"));
    }

    [Fact]
    public async Task GetNewsAsync_ShouldFail_WhenValidationFails()
    {
        var jsonWithMissingFields = """
        {
            "data": [
                {
                    "uuid": null,
                    "title": null,
                    "description": null,
                    "url": null,
                    "image_url": null,
                    "published_at": null
                }
            ]
        }
        """;

        _httpClient = CreateMockHttpClient((request, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonWithMissingFields)
            });
        });

        var client = GetService<INewsClient>();

        var result = await client.GetNewsAsync(SubjectEnum.Politics, DateTime.UtcNow);

        result.IsFailed.ShouldBe(true);
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetNewsAsync_ShouldSucceed_WhenResponseIsValid()
    {
        var validJson = """
        {
            "data": [
                {
                    "uuid": "123",
                    "title": "Sample News",
                    "description": "Description here",
                    "url": "https://example.com/news",
                    "image_url": "https://example.com/image.jpg",
                    "published_at": "2026-05-05T12:30:00Z"
                }
            ]
        }
        """;

        _httpClient = CreateMockHttpClient((request, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validJson)
            });
        });

        var client = GetService<INewsClient>();

        var result = await client.GetNewsAsync(SubjectEnum.Science, DateTime.UtcNow);

        result.IsSuccess.ShouldBe(true);
        result.Value.Count().ShouldBe(1);
        result.Value.First().Title.ShouldBe("Sample News");
        result.Value.First().PublishedOn.ShouldBe(DateTime.Parse("2026-05-05T12:30:00Z").ToUniversalTime());
    }

    [Fact]
    public async Task GetNewsAsync_ShouldUsePublishedBeforeAndNewestSort()
    {
        var validJson = """
        {
            "data": [
                {
                    "uuid": "123",
                    "title": "Sample News",
                    "description": "Description here",
                    "url": "https://example.com/news",
                    "image_url": "https://example.com/image.jpg",
                    "published_at": "2026-05-05T12:30:00Z"
                }
            ]
        }
        """;

        string? capturedRequestUri = null;
        _httpClient = CreateMockHttpClient((request, ct) =>
        {
            capturedRequestUri = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validJson)
            });
        });

        var client = GetService<INewsClient>();

        var result = await client.GetNewsAsync(SubjectEnum.Science, new DateTime(2026, 5, 5, 13, 45, 30, DateTimeKind.Utc));

        result.IsSuccess.ShouldBeTrue();
        capturedRequestUri.ShouldNotBeNullOrWhiteSpace();
        capturedRequestUri.ShouldContain("published_before=2026-05-05T13%3A45%3A30");
        capturedRequestUri.ShouldNotContain("published_on=");
        capturedRequestUri.ShouldContain("sort=published_on");
    }

    [Fact]
    public async Task GetNewsAsync_ShouldIncludeBlockedDomainsInExcludeDomainsQueryParameter()
    {
        var validJson = """
        {
            "data": [
                {
                    "uuid": "123",
                    "title": "Sample News",
                    "description": "Description here",
                    "url": "https://example.com/news",
                    "image_url": "https://example.com/image.jpg",
                    "published_at": "2026-05-05T12:30:00Z"
                }
            ]
        }
        """;

        string? capturedRequestUri = null;
        _httpClient = CreateMockHttpClient((request, ct) =>
        {
            capturedRequestUri = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validJson)
            });
        });

        var client = GetService<INewsClient>();

        var result = await client.GetNewsAsync(SubjectEnum.Science, DateTime.UtcNow);

        result.IsSuccess.ShouldBeTrue();
        capturedRequestUri.ShouldNotBeNullOrWhiteSpace();
        capturedRequestUri.ShouldContain("exclude_domains=");
        capturedRequestUri.ShouldContain("www.sportsnet.ca");
        capturedRequestUri.ShouldContain("spitalfieldslife.com");
        capturedRequestUri.ShouldContain("www.rte.ie");
        capturedRequestUri.ShouldContain("mumbrella.com.au");
        capturedRequestUri.ShouldContain("deadline.com");
        capturedRequestUri.ShouldContain("thedailyblog.co.nz");
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);
}
