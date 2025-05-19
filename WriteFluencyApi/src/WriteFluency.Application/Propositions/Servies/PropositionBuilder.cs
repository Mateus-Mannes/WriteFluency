using System.ComponentModel.DataAnnotations;
using FluentResults;

namespace WriteFluency.Propositions;

public class PropositionBuilder
{
        private string? ImageFileId;
        public Result<string?> SetImageFileId(string? imageFileId)
        {
            ImageFileId = imageFileId;
            return Result.Ok(imageFileId);
        }

        [Required]
        private string? ArticleText;
        public Result<string> SetArticleText(string articleText)
        {
            ArticleText = articleText;
            return Result.Ok(articleText);
        }


    [Required]
    private string? PropositionText;
    public Result<string> SetPropositionText(string propositionText)
    {
        PropositionText = propositionText;
        return Result.Ok(propositionText);
    }

    [Required]
    private string? AudioVoice;
    public Result<AudioDto> SetAudioVoice(AudioDto audio)
    {
        AudioVoice = audio.Voice;
        return Result.Ok(audio);
    }

    [Required]
    private string? AudioFileId;
    public Result<string> SetAudioFileId(string audioFileId)
    {
        AudioFileId = audioFileId;
        return Result.Ok(audioFileId);
    }

    public Result<Proposition> Build(CreatePropositionDto dto, NewsDto newsArticle)
    {
        if(newsArticle is null) return Result.Fail(new Error("News article cannot be null"));
        
        // validate through data annotations
        var validationContext = new ValidationContext(this);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(this, validationContext, validationResults, true);
        if (!isValid)
        {
            var errors = validationResults.Select(vr => new Error(vr.ErrorMessage));
            return Result.Fail(errors);
        }

        // create the proposition
        var proposition = new Proposition
        {
            PublishedOn = dto.PublishedOn,
            SubjectId = dto.Subject,
            ComplexityId = dto.Complexity,
            AudioFileId = Guid.Parse(AudioFileId!),
            Voice = AudioVoice!,
            Text = PropositionText!,
            TextLength = PropositionText!.Length,
            ImageFileId = string.IsNullOrEmpty(ImageFileId) ? null : Guid.Parse(ImageFileId),
            CreatedAt = DateTime.UtcNow,
            NewsInfo = new NewsInfo
            {
                Id = newsArticle!.ExternalId,
                Title = newsArticle.Title,
                Description = newsArticle.Description,
                Url = newsArticle.Url,
                ImageUrl = newsArticle.ImageUrl,
                Text = ArticleText!,
                TextLength = ArticleText!.Length
            }
        };

        return Result.Ok(proposition);
    }
    
}
