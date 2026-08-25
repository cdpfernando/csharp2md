using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Tests.Identity;

public sealed class IdentityDeterminismTests
{
    [Fact]
    [Trait("Requirement", "TAX-70")]
    public void SolutionProjectVariantAndSymbolIdentities_UnderTwoSimulatedClonePaths_AreByteIdentical()
    {
        var underRootA = BuildUnderRootA();
        var underRootB = BuildUnderRootB();

        Assert.Equal(underRootA.Solution.Value, underRootB.Solution.Value);
        Assert.Equal(underRootA.Project.Value, underRootB.Project.Value);
        Assert.Equal(underRootA.Variant.Value, underRootB.Variant.Value);
        Assert.Equal(underRootA.Symbol.Value, underRootB.Symbol.Value);

        static (SolutionId Solution, ProjectId Project, AnalysisVariantId Variant, CanonicalSymbolSignature Symbol) BuildUnderRootA()
        {
            var solution = SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln");
            var project = ProjectId.Create(solution, "src/Acme.Payments/Acme.Payments.csproj");
            var variant = AnalysisVariantId.Create("net10.0", "Release", ["TRACE", "DEBUG"], "ci");
            var symbol = CanonicalSymbolSignature.Create("method", "global::Acme.Payment", "Run", 0, "global::System.Void");
            return (solution, project, variant, symbol);
        }

        static (SolutionId Solution, ProjectId Project, AnalysisVariantId Variant, CanonicalSymbolSignature Symbol) BuildUnderRootB()
        {
            var solution = SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln");
            var project = ProjectId.Create(solution, "src/Acme.Payments/Acme.Payments.csproj");
            var variant = AnalysisVariantId.Create("net10.0", "Release", ["TRACE", "DEBUG"], "ci");
            var symbol = CanonicalSymbolSignature.Create("method", "global::Acme.Payment", "Run", 0, "global::System.Void");
            return (solution, project, variant, symbol);
        }
    }

    [Fact]
    [Trait("Requirement", "TAX-71")]
    public void AnalysisVariantIdentity_TheOnlyCollectionBearingComponentInThisPhase_IsByteIdenticalUnderTwoShuffledSymbolOrders()
    {
        var forward = AnalysisVariantId.Create("net10.0", "Release", ["TRACE", "DEBUG", "BETA"], "ci");
        var shuffled = AnalysisVariantId.Create("net10.0", "Release", ["BETA", "TRACE", "DEBUG"], "ci");

        Assert.Equal(forward.Value, shuffled.Value);
    }

    [Theory]
    [Trait("Requirement", "TAX-76")]
    [MemberData(nameof(MissingRequiredComponentCases))]
    public void EachRequiredComponentOmittedInTurn_FailsNamingThatComponentWithNoPartialIdentity(Action buildWithMissingComponent, string expectedParameterName)
    {
        var exception = Assert.Throws<ArgumentException>(buildWithMissingComponent);

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    public static IEnumerable<object[]> MissingRequiredComponentCases()
    {
        yield return new object[] { (Action)(() => WorkspaceIdentity.Create("")), "logicalName" };
        yield return new object[] { (Action)(() => SolutionId.Create(WorkspaceIdentity.Create("acme"), "")), "logicalRelativePath" };
        yield return new object[]
        {
            (Action)(() => ProjectId.Create(SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln"), "")),
            "logicalRelativePath",
        };
        yield return new object[] { (Action)(() => AnalysisVariantId.Create("", "Release", ["TRACE"], "ci")), "targetFramework" };
        yield return new object[] { (Action)(() => AnalysisVariantId.Create("net10.0", "", ["TRACE"], "ci")), "configuration" };
        yield return new object[] { (Action)(() => AnalysisVariantId.Create("net10.0", "Release", ["TRACE"], "")), "environment" };
        yield return new object[]
        {
            (Action)(() => CanonicalSymbolSignature.Create("", "global::Acme.Payment", "Run", 0, "global::System.Void")),
            "symbolKind",
        };
    }

    [Fact]
    [Trait("Requirement", "TAX-77")]
    public void ForcedCollision_FailsNamingBothParticipatingFactTypes()
    {
        var ledger = new IdentityLedger();
        var sharedId = FactIdGrammar.Create("solution", ("path", "src/Acme.sln"));
        ledger.Register(new FactReference(sharedId, "solution"));

        var exception = Assert.Throws<ArgumentException>(() => ledger.Register(new FactReference(sharedId, "project")));

        Assert.Contains("solution", exception.Message, StringComparison.Ordinal);
        Assert.Contains("project", exception.Message, StringComparison.Ordinal);
    }
}
