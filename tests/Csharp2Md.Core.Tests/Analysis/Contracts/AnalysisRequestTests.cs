using Csharp2Md.Core.Analysis.Contracts;

namespace Csharp2Md.Core.Tests.Analysis.Contracts;

public sealed class AnalysisRequestTests
{
    [Fact] public void Create_OmittedOptions_AppliesSyntaxOnlyUntrustedAndTenMinuteDefaults()
    {
        var result = AnalysisRequest.Create("input", "output");
        Assert.True(result.IsSuccess);
        Assert.Equal(AnalysisMode.SyntaxOnly, result.Request!.Options.Mode);
        Assert.Equal(TrustMode.Untrusted, result.Request.Options.Trust);
        Assert.Equal(TimeSpan.FromMinutes(10), result.Request.Options.ServiceTimeout);
    }

    [Fact] public void Create_TrustedSemanticOptions_AcceptsRequest() => Assert.True(AnalysisRequest.Create("input", "output", options: new AnalysisOptions { Mode = AnalysisMode.Semantic, Trust = TrustMode.TrustedSolution }).IsSuccess);
    [Fact] public void Create_TrustedSemanticGeneratorOptions_AcceptsRequest() => Assert.True(AnalysisRequest.Create("input", "output", options: new AnalysisOptions { Mode = AnalysisMode.Semantic, Trust = TrustMode.TrustedSolution, IncludeSourceGenerators = true }).IsSuccess);
    [Fact] public void Create_SemanticWithoutTrust_ReturnsTypedFailure() => Assert.Equal(AnalysisRequestError.SemanticRequiresTrustedSolution, AnalysisRequest.Create("input", "output", options: new AnalysisOptions { Mode = AnalysisMode.Semantic }).Error);
    [Fact] public void Create_SemanticWithUntrustedTrust_ReturnsTypedFailure() => Assert.Equal(AnalysisRequestError.SemanticRequiresTrustedSolution, AnalysisRequest.Create("input", "output", options: new AnalysisOptions { Mode = AnalysisMode.Semantic, Trust = TrustMode.Untrusted }).Error);
    [Fact] public void Create_GeneratorsInSyntaxOnly_ReturnsTypedFailure() => Assert.Equal(AnalysisRequestError.GeneratorsRequireTrustedSemanticMode, AnalysisRequest.Create("input", "output", options: new AnalysisOptions { IncludeSourceGenerators = true }).Error);
    [Fact] public void Create_GeneratorsWithUntrustedSemantic_ReturnsTrustFailure() => Assert.Equal(AnalysisRequestError.SemanticRequiresTrustedSolution, AnalysisRequest.Create("input", "output", options: new AnalysisOptions { Mode = AnalysisMode.Semantic, IncludeSourceGenerators = true }).Error);
    [Theory] [InlineData(0)] [InlineData(-1)] public void Create_NonPositiveTimeout_ReturnsTypedFailure(int minutes) => Assert.Equal(AnalysisRequestError.InvalidTimeout, AnalysisRequest.Create("input", "output", options: new AnalysisOptions { ServiceTimeout = TimeSpan.FromMinutes(minutes) }).Error);
    [Fact] public void Create_EmptyInput_ReturnsTypedFailure() => Assert.Equal(AnalysisRequestError.EmptyInput, AnalysisRequest.Create(" ", "output").Error);
    [Fact] public void Create_EmptyOutputRoot_ReturnsTypedFailure() => Assert.Equal(AnalysisRequestError.EmptyOutputRoot, AnalysisRequest.Create("input", " ").Error);
}
