using FluentResults;

namespace WriteFluency.Application.Propositions.Interfaces;

public interface IArticleContentPolicyValidator
{
    Result Validate(string articleContent);
}
