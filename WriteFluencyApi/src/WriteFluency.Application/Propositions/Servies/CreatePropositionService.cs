using WriteFluency.Common;
using WriteFluency.Application.Propositions.Interfaces;
using WriteFluency.TextComparisons;
using FluentResults.Extensions;
using FluentResults;

namespace WriteFluency.Propositions;

public class CreatePropositionService
{
    private readonly INewsClient _newsClient;
    private readonly IArticleExtractor _articleExtractor;
    private readonly IGenerativeAIClient _generativeAIClient;
    private readonly IFileService _fileService;

    public CreatePropositionService(
        INewsClient newsClient,
        IArticleExtractor articleExtractor,
        IGenerativeAIClient generativeAIClient,
        IFileService fileService)
    {
        _articleExtractor = articleExtractor;
        _newsClient = newsClient;
        _generativeAIClient = generativeAIClient;
        _fileService = fileService;
    }

    public async Task<IEnumerable<Proposition>> CreatePropositionsAsync(CreatePropositionDto dto, int quantity, CancellationToken cancellationToken = default)
    {
        var newsResult = await _newsClient.GetNewsAsync(dto.Subject, dto.PublishedOn, quantity, cancellationToken);
        if (newsResult.IsFailed) return Enumerable.Empty<Proposition>();

        var propositions = new List<Proposition>();
        foreach (var newsArticle in newsResult.Value)
        {
            var propositionResult = await BuildPropositionAsync(dto, newsArticle, cancellationToken);
            if (propositionResult.IsSuccess) propositions.Add(propositionResult.Value);
        }
        return propositions;
    }

    private async Task<Result<Proposition>> BuildPropositionAsync(CreatePropositionDto dto, NewsDto newsArticle, CancellationToken cancellationToken = default)
    {
        var builder = new PropositionBuilder();

        await _articleExtractor.DownloadImageAsync(newsArticle.ImageUrl, cancellationToken)
            .Bind(file =>
            {
                using var imageStream = new MemoryStream(file);
                return _fileService.UploadFileAsync("images", imageStream, cancellationToken);
            })
            .Bind(fileId => builder.SetImageFileId(fileId.ToString()));

        return await _articleExtractor.GetVisibleTextAsync(newsArticle.Url, cancellationToken)
            .Map(articleText => articleText.Length > 3000 ? articleText[..3000] : articleText)
            .Bind(articleText => builder.SetArticleText(articleText))
            .Bind(articleText => _generativeAIClient.GenerateTextAsync(dto.Complexity, articleText, cancellationToken))
            .Bind(propositionText => builder.SetPropositionText(propositionText))
            .Bind(propositionText => _generativeAIClient.GenerateAudioAsync(propositionText, cancellationToken))
            .Bind(audio => builder.SetAudioVoice(audio))
            .Bind(audio =>
            {
                using var stream = new MemoryStream(audio.Audio);
                return _fileService.UploadFileAsync("propositions", stream, cancellationToken);
            })
            .Bind(fileId => builder.SetAudioFileId(fileId.ToString()))
            .Bind(_ => builder.Build(dto, newsArticle));
    }
}
