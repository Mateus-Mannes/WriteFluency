using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WriteFluency.Endpoints;

namespace WriteFluency.TextComparisons;

public class TextComparisonEndpointGroup : IEndpointMapper
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("text-comparison").WithTags("Text Comparisons");
        group.MapPost("compare-texts", CompareTexts).Produces<Ok<List<TextComparison>>>();
    }

    private Ok<TextComparisonResult> CompareTexts(
        [FromBody] CompareTextsDto compareTextsDto,
        TextComparisonService textComparisonService,
        ILogger<TextComparisonEndpointGroup> logger)
    {
        logger.LogInformation("Comparing texts: UserText='{UserText}'", compareTextsDto.UserText);
        var result = textComparisonService
            .CompareTexts(compareTextsDto.OriginalText, compareTextsDto.UserText);
        logger.LogInformation("Comparison result: Accuracy={AccuracyPercentage}, Comparisons={Comparisons}", result.AccuracyPercentage, result.Comparisons.Count);
        return TypedResults.Ok(result);
    }
}
