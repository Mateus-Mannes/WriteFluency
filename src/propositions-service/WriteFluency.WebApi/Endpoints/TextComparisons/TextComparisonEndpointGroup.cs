using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;
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
        CorrectionOrchestrationService correctionOrchestrationService,
        PropositionService propositionService,
        IUsersSessionClient usersSessionClient,
        IOptions<TextComparisonRefinementValidationOptions> validationOptions,
        ILogger<TextComparisonEndpointGroup> logger,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        response.Headers.CacheControl = NoStoreCacheControl;

        if (compareTextsDto.PropositionId <= 0)
        {
            return TypedResults.BadRequest("PropositionId is required.");
        }

        var userTextValidation = TextComparisonRequestValidation.ValidateUserText(
            compareTextsDto.UserText,
            validationOptions.Value.MaxUserTextLength);
        if (!userTextValidation.IsValid)
        {
            LogRejectedRequest(
                logger,
                compareTextsDto.PropositionId,
                userTextValidation.UserTextLength,
                userTextValidation.ReasonCode);
            return TypedResults.BadRequest(userTextValidation.ErrorMessage!);
        }

        var userText = compareTextsDto.UserText!;

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

            var orchestrationResult = await correctionOrchestrationService.CompareTextsAsync(
                accessResult.OriginalText,
                userText,
                isPro,
                cancellationToken);
            var result = orchestrationResult.Result;

            logger.LogInformation(
                "Comparison completed for proposition {PropositionId}: IsPro={IsPro}, CorrectionMode={CorrectionMode}, StaticComparisons={StaticComparisons}, RemovedComparisons={RemovedComparisons}, FinalComparisons={FinalComparisons}, ValidationReason={ValidationReason}, Accuracy={AccuracyPercentage}, DurationMs={DurationMs}",
                compareTextsDto.PropositionId,
                isPro,
                result.CorrectionMode,
                orchestrationResult.StaticComparisonCount,
                orchestrationResult.RemovedComparisonCount,
                result.Comparisons.Count,
                orchestrationResult.ValidationReasonCode,
                result.AccuracyPercentage,
                stopwatch.ElapsedMilliseconds);

            return TypedResults.Ok(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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

    private static void LogRejectedRequest(
        ILogger<TextComparisonEndpointGroup> logger,
        int propositionId,
        int? userTextLength,
        string reason)
    {
        logger.LogWarning(
            "Comparison request rejected for proposition {PropositionId}: UserTextLength={UserTextLength}, Reason={Reason}",
            propositionId,
            userTextLength,
            reason);
    }
}
