using FluentResults;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WriteFluency.Endpoints;
using WriteFluency.TextComparisons;

namespace WriteFluency.Propositions;

public class PropositionEndpointGroup : IEndpointMapper
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("proposition").WithTags("Propositions");
        group.MapPost("/generate-proposition", GeneratePropositionAsync);
        group.MapGet("/topics", GetTopics).Produces<Result<TopicsDto>>();
    }

    public async Task<Results<Ok<PropositionDto>, InternalServerError<string>>> GeneratePropositionAsync(
        GetPropositionDto generatePropositionDto,
        PropositionService propositionService,
        ILogger<PropositionEndpointGroup> logger)
    {
        try
        {
            var proposition = await propositionService.GetAsync(generatePropositionDto);
            return TypedResults.Ok(proposition);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error generating proposition");
            return TypedResults.InternalServerError("Unable to generate proposition");
        }
    }

    public IResult GetTopics()
    {
        return Results.Ok(new TopicsDto(
            Enum.GetNames(typeof(ComplexityEnum)),
            Enum.GetNames(typeof(SubjectEnum))));
    }
}
