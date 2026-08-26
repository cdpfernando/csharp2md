using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class ObservationIdentityUnionTests
{
    [Fact]
    [Trait("Requirement", "ROSE-44")]
    public void ExtractPath_SpanOnlyPair_CollapsesToOneIdentity()
    {
        var first = CreateDraft(new SourceSpan(1, 1, 1, 8), new BindingDiagnostic("unbound", "unbound"));
        var second = CreateDraft(new SourceSpan(40, 2, 40, 20), new BindingDiagnostic("unbound", "unbound"));
        var accumulator = new SnapshotAccumulator();

        AddAssigned(accumulator, first);
        AddAssigned(accumulator, second);

        var observations = accumulator.ToSnapshot().Observations.ToArray();
        Assert.Equal(first.Locator.Span, Assert.Single(OccurrenceOrdinalAssigner.Assign([first])).Locator.Span);
        Assert.NotEqual(first.Locator.Span, second.Locator.Span);
        Assert.Equal(
            Assert.Single(OccurrenceOrdinalAssigner.Assign([first])).Identity,
            Assert.Single(OccurrenceOrdinalAssigner.Assign([second])).Identity);
        Assert.Single(observations);
        Assert.Equal(observations[0].Identity.Owner, first.Owner);
        Assert.Equal(ObservationKind.Invocation, observations[0].Identity.Kind);
        Assert.Equal(1, observations[0].Identity.OccurrenceOrdinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-44")]
    public async Task ExtractPath_ShuffledDocumentOrder_ProducesTheSameIdentitySet()
    {
        var drafts = await CollectAcmeOrdersAsync();
        Assert.NotEmpty(drafts);

        var byDocument = drafts
            .GroupBy(draft => draft.Locator.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var forwardOrder = byDocument
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .SelectMany(group => group)
            .ToArray();
        var reverseOrder = byDocument
            .OrderByDescending(group => group.Key, StringComparer.Ordinal)
            .SelectMany(group => group)
            .ToArray();

        var forward = OccurrenceOrdinalAssigner.Assign(forwardOrder);
        var reversed = OccurrenceOrdinalAssigner.Assign(reverseOrder);

        Assert.Equal(
            forward.Select(static observation => observation.Identity).ToHashSet(),
            reversed.Select(static observation => observation.Identity).ToHashSet());
    }

    [Fact]
    [Trait("Requirement", "ROSE-44")]
    public void ExtractPath_MultiTfmBoundAndUnbound_KeepsBound()
    {
        var unbound = CreateDraft(new SourceSpan(1, 1, 1, 8), new BindingDiagnostic("unbound", "unbound"));
        var bound = CreateDraft(new SourceSpan(2, 1, 2, 8), new BindingDiagnostic("bound", "bound"));
        var unboundFirst = new SnapshotAccumulator();
        var boundFirst = new SnapshotAccumulator();

        AddAssigned(unboundFirst, unbound);
        AddAssigned(unboundFirst, bound);
        AddAssigned(boundFirst, bound);
        AddAssigned(boundFirst, unbound);

        Assert.Equal("bound", Assert.Single(unboundFirst.ToSnapshot().Observations.ToArray()).Diagnostic.Code);
        Assert.Equal("bound", Assert.Single(boundFirst.ToSnapshot().Observations.ToArray()).Diagnostic.Code);
        Assert.Equal(EvidenceMethod.Semantic, Assert.Single(boundFirst.ToSnapshot().Observations.ToArray()).ExtractionMethod);
    }

    private static void AddAssigned(SnapshotAccumulator accumulator, ObservationDraft draft)
    {
        foreach (var observation in OccurrenceOrdinalAssigner.Assign([draft]))
        {
            accumulator.AddObservation(observation);
        }
    }

    private static ObservationDraft CreateDraft(SourceSpan span, BindingDiagnostic diagnostic) =>
        new(
            Solution.Create(SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx")).Reference,
            ObservationKind.Invocation,
            NormalizedPayload.Create([]),
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Orders/Program.cs", span),
            string.Equals(diagnostic.Code, "bound", StringComparison.Ordinal)
                ? EvidenceMethod.Semantic
                : EvidenceMethod.Syntactic,
            diagnostic,
            DocumentHash.Create(new string('a', 64)));

    private static async Task<ObservationDraft[]> CollectAcmeOrdersAsync()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
            return [.. AlwaysWhenBindableWalker.Collect(context, CancellationToken.None)];
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }
}
