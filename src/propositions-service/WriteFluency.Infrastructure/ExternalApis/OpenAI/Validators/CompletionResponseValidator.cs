using FluentValidation;

namespace WriteFluency.Infrastructure.ExternalApis;

public class CompletionResponseValidator : AbstractValidator<CompletionResponse>
{
    public CompletionResponseValidator()
    {
        RuleFor(x => x.Choices)
            .NotEmpty().WithMessage("Choices cannot be empty.");

        RuleForEach(x => x.Choices)
            .NotNull().WithMessage("Choice cannot be null.")
            .ChildRules(x =>
            {
                x.RuleFor(c => c.Message).NotNull().WithMessage("Message cannot be null.");
                x.RuleFor(c => c.Message.Content).NotEmpty().WithMessage("Content cannot be empty.")
                    .When(c => c.Message is not null);
            }).When(x => x.Choices is not null);
    }
}
