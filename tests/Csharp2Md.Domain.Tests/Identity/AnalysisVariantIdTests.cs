using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Tests.Identity;

public sealed class AnalysisVariantIdTests
{
    [Fact]
    [Trait("Requirement", "TAX-78")]
    public void Create_ChangingAnySingleComponent_ChangesTheIdentity()
    {
        var baseline = AnalysisVariantId.Create("net10.0", "Release", ["TRACE"], "ci");

        var differentTfm = AnalysisVariantId.Create("net9.0", "Release", ["TRACE"], "ci");
        var differentConfiguration = AnalysisVariantId.Create("net10.0", "Debug", ["TRACE"], "ci");
        var differentSymbols = AnalysisVariantId.Create("net10.0", "Release", ["DEBUG"], "ci");
        var differentEnvironment = AnalysisVariantId.Create("net10.0", "Release", ["TRACE"], "local");

        Assert.NotEqual(baseline.Value, differentTfm.Value);
        Assert.NotEqual(baseline.Value, differentConfiguration.Value);
        Assert.NotEqual(baseline.Value, differentSymbols.Value);
        Assert.NotEqual(baseline.Value, differentEnvironment.Value);
    }

    [Fact]
    [Trait("Requirement", "TAX-78")]
    public void Create_SymbolsInDifferentOrderWithADuplicate_ProduceOneIdentity()
    {
        var forward = AnalysisVariantId.Create("net10.0", "Release", ["TRACE", "DEBUG", "TRACE"], "ci");
        var reverse = AnalysisVariantId.Create("net10.0", "Release", ["DEBUG", "TRACE"], "ci");

        Assert.Equal(forward.Value, reverse.Value);
    }

    [Theory]
    [Trait("Requirement", "TAX-78")]
    [InlineData("")]
    [InlineData("net10.0 ")]
    [InlineData(" net10.0")]
    public void Create_NonCanonicalTargetFramework_IsRejectedNamingParameter(string targetFramework)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AnalysisVariantId.Create(targetFramework, "Release", ["TRACE"], "ci"));

        Assert.Equal("targetFramework", exception.ParamName);
    }

    [Theory]
    [Trait("Requirement", "TAX-78")]
    [InlineData("")]
    [InlineData("Release ")]
    public void Create_NonCanonicalConfiguration_IsRejectedNamingParameter(string configuration)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AnalysisVariantId.Create("net10.0", configuration, ["TRACE"], "ci"));

        Assert.Equal("configuration", exception.ParamName);
    }

    [Theory]
    [Trait("Requirement", "TAX-78")]
    [InlineData("")]
    [InlineData("ci ")]
    public void Create_NonCanonicalEnvironment_IsRejectedNamingParameter(string environment)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AnalysisVariantId.Create("net10.0", "Release", ["TRACE"], environment));

        Assert.Equal("environment", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-78")]
    public void Create_NonCanonicalSymbol_IsRejectedNamingParameter()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AnalysisVariantId.Create("net10.0", "Release", ["TRACE ", "DEBUG"], "ci"));

        Assert.Equal("symbols", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-78")]
    public void Create_HasNoAbsolutePathLocationOrTimestampParameter()
    {
        var method = typeof(AnalysisVariantId).GetMethod(nameof(AnalysisVariantId.Create))!;
        var parameterNames = method.GetParameters().Select(static p => p.Name!.ToLowerInvariant()).ToArray();

        Assert.DoesNotContain(parameterNames, static name => name.Contains("path") || name.Contains("location") || name.Contains("timestamp"));
    }
}
