using System.Reflection;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Relations;

public sealed class ConfirmedRelationTests
{
    private static FactReference SolutionReference(string name) =>
        new(new FactId("solution", $"id1:solution;name={name}"), "Solution");

    private static FactReference ProjectReference(string name) =>
        new(new FactId("project", $"id1:project;name={name}"), "Project");

    private static FactReference SymbolReference(string name) =>
        new(new FactId("symbol", $"id1:symbol;name={name}"), "Symbol");

    private static FacetBinding EmptyFacets() => FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    private static EvidenceChain ValidEvidence() =>
        EvidenceChain.Create([
            new ObservationIdentity(
                SolutionReference("acme"), ObservationKind.Invocation, NormalizedPayload.Create([]), 1),
        ]);

    private static ClassifierIdentity ValidClassifier() => ClassifierIdentity.Create("csharp2md.structural.contains", 1);

    private static ImmutableArray<AnalysisVariantId> ValidVariants() =>
        [AnalysisVariantId.Create("net10.0", "Release", [], "ci")];

    private static ConfirmedRelation CreateValid(
        FactReference? source = null,
        FactReference? target = null,
        FacetBinding? facets = null,
        EvidenceChain? derivedFrom = null,
        ClassifierIdentity? classifier = null,
        ImmutableArray<AnalysisVariantId>? variants = null) =>
        ConfirmedRelation.Create(
            RelationKind.Contains,
            source ?? SolutionReference("acme"),
            target ?? ProjectReference("payments"),
            facets ?? EmptyFacets(),
            derivedFrom ?? ValidEvidence(),
            classifier ?? ValidClassifier(),
            variants ?? ValidVariants(),
            EvidenceMethod.Syntactic);

    [Fact]
    [Trait("Requirement", "TAX-44")]
    public void Create_RegisteredTriple_ProducesTheConfirmedRelation()
    {
        var relation = CreateValid();

        Assert.Equal(RelationKind.Contains, relation.Kind);
        Assert.Equal(SolutionReference("acme"), relation.Source);
        Assert.Equal(ProjectReference("payments"), relation.Target);
    }

    [Fact]
    [Trait("Requirement", "TAX-45")]
    public void Create_UnregisteredTriple_IsRejectedNamingSourceRelationAndTarget()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateValid(
            source: SolutionReference("acme"), target: SymbolReference("Charge")));

        Assert.Contains("Solution", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(RelationKind.Contains), exception.Message, StringComparison.Ordinal);
        Assert.Contains("Symbol", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_DefaultSource_IsRejectedNamingSource()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateValid(source: default(FactReference)));

        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    [Trait("Requirement", "TAX-63")]
    public void Create_DefaultTarget_IsRejectedNamingTarget_SoNoDanglingEdgeCanBeConstructed()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateValid(target: default(FactReference)));

        Assert.Equal("target", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_DefaultFacets_IsRejectedNamingFacets()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateValid(facets: default(FacetBinding)));

        Assert.Equal("facets", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_DefaultEvidenceChain_IsRejectedNamingDerivedFrom()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateValid(derivedFrom: default(EvidenceChain)));

        Assert.Equal("derivedFrom", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_DefaultClassifier_IsRejectedNamingClassifier()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateValid(classifier: default(ClassifierIdentity)));

        Assert.Equal("classifier", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_DefaultAnalysisVariants_IsRejectedNamingAnalysisVariants()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateValid(variants: default(ImmutableArray<AnalysisVariantId>)));

        Assert.Equal("analysisVariants", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_EmptyAnalysisVariants_IsRejectedNamingAnalysisVariants()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateValid(variants: ImmutableArray<AnalysisVariantId>.Empty));

        Assert.Equal("analysisVariants", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-61")]
    public void Resolution_AlwaysReadsConfirmed()
    {
        var relation = CreateValid();

        Assert.Equal(Resolution.Confirmed, relation.Resolution);
    }

    [Fact]
    [Trait("Requirement", "TAX-61")]
    public void Resolution_HasNoSetterAndNoConstructorOrFactoryParameter()
    {
        var property = typeof(ConfirmedRelation).GetProperty(nameof(ConfirmedRelation.Resolution))!;
        var factory = typeof(ConfirmedRelation).GetMethod(nameof(ConfirmedRelation.Create), BindingFlags.Public | BindingFlags.Static)!;
        var constructors = typeof(ConfirmedRelation).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Null(property.GetSetMethod(nonPublic: true));
        Assert.DoesNotContain(factory.GetParameters(), p => p.ParameterType == typeof(Resolution));
        Assert.All(constructors, ctor => Assert.DoesNotContain(ctor.GetParameters(), p => p.ParameterType == typeof(Resolution)));
    }

    [Fact]
    [Trait("Requirement", "ENG-58")]
    public void Create_TakesEvidenceMethodImmediatelyAfterAnalysisVariants()
    {
        var factory = typeof(ConfirmedRelation).GetMethod(nameof(ConfirmedRelation.Create), BindingFlags.Public | BindingFlags.Static)!;
        var parameters = factory.GetParameters();
        var variantsIndex = Array.FindIndex(parameters, parameter => parameter.Name == "analysisVariants");
        var evidenceIndex = Array.FindIndex(parameters, parameter => parameter.Name == "evidenceMethod");

        Assert.True(variantsIndex >= 0, "Create is missing the analysisVariants parameter.");
        Assert.Equal(typeof(EvidenceMethod), parameters[evidenceIndex].ParameterType);
        Assert.Equal(variantsIndex + 1, evidenceIndex);
    }

    [Fact]
    [Trait("Requirement", "ENG-58")]
    public void Create_InvokesWithSyntacticEvidence_IsRejectedNamingTheRelation()
    {
        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            RelationKind.Invokes,
            SymbolReference("Caller"),
            SymbolReference("Callee"),
            EmptyFacets(),
            ValidEvidence(),
            ValidClassifier(),
            ValidVariants(),
            EvidenceMethod.Syntactic));

        Assert.Contains(nameof(RelationKind.Invokes), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(EvidenceMethod.Semantic), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(EvidenceMethod.Syntactic), exception.Message, StringComparison.Ordinal);
    }
}
