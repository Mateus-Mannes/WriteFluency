using Shouldly;

namespace WriteFluency.Propositions;

public class ArticleContentPolicyValidatorTests
{
    private readonly ArticleContentPolicyValidator _validator = new();

    [Fact]
    public void ShouldIncludeIdentifiedTermsInViolationMessage()
    {
        var result = _validator.Validate("The report mentions a behead attempt and explicit torture details.");

        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);

        var errorMessage = result.Errors.Single().Message;
        errorMessage.ShouldContain("ViolenceTerms");
        errorMessage.ShouldContain("Invalid terms identified:");
        errorMessage.ShouldContain("'behead'");
        errorMessage.ShouldContain("'torture'");
    }

    [Fact]
    public void ShouldReturnOneErrorPerCategoryWithIdentifiedTerms()
    {
        var result = _validator.Validate("This sale includes a coupon and explicit porn content with abuse references.");

        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(3);

        var commercialError = result.Errors.Single(error => error.Message.Contains("CommercialContentTerms", StringComparison.Ordinal)).Message;
        commercialError.ShouldContain("'coupon'");
        commercialError.ShouldContain("'sale'");

        var sexualError = result.Errors.Single(error => error.Message.Contains("SexualTerms", StringComparison.Ordinal)).Message;
        sexualError.ShouldContain("'porn'");

        var abuseError = result.Errors.Single(error => error.Message.Contains("AbuseTerms", StringComparison.Ordinal)).Message;
        abuseError.ShouldContain("'abuse'");
    }

    [Fact]
    public void ShouldReturnSuccessWhenArticleHasNoPolicyViolations()
    {
        var result = _validator.Validate("Scientists announced a breakthrough in battery research after years of testing.");

        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }
}
