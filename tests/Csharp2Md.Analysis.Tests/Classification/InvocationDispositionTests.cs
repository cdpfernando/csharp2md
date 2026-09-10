using Csharp2Md.Analysis.Classification;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class InvocationDispositionTests
{
    [Fact]
    [Trait("Requirement", "GCPC-011")]
    public void Create_EveryNonExclusionKind_IsRepresentable()
    {
        InvocationDispositionKind[] kinds =
        [
            InvocationDispositionKind.Confirmed,
            InvocationDispositionKind.Candidate,
            InvocationDispositionKind.Unresolved,
            InvocationDispositionKind.OpenFrontier,
        ];

        foreach (var kind in kinds)
        {
            var disposition = InvocationDisposition.Create(Occurrence(1), kind);

            Assert.Equal(kind, disposition.Kind);
            Assert.Null(disposition.ExclusionCategory);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-011")]
    [Trait("Requirement", "GCPC-016")]
    public void Create_CategorizedExclusion_IsRepresentable()
    {
        InvocationExclusionCategory[] categories =
        [
            InvocationExclusionCategory.ExternalFrameworkCallable,
            InvocationExclusionCategory.DuplicateEdge,
        ];

        foreach (var category in categories)
        {
            var disposition = InvocationDisposition.Create(Occurrence(1), category);

            Assert.Equal(InvocationDispositionKind.Excluded, disposition.Kind);
            Assert.Equal(category, disposition.ExclusionCategory);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-011")]
    public void Create_ExcludedKindWithoutACategory_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => InvocationDisposition.Create(Occurrence(1), InvocationDispositionKind.Excluded));
    }

    [Fact]
    [Trait("Requirement", "GCPC-011")]
    public void Ledger_OneDispositionPerOccurrence_IsCollected()
    {
        var ledger = new InvocationDispositionLedger();
        var confirmed = InvocationDisposition.Create(Occurrence(1), InvocationDispositionKind.Confirmed);
        var candidate = InvocationDisposition.Create(Occurrence(2), InvocationDispositionKind.Candidate);

        ledger.Add(confirmed);
        ledger.Add(candidate);

        Assert.Equal(new[] { confirmed, candidate }, ledger.Dispositions.ToArray());
        Assert.Empty(ledger.Duplicates);
    }

    [Fact]
    [Trait("Requirement", "GCPC-011")]
    public void Ledger_TwoDispositionsForOneOccurrence_IsDetectableNotSilentlyOverwritten()
    {
        var ledger = new InvocationDispositionLedger();
        var occurrence = Occurrence(1);
        var first = InvocationDisposition.Create(occurrence, InvocationDispositionKind.Confirmed);
        var second = InvocationDisposition.Create(occurrence, InvocationDispositionKind.Unresolved);

        ledger.Add(first);
        ledger.Add(second);

        // The first disposition recorded for the occurrence is kept ...
        var kept = Assert.Single(ledger.Dispositions);
        Assert.Equal(first, kept);
        Assert.Equal(InvocationDispositionKind.Confirmed, kept.Kind);
        // ... and the conflicting second one is detectable, not silently dropped or overwritten.
        var duplicate = Assert.Single(ledger.Duplicates);
        Assert.Equal(second, duplicate);
        Assert.Equal(occurrence, duplicate.Occurrence);
    }

    private static ObservationIdentity Occurrence(int ordinal) =>
        new(Owner(), ObservationKind.Invocation, NormalizedPayload.Create([]), ordinal);

    private static FactReference Owner()
    {
        var project = ProjectId.Create(SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.slnx"), "Acme/Acme.csproj");
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", "global::Acme.Widget", "Get", 0, "global::System.Void"),
            project,
            Csharp2Md.Domain.Facets.SymbolFacetSet.Create([Csharp2Md.Domain.Facets.SymbolFacet.Callable]));
        return symbol.Reference;
    }
}
