using WriteFluency.Common;
using WriteFluency.Application.Propositions.Interfaces;
using WriteFluency.TextComparisons;

namespace WriteFluency.Propositions;

public class PropositionService
{
    private readonly INewsClient _newsClient;
    private readonly IArticleExtractor _articleExtractor;
    private readonly IGenerativeAIClient _generativeAIClient;
    private readonly IFileService _fileService;

    public PropositionService(
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
    
    public async Task GeneratePropositionsAsync(DateTime publishedOn, SubjectEnum subject, ComplexityEnum complexity) 
    {
        // Do today news query
        var newsResult = await _newsClient.GetNewsAsync(subject, publishedOn);
        if (newsResult.IsFailed) return;

        // do the web scraping of the news
        foreach (var newsArticle in newsResult.Value)
        {
            var articleText = await _articleExtractor.GetVisibleTextAsync(newsArticle.Url);

            // check if the text is too long, if so, truncate it
            articleText = articleText.Length > 3000 ? articleText[..3000] : articleText;

            // Generate a summarized text of the article
            var propositionTextResult = await _generativeAIClient.GenerateTextAsync(complexity, articleText);
            if (propositionTextResult.IsFailed) continue;

            // Generate an audio of the summarized text
            var propositionAudioResult = await _generativeAIClient.GenerateAudioAsync(propositionTextResult.Value);
            if (propositionAudioResult.IsFailed) continue;

            // Upload the audio to the file storage
            using var stream = new MemoryStream(propositionAudioResult.Value.Audio);
            var fileIdResult = await _fileService.UploadFileAsync("propositions", stream);
            if (fileIdResult.IsFailed) continue;

            // Upload the image to the file storage
            var imageResult = await _articleExtractor.DownloadImageAsync(newsArticle.ImageUrl);
            Guid? imageFileId = null;
            if (imageResult.IsSuccess)
            {
                using var imageStream = new MemoryStream(imageResult.Value);
                var imageFileIdResult = await _fileService.UploadFileAsync("images", imageStream);
                if (imageFileIdResult.IsSuccess) imageFileId = imageFileIdResult.Value;
            }

            // Save the proposition to the database
        }
        

        // generate the text using ai

        // generate the audio

        // continue ultil reach the limit of requests or the limit of propositions per theme
        // from the oldest date beyond
    }
}
