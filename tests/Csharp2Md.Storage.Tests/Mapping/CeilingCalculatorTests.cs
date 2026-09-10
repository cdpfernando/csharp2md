using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Tests.Mapping;

/// <summary>GCPC-036/GCPC-037: the per-artifact byte ceiling is derived, not a constant, and publishes
/// the inputs a consumer needs to re-derive it.</summary>
public sealed class CeilingCalculatorTests
{
    [Fact]
    public void Derive_SameBudgetAndRatio_IsReproducible()
    {
        var first = CeilingCalculator.Derive(readingBudgetTokens: 60_000, maxFileReadsPerScenario: 20, bytesPerToken: 5.0);
        var second = CeilingCalculator.Derive(readingBudgetTokens: 60_000, maxFileReadsPerScenario: 20, bytesPerToken: 5.0);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Derive_DifferentDeclaredBudget_MovesTheCeiling()
    {
        var smaller = CeilingCalculator.Derive(readingBudgetTokens: 50_000);
        var larger = CeilingCalculator.Derive(readingBudgetTokens: 150_000);

        Assert.NotEqual(smaller.CeilingBytes, larger.CeilingBytes);
        Assert.True(larger.CeilingBytes > smaller.CeilingBytes);
    }

    [Fact]
    public void Derive_Defaults_LandsOnTheDocumentedThirtyTwoKibCeiling()
    {
        var calculation = CeilingCalculator.Derive();

        Assert.Equal(32 * 1024, calculation.CeilingBytes);
    }

    [Fact]
    public void TokenEstimator_IsDeclaredAndDeterministic()
    {
        Assert.False(string.IsNullOrWhiteSpace(CeilingCalculator.TokenEstimatorId));

        var first = CeilingCalculator.EstimateTokens(16_384);
        var second = CeilingCalculator.EstimateTokens(16_384);

        Assert.Equal(first, second);
        Assert.Equal(CeilingCalculator.Derive().CeilingBytes, CeilingCalculator.Derive().CeilingBytes);
    }

    [Fact]
    public void Derive_PublishedCalculation_NamesEveryInputNeededToRederiveIt()
    {
        var calculation = CeilingCalculator.Derive(readingBudgetTokens: 42_000, maxFileReadsPerScenario: 7, bytesPerToken: 3.5);

        var rederived = (int)Math.Round(
            calculation.ReadingBudgetTokens * calculation.BytesPerToken / calculation.MaxFileReadsPerScenario,
            MidpointRounding.AwayFromZero);

        Assert.Equal(42_000, calculation.ReadingBudgetTokens);
        Assert.Equal(7, calculation.MaxFileReadsPerScenario);
        Assert.Equal(3.5, calculation.BytesPerToken);
        Assert.Equal(CeilingCalculator.TokenEstimatorId, calculation.TokenEstimatorId);
        Assert.Equal(rederived, calculation.CeilingBytes);
    }
}
