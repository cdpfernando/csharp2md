using System.Reflection;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Relations;

public sealed class ConfirmedRelationSetTests
{
    private static FactReference SolutionReference(string name) =>
        new(new FactId("solution", $"id1:solution;name={name}"), "Solution");

    private static FactReference ProjectReference(string name) =>
        new(new FactId("project", $"id1:project;name={name}"), "Project");

    private static FactReference SymbolReference(string name) =>
        new(new FactId("symbol", $"id1:symbol;name={name}"), "Symbol");

    private static EvidenceChain ValidEvidence(FactReference owner) =>
        EvidenceChain.Create([new ObservationIdentity(owner, ObservationKind.Invocation, NormalizedPayload.Create([]), 1)]);

    private static ConfirmedRelation Confirmed(string projectName = "payments") =>
        ConfirmedRelation.Create(
            RelationKind.Contains,
            SolutionReference("acme"),
            ProjectReference(projectName),
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            ValidEvidence(SolutionReference("acme")),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);

    [Fact]
    [Trait("Requirement", "TAX-62")]
    public void Add_ConfirmedRelation_IsAccepted()
    {
        var set = new ConfirmedRelationSet();
        var relation = Confirmed();

        set.Add(relation);

        Assert.Same(relation, Assert.Single(set));
    }

    [Fact]
    [Trait("Requirement", "TAX-62")]
    public void Add_OnlyAcceptsConfirmedRelation_SoACandidateOrAnUnresolvedRecordCannotBePassed()
    {
        var addMethod = typeof(ConfirmedRelationSet).GetMethod(nameof(ConfirmedRelationSet.Add), BindingFlags.Public | BindingFlags.Instance)!;
        var parameterType = addMethod.GetParameters().Single().ParameterType;

        Assert.Equal(typeof(ConfirmedRelation), parameterType);
        Assert.False(parameterType.IsAssignableFrom(typeof(CandidateLink)));
        Assert.False(parameterType.IsAssignableFrom(typeof(UnresolvedRecord)));

        var candidate = CandidateLink.Create(RelationKind.MapsTo, SymbolReference("Entity"), ProjectReference("payments"), ValidEvidence(SymbolReference("Entity")));
        var unresolved = UnresolvedRecord.Create(RelationKind.Invokes, SymbolReference("Entity"), UnresolvedCause.AmbiguousTarget, ValidEvidence(SymbolReference("Entity")));

        Assert.Equal(Resolution.Candidate, candidate.Resolution);
        Assert.Equal(Resolution.Unresolved, unresolved.Resolution);
    }

    [Fact]
    [Trait("Requirement", "TAX-62")]
    public void Set_IsEnumerableInDeterministicInsertionOrder()
    {
        var set = new ConfirmedRelationSet();
        var first = Confirmed("payments");
        var second = Confirmed("billing");

        set.Add(first);
        set.Add(second);

        Assert.Equal([first, second], set.ToArray());
    }

    [Fact]
    [Trait("Requirement", "TAX-62")]
    public void Add_TheSameRelationTwice_IsIdempotent()
    {
        var set = new ConfirmedRelationSet();
        var relation = Confirmed();

        set.Add(relation);
        set.Add(relation);

        Assert.Same(relation, Assert.Single(set));
    }

    [Fact]
    [Trait("Requirement", "TAX-62")]
    public void NoPublicMember_ExposesAWayToMutateAContainedRelation()
    {
        var publicMembers = typeof(ConfirmedRelationSet).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(publicMembers, member => member is FieldInfo);
        Assert.DoesNotContain(publicMembers, member => member is PropertyInfo property && property.CanWrite);
        Assert.DoesNotContain(publicMembers, member =>
            member is MethodInfo method
            && method.ReturnType != typeof(void)
            && method.ReturnType != typeof(int)
            && method.ReturnType != typeof(IEnumerator<ConfirmedRelation>)
            && !method.Name.StartsWith("get_", StringComparison.Ordinal));
    }
}
