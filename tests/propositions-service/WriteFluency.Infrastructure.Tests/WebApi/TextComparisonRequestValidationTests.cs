using Shouldly;
using WriteFluency.TextComparisons;

namespace WriteFluency.Infrastructure.Tests.WebApi;

public class TextComparisonRequestValidationTests
{
    [Fact]
    public void ValidateUserText_WhenNull_ShouldReject()
    {
        var result = TextComparisonRequestValidation.ValidateUserText(
            null,
            maxUserTextLength: 3000);

        result.IsValid.ShouldBeFalse();
        result.ReasonCode.ShouldBe("missing_user_text");
        result.ErrorMessage.ShouldBe("UserText is required.");
        result.UserTextLength.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateUserText_WhenEmptyOrWhitespace_ShouldReject(string value)
    {
        var result = TextComparisonRequestValidation.ValidateUserText(
            value,
            maxUserTextLength: 3000);

        result.IsValid.ShouldBeFalse();
        result.ReasonCode.ShouldBe("empty_user_text");
        result.ErrorMessage.ShouldBe("UserText cannot be empty.");
        result.UserTextLength.ShouldBe(value.Length);
    }

    [Fact]
    public void ValidateUserText_WhenOversized_ShouldReject()
    {
        var result = TextComparisonRequestValidation.ValidateUserText(
            new string('a', 3001),
            maxUserTextLength: 3000);

        result.IsValid.ShouldBeFalse();
        result.ReasonCode.ShouldBe("user_text_too_long");
        result.ErrorMessage.ShouldBe("UserText cannot exceed 3000 characters.");
        result.UserTextLength.ShouldBe(3001);
    }

    [Fact]
    public void ValidateUserText_WhenValid_ShouldPass()
    {
        var result = TextComparisonRequestValidation.ValidateUserText(
            "submitted text",
            maxUserTextLength: 3000);

        result.IsValid.ShouldBeTrue();
        result.ReasonCode.ShouldBe("valid");
        result.ErrorMessage.ShouldBeNull();
        result.UserTextLength.ShouldBe(14);
    }
}
