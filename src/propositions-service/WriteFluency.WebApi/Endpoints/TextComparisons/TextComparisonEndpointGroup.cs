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
        AnonymousProReviewFingerprintService anonymousFingerprintService,
        AnonymousCatalogAccessFingerprintService anonymousCatalogAccessFingerprintService,
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

        var userTextLength = compareTextsDto.UserText?.Length ?? 0;
        if (userTextLength > validationOptions.Value.MaxUserTextLength)
        {
            logger.LogWarning(
                "Comparison request rejected for proposition {PropositionId}: UserTextLength={UserTextLength}, MaxUserTextLength={MaxUserTextLength}",
                compareTextsDto.PropositionId,
                userTextLength,
                validationOptions.Value.MaxUserTextLength);
            return TypedResults.BadRequest(
                $"UserText cannot exceed {validationOptions.Value.MaxUserTextLength} characters.");
        }

        try
        {
            var session = await usersSessionClient.GetSessionAsync(request, cancellationToken);
            var catalogAnonymousFingerprintHash = session.IsAuthenticated
                ? null
                : anonymousCatalogAccessFingerprintService.CreateFingerprintHash(request);
            var accessContext = new PropositionAccessContext(
                session.IsAuthenticated,
                session.IsPro,
                session.UserId,
                catalogAnonymousFingerprintHash);
            var accessResult = await propositionService.GetExerciseForComparisonAsync(
                compareTextsDto.PropositionId,
                accessContext,
                cancellationToken);

            if (accessResult is null)
            {
                return TypedResults.NotFound("Proposition not found");
            }

            if (!accessResult.IsGranted || accessResult.OriginalText is null)
            {
                return ProRequired(accessResult.Metadata);
            }

            var anonymousFingerprintHash = session.IsAuthenticated
                ? null
                : anonymousFingerprintService.CreateFingerprintHash(request);
            var orchestrationResult = await correctionOrchestrationService.CompareTextsAsync(
                new CorrectionOrchestrationRequest(
                    accessResult.OriginalText,
                    compareTextsDto.UserText ?? string.Empty,
                    session.IsAuthenticated,
                    session.IsPro,
                    session.UserId,
                    anonymousFingerprintHash,
                    EnableFreeReviewTeaser: true),
                cancellationToken);
            var result = orchestrationResult.Result;

            if (result.MistakePatternStatus == MistakePatternStatuses.SkippedUsageLimit)
            {
                logger.LogError(
                    "Pro mistake-pattern AI review limit reached for proposition {PropositionId}: UserId={UserId}, UserTextLength={UserTextLength}, StaticComparisons={StaticComparisons}, FinalComparisons={FinalComparisons}",
                    compareTextsDto.PropositionId,
                    session.UserId,
                    userTextLength,
                    orchestrationResult.StaticComparisonCount,
                    result.Comparisons.Count);
            }

            if (result.MistakePatternStatus is MistakePatternStatuses.LoginRequiredToUnlockReview
                or MistakePatternStatuses.UpgradeRequiredToUnlockReview)
            {
                logger.LogInformation(
                    "Pro review teaser locked for proposition {PropositionId}: IsAuthenticated={IsAuthenticated}, IsPro={IsPro}, Status={MistakePatternStatus}, ReviewSource={MistakePatternReviewSource}, UserTextLength={UserTextLength}, StaticComparisons={StaticComparisons}, FinalComparisons={FinalComparisons}, AnonymousFingerprintPresent={AnonymousFingerprintPresent}",
                    compareTextsDto.PropositionId,
                    session.IsAuthenticated,
                    session.IsPro,
                    result.MistakePatternStatus,
                    result.MistakePatternReviewSource,
                    userTextLength,
                    orchestrationResult.StaticComparisonCount,
                    result.Comparisons.Count,
                    anonymousFingerprintHash is not null);
            }

            logger.LogInformation(
                "Comparison completed for proposition {PropositionId}: IsAuthenticated={IsAuthenticated}, IsPro={IsPro}, CorrectionMode={CorrectionMode}, StaticComparisons={StaticComparisons}, RemovedComparisons={RemovedComparisons}, FinalComparisons={FinalComparisons}, MistakePatternStatus={MistakePatternStatus}, MistakePatternReviewSource={MistakePatternReviewSource}, MistakePatternComparisons={MistakePatternComparisons}, Accuracy={AccuracyPercentage}, DurationMs={DurationMs}",
                compareTextsDto.PropositionId,
                session.IsAuthenticated,
                session.IsPro,
                result.CorrectionMode,
                orchestrationResult.StaticComparisonCount,
                orchestrationResult.RemovedComparisonCount,
                result.Comparisons.Count,
                result.MistakePatternStatus,
                result.MistakePatternReviewSource,
                result.Comparisons.Count(comparison =>
                    comparison.MistakePatternTags?.Count > 0
                    && !string.IsNullOrWhiteSpace(comparison.MistakePatternPhrase)),
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

}
