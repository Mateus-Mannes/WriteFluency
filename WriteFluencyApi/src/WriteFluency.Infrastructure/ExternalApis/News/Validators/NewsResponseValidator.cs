using FluentValidation;

namespace WriteFluency.Infrastructure.ExternalApis;

public class NewsResponseValidator : AbstractValidator<NewsResponse>
{

    public NewsResponseValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleForEach(x => x.Data).ChildRules(data =>
        {
            data.RuleFor(x => x.Uuid).NotEmpty();
            data.RuleFor(x => x.Title).NotEmpty();
            data.RuleFor(x => x.Url).NotEmpty().Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute));
            data.RuleFor(x => x.ImageUrl).NotEmpty().Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute));
        }).When(x => x.Data != null && x.Data.Count > 0);
    }
}
