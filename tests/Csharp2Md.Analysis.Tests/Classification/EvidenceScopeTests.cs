using Csharp2Md.Analysis.Classification;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class EvidenceScopeTests
{
    [Fact]
    [Trait("Requirement", "GCPC-039")]
    public void For_StructuralRelation_ExcludesInvocationAndDataAccessButKeepsDeclarationEvidence()
    {
        var source = Document();
        var target = Symbol();
        var declaration = CreateObservation(target, ObservationKind.BaseType, ordinal: 1);
        var invocation = CreateObservation(target, ObservationKind.Invocation, ordinal: 1);
        var dataAccess = CreateObservation(target, ObservationKind.DataAccess, ordinal: 1);

        var chain = EvidenceScope.For(source, target, RelationKind.Contains, [declaration, invocation, dataAccess]);

        Assert.Contains(declaration.Identity, chain.DerivedFrom);
        Assert.DoesNotContain(chain.DerivedFrom, identity => identity.Kind is ObservationKind.Invocation);
        Assert.DoesNotContain(chain.DerivedFrom, identity => identity.Kind is ObservationKind.DataAccess);
    }

    [Fact]
    [Trait("Requirement", "GCPC-039")]
    public void For_CausalRelation_ContainsExactlyTheOccurrenceThatProducedIt()
    {
        var source = Symbol();
        var target = Symbol();
        var occurrence = CreateObservation(source, ObservationKind.Invocation, ordinal: 1);

        var chain = EvidenceScope.For(source, target, RelationKind.Invokes, [occurrence]);

        Assert.Equal(new[] { occurrence.Identity }, chain.DerivedFrom.ToArray());
    }

    [Fact]
    [Trait("Requirement", "APR-11")]
    public void Qualifying_Contains_DropsInvocationAndDataAccess()
    {
        var target = Symbol();
        var declaration = CreateObservation(target, ObservationKind.BaseType, ordinal: 1);
        var invocation = CreateObservation(target, ObservationKind.Invocation, ordinal: 1);
        var dataAccess = CreateObservation(target, ObservationKind.DataAccess, ordinal: 1);

        var qualifying = EvidenceScope.Qualifying(
            RelationKind.Contains,
            [declaration, invocation, dataAccess]);

        Assert.Equal([declaration], qualifying);
    }

    [Fact]
    [Trait("Requirement", "APR-11")]
    [Trait("Requirement", "APR-13")]
    public void Qualifying_Contains_OnlyBehavioral_ReturnsEmptyWithoutThrowing()
    {
        var target = Symbol();
        var invocation = CreateObservation(target, ObservationKind.Invocation, ordinal: 1);
        var dataAccess = CreateObservation(target, ObservationKind.DataAccess, ordinal: 1);

        var qualifying = EvidenceScope.Qualifying(
            RelationKind.Contains,
            [invocation, dataAccess]);

        Assert.Empty(qualifying);
    }

    [Fact]
    [Trait("Requirement", "APR-11")]
    public void Qualifying_Invokes_ReturnsCallerSetUnchanged()
    {
        var source = Symbol();
        var occurrence = CreateObservation(source, ObservationKind.Invocation, ordinal: 1);
        var dataAccess = CreateObservation(source, ObservationKind.DataAccess, ordinal: 1);
        Observation[] callerSet = [occurrence, dataAccess];

        var qualifying = EvidenceScope.Qualifying(RelationKind.Invokes, callerSet);

        Assert.Equal(callerSet, qualifying);
    }

    [Fact]
    [Trait("Requirement", "GCPC-039")]
    [Trait("Requirement", "APR-13")]
    public void For_NoQualifyingCandidates_RejectsTheEmptyScope()
    {
        var source = Document();
        var target = Symbol();
        var invocation = CreateObservation(target, ObservationKind.Invocation, ordinal: 1);
        var dataAccess = CreateObservation(target, ObservationKind.DataAccess, ordinal: 1);

        // Both candidates are exactly the behavioral noise a structural relation excludes, so nothing
        // survives -- the taxonomy requires a non-empty derived_from, and EvidenceChain.Create enforces
        // it (Csharp2Md.Domain.Proof.EvidenceChain).
        var exception = Assert.Throws<ArgumentException>(
            () => EvidenceScope.For(source, target, RelationKind.Contains, [invocation, dataAccess]));
        Assert.Contains("at least one observation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-039")]
    [Trait("Requirement", "APR-13")]
    public void For_EmptyCandidateSet_RejectsTheEmptyScope()
    {
        var source = Document();
        var target = Symbol();

        var exception = Assert.Throws<ArgumentException>(
            () => EvidenceScope.For(source, target, RelationKind.Contains, []));
        Assert.Contains("at least one observation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-039")]
    public void For_UninitializedEndpoint_IsRejected()
    {
        var target = Symbol();
        var declaration = CreateObservation(target, ObservationKind.BaseType, ordinal: 1);

        Assert.Throws<ArgumentException>(
            () => EvidenceScope.For(default, target, RelationKind.Contains, [declaration]));
    }

    private static FactReference Document()
    {
        var document = Csharp2Md.Domain.Facts.Document.Create(
            ProjectId.Create(SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.slnx"), "Acme/Acme.csproj"),
            "Acme/Widget.cs");
        return document.Reference;
    }

    private static FactReference Symbol()
    {
        var project = ProjectId.Create(SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.slnx"), "Acme/Acme.csproj");
        var symbol = Csharp2Md.Domain.Facts.Symbol.Create(
            CanonicalSymbolSignature.Create("method", "global::Acme.Widget", "Get", 0, "global::System.Void"),
            project,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        return symbol.Reference;
    }

    private static Observation CreateObservation(FactReference owner, ObservationKind kind, int ordinal) =>
        Observation.Create(
            owner,
            kind,
            NormalizedPayload.Create([]),
            ordinal,
            new EvidenceLocator(DocumentId.Create("doc"), "Acme/Widget.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("bound", "bound"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
}
