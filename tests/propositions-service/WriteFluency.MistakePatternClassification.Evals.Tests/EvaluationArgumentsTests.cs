using Shouldly;

namespace WriteFluency.MistakePatternClassification.Evals.Tests;

public sealed class EvaluationArgumentsTests
{
    [Fact]
    public void Parse_ShouldUseDefaultPricing()
    {
        var arguments = EvaluationArguments.Parse([]);

        arguments.InputUsdPerMillionTokens.ShouldBe(
            EvaluationPricing.DefaultInputUsdPerMillionTokens);
        arguments.OutputUsdPerMillionTokens.ShouldBe(
            EvaluationPricing.DefaultOutputUsdPerMillionTokens);
    }

    [Fact]
    public void Parse_ShouldUsePricingOverrides()
    {
        var arguments = EvaluationArguments.Parse(
            [
                "--input-usd-per-million-tokens",
                "0.12",
                "--output-usd-per-million-tokens",
                "0.98"
            ]);

        arguments.InputUsdPerMillionTokens.ShouldBe(0.12m);
        arguments.OutputUsdPerMillionTokens.ShouldBe(0.98m);
    }
}
