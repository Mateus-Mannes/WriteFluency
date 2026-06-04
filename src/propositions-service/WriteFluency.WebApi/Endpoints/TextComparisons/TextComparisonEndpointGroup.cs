using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WriteFluency.Endpoints;
using WriteFluency.Propositions;
using WriteFluency.WebApi.Users;

namespace WriteFluency.TextComparisons;

public class TextComparisonEndpointGroup : IEndpointMapper
{
    private const string NoStoreCacheControl = "no-store";
    private const string ProRequiredAccess = "pro_required";

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("text-comparison").WithTags("Text Comparisons");
        group.MapPost("compare-texts", CompareTexts)
            .Produces<TextComparisonResult>()
            .Produces<ProRequiredResultDto>(StatusCodes.Status403Forbidden);
    }

    private async Task<Results<
        Ok<TextComparisonResult>,
        BadRequest<string>,
        NotFound<string>,
        JsonHttpResult<ProRequiredResultDto>,
        InternalServerError<string>>> CompareTexts(
        [FromBody] CompareTextsDto compareTextsDto,
        HttpRequest request,
        HttpResponse response,
        TextComparisonService textComparisonService,
        PropositionService propositionService,
        IUsersSessionClient usersSessionClient,
        ILogger<TextComparisonEndpointGroup> logger,
        CancellationToken cancellationToken)
    {
        response.Headers.CacheControl = NoStoreCacheControl;

        if (compareTextsDto.PropositionId <= 0)
        {
            return TypedResults.BadRequest("PropositionId is required.");
        }

        if (compareTextsDto.UserText is null)
        {
            return TypedResults.BadRequest("UserText is required.");
        }

        try
        {
            var isPro = await usersSessionClient.IsProAsync(request, cancellationToken);
            var accessResult = await propositionService.GetExerciseForComparisonAsync(
                compareTextsDto.PropositionId,
                isPro,
                cancellationToken);

            if (accessResult is null)
            {
                return TypedResults.NotFound("Proposition not found");
            }

            if (!accessResult.IsGranted || accessResult.OriginalText is null)
            {
                return ProRequired(accessResult.Metadata);
            }

            logger.LogInformation(
                "Comparing text for proposition {PropositionId}",
                compareTextsDto.PropositionId);

            var result = textComparisonService
                .CompareTexts(accessResult.OriginalText, compareTextsDto.UserText);

            logger.LogInformation(
                "Comparison result for proposition {PropositionId}: Accuracy={AccuracyPercentage}, Comparisons={Comparisons}",
                compareTextsDto.PropositionId,
                result.AccuracyPercentage,
                result.Comparisons.Count);

            return TypedResults.Ok(result);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error comparing text for proposition {PropositionId}", compareTextsDto.PropositionId);
            return TypedResults.InternalServerError("Unable to compare texts.");
        }
    }

    private static JsonHttpResult<ProRequiredResultDto> ProRequired(PropositionMetadataDto metadata)
    {
        return TypedResults.Json(
            new ProRequiredResultDto(ProRequiredAccess, metadata),
            statusCode: StatusCodes.Status403Forbidden);
    }
}
