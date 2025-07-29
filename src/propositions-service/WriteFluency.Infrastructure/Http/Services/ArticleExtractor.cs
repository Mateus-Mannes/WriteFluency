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

    public async Task<Result<string>> GetVisibleTextAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(url, cancellationToken);

            var context = BrowsingContext.New(Configuration.Default.WithCss().WithDefaultLoader());
            var parser = context.GetService<IHtmlParser>();
            var document = await parser!.ParseDocumentAsync(html, cancellationToken);

            // Remove unwanted tags
            var unwantedTags = document.QuerySelectorAll("script, style, head, noscript, iframe, svg");
            foreach (var node in unwantedTags)
            {
                node.Remove();
            }

            // Extract visible text only
            var visibleText = document.Body!.Descendants<IText>()
                .Where(t => !string.IsNullOrWhiteSpace(t.Text))
                .Where(t =>
                {
                    var parent = t.ParentElement;
                    if (parent == null) return false;

                    var style = parent.ComputeCurrentStyle();
                    if (style == null) return false;

                    return style.GetDisplay() != "none" &&
                        style.GetVisibility() != "hidden";
                })
                .Select(t => t.Text.Trim())
                .ToList();


            return string.Join(" ", visibleText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract visible text from {Url}", url);
            return Result.Fail(new Error("Failed to extract visible text").CausedBy(ex));
        }

    }

    public async Task<Result<byte[]>> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(imageUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to download image from {ImageUrl}", imageUrl);
            return Result.Fail(new Error("Failed to download image").CausedBy(ex));
        }
    }
}
