using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class DocumentOrderIndependenceTests
{
    [Fact]
    [Trait("Requirement", "ROSE-54")]
    public async Task Assign_ShuffledDocumentGroups_KeepsStructuralAndObservationIdentities()
    {
        var context = new PipelineContext(new SwallowingSession(), AcmeOrdersSolutionPath());
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);

            var factIds = StructuralFactIds(context.Accumulator);
            Assert.NotEmpty(factIds);

            var drafts = AlwaysWhenBindableWalker.Collect(context, CancellationToken.None).ToArray();
            Assert.NotEmpty(drafts);

            var byDocument = drafts
                .GroupBy(draft => draft.Locator.RelativePath, StringComparer.Ordinal)
                .ToArray();
            Assert.True(byDocument.Length >= 2, $"Expected at least two documents to shuffle, found {byDocument.Length}.");

            var forward = byDocument.SelectMany(group => group).ToArray();
            var shuffled = byDocument.Reverse().SelectMany(group => group).ToArray();
            Assert.NotEqual(forward[0].Locator.RelativePath, shuffled[0].Locator.RelativePath);

            var sink = new SnapshotAccumulator();
            var forwardRedacted = forward.Select(draft => ObservationMaterializer.Redact(draft, sink)).ToArray();
            var shuffledRedacted = shuffled.Select(draft => ObservationMaterializer.Redact(draft, sink)).ToArray();

            var forwardIds = SortedIdentities(OccurrenceOrdinalAssigner.Assign(forwardRedacted));
            var shuffledIds = SortedIdentities(OccurrenceOrdinalAssigner.Assign(shuffledRedacted));
            Assert.NotEmpty(forwardIds);
            Assert.Equal(forwardIds, shuffledIds);

            context.CSharpDocuments = context.CSharpDocuments.Reverse().ToImmutableArray();
            Assert.Equal(factIds, StructuralFactIds(context.Accumulator));
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    private static string AcmeOrdersSolutionPath()
    {
        var path = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    private static string[] StructuralFactIds(SnapshotAccumulator accumulator) =>
        accumulator.ToSnapshot().Facts
            .Select(fact => fact.Reference.Id.Value)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    private static ObservationIdentity[] SortedIdentities(IEnumerable<Observation> observations) =>
        observations.Select(observation => observation.Identity)
            .OrderBy(identity => identity.Owner.Id.Value, StringComparer.Ordinal)
            .ThenBy(identity => identity.Kind.ToString(), StringComparer.Ordinal)
            .ThenBy(identity => identity.OccurrenceOrdinal)
            .ToArray();
}
