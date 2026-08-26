using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class OccurrenceOrdinalAssignerTests
{
    [Fact]
    [Trait("Requirement", "ROSE-43")]
    public void Assign_TwoDraftsSharingOwnerKindPayload_GetOrdinalsOneAndTwoInLocatorOrder()
    {
        var later = CreateDraft("src/Acme.Orders/Api/OrdersController.cs", new SourceSpan(20, 1, 20, 12));
        var earlier = CreateDraft("src/Acme.Orders/Api/OrdersController.cs", new SourceSpan(4, 1, 4, 8));

        var assigned = OccurrenceOrdinalAssigner.Assign([later, earlier]);

        Assert.Equal(2, assigned.Length);
        Assert.Equal(1, assigned[0].Identity.OccurrenceOrdinal);
        Assert.Equal(earlier.Locator, assigned[0].Locator);
        Assert.Equal(2, assigned[1].Identity.OccurrenceOrdinal);
        Assert.Equal(later.Locator, assigned[1].Locator);
        Assert.NotEqual(assigned[0].Identity, assigned[1].Identity);
        Assert.Equal(assigned[0].Identity.Owner, assigned[1].Identity.Owner);
        Assert.Equal(assigned[0].Identity.Kind, assigned[1].Identity.Kind);
        Assert.Equal(assigned[0].Identity.Payload, assigned[1].Identity.Payload);
    }

    [Fact]
    [Trait("Requirement", "ROSE-43")]
    public void Assign_ShuffledInput_DoesNotChangeAssignedOrdinals()
    {
        var first = CreateDraft("src/Acme.Orders/Api/OrdersController.cs", new SourceSpan(4, 1, 4, 8));
        var second = CreateDraft("src/Acme.Orders/Program.cs", new SourceSpan(1, 1, 1, 8));
        var drafts = new[] { first, second };

        var forward = OccurrenceOrdinalAssigner.Assign(drafts);
        var reversed = OccurrenceOrdinalAssigner.Assign(drafts.Reverse());

        Assert.Equal(forward.Select(ToOrdinalKey), reversed.Select(ToOrdinalKey));
        Assert.Equal(1, forward.Single(observation => observation.Locator.Equals(first.Locator)).Identity.OccurrenceOrdinal);
        Assert.Equal(2, forward.Single(observation => observation.Locator.Equals(second.Locator)).Identity.OccurrenceOrdinal);
        Assert.Equal(1, reversed.Single(observation => observation.Locator.Equals(first.Locator)).Identity.OccurrenceOrdinal);
        Assert.Equal(2, reversed.Single(observation => observation.Locator.Equals(second.Locator)).Identity.OccurrenceOrdinal);
    }

    private static (string Path, int StartLine, int StartColumn, int EndLine, int EndColumn, int Ordinal) ToOrdinalKey(
        Observation observation) =>
        (
            observation.Locator.RelativePath,
            observation.Locator.Span.StartLine,
            observation.Locator.Span.StartColumn,
            observation.Locator.Span.EndLine,
            observation.Locator.Span.EndColumn,
            observation.Identity.OccurrenceOrdinal);

    private static ObservationDraft CreateDraft(string relativePath, SourceSpan span) =>
        new(
            Solution.Create(SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx")).Reference,
            ObservationKind.Invocation,
            NormalizedPayload.Create([]),
            new EvidenceLocator(DocumentId.Create("doc"), relativePath, span),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("bound", "bound"),
            DocumentHash.Create(new string('a', 64)));
}
