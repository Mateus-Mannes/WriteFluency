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
        
        // Regenerate endpoint only available in Development
        var env = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment())
        {
            group.MapPost("/{id}/regenerate", RegeneratePropositionAsync)
                .WithDescription("Development only: Regenerate proposition from existing data");
        }
        
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

    public async Task<Results<Ok<Proposition>, InternalServerError<string>, NotFound<string>, BadRequest<string>>> RegeneratePropositionAsync(
        int id,
        PropositionService propositionService,
        ILogger<PropositionEndpointGroup> logger)
    {
        try
        {
            var result = await propositionService.RegeneratePropositionAsync(id);

            if (result.IsFailed)
            {
                var errorMessage = string.Join(", ", result.Errors.Select(e => e.Message));
                
                if (errorMessage.Contains("not found"))
                {
                    return TypedResults.NotFound(errorMessage);
                }
                
                return TypedResults.BadRequest(errorMessage);
            }

            return TypedResults.Ok(result.Value);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error regenerating proposition");
            return TypedResults.InternalServerError("Unable to regenerate proposition");
        }
    }
}
