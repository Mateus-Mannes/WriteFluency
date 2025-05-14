using System;
using AngleSharp;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using FluentResults;
using Microsoft.Extensions.Logging;
using WriteFluency.Application.Propositions.Interfaces;

namespace WriteFluency.Infrastructure.Http;

public class ArticleExtractor : IArticleExtractor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ArticleExtractor> _logger;

    public ArticleExtractor(HttpClient httpClient, ILogger<ArticleExtractor> logger)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<string> GetVisibleTextAsync(string url)
    {
        var html = await _httpClient.GetStringAsync(url);

        var context = BrowsingContext.New(Configuration.Default);
        var parser = context.GetService<IHtmlParser>();
        var document = await parser!.ParseDocumentAsync(html);

        // Remove unwanted tags
        var unwantedTags = document.QuerySelectorAll("script, style, head, noscript, iframe, svg");
        foreach (var node in unwantedTags)
        {
            node.Remove();
        }

        // Extract visible text only
        var visibleText = document.Body!.Descendants<IText>()
            .Where(t => !string.IsNullOrWhiteSpace(t.Text))
            .Where(t => t.ParentElement != null &&
                        t.ParentElement.ComputeCurrentStyle().GetDisplay() != "none" &&
                        t.ParentElement.ComputeCurrentStyle().GetVisibility() != "hidden")
            .Select(t => t.Text.Trim());

        return string.Join(" ", visibleText);
    }

    public async Task<Result<byte[]>> DownloadImageAsync(string imageUrl)
    {
        try
        {
            var response = await _httpClient.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to download image from {ImageUrl}", imageUrl);
            return Result.Fail(new Error("Failed to download image").CausedBy(ex));
        }
    }
}
