using FluentResults;
using Microsoft.AspNetCore.Http.HttpResults;
using WriteFluency.Endpoints;

namespace WriteFluency.Propositions;

public class PropositionEndpointGroup : IEndpointMapper
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("proposition").WithTags("Propositions");
        group.MapGet("/{id}", GetPropositionAsync);
        group.MapPost("/generate-proposition", GeneratePropositionAsync);
        group.MapGet("/topics", GetTopics).Produces<Result<TopicsDto>>();
    }

    public async Task<Results<Ok<Proposition>, InternalServerError<string>, NotFound<string>>> GetPropositionAsync(
        int id,
        PropositionService propositionService,
        ILogger<PropositionEndpointGroup> logger)
    {
        try
        {
            var proposition = await propositionService.GetAsync(id);

            if (proposition is null)
            {
                return TypedResults.NotFound("Proposition not found");
            }

            return TypedResults.Ok(proposition);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving proposition");
            return TypedResults.InternalServerError("Unable to retrieve proposition");
        }
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
