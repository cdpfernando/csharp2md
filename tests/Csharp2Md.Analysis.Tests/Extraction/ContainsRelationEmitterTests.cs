using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class ContainsRelationEmitterTests
{
    [Fact]
    [Trait("Requirement", "ROSE-17")]
    [Trait("Requirement", "ROSE-18")]
    [Trait("Requirement", "ROSE-19")]
    [Trait("Requirement", "ROSE-20")]
    public async Task ExecuteAsync_AcmeOrders_EmitsOnlyInventoryContainsRelations()
    {
        var context = new PipelineContext(new SwallowingSession(), AcmeOrdersSolutionPath());
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
            var result = await new ObservationExtractionStage().ExecuteAsync(context, CancellationToken.None);
            var snapshot = context.Accumulator.ToSnapshot();
            var relations = snapshot.ConfirmedRelations.ToArray();

            Assert.True(result.RelationCount > 0, $"Observation Extraction relation count was {result.RelationCount}.");
            Assert.Equal(relations.Length, result.RelationCount);
            Assert.Contains(
                relations,
                relation => relation.Kind is RelationKind.Contains
                    && relation.Source.FactType == "Project"
                    && relation.Target.FactType == "Document");
            Assert.Contains(
                relations,
                relation => relation.Kind is RelationKind.Contains
                    && relation.Source.FactType == "Document"
                    && relation.Target.FactType == "Symbol");
            Assert.DoesNotContain(
                relations,
                relation => relation.Kind is RelationKind.Contains
                    && relation.Source.FactType == "Solution"
                    && relation.Target.FactType == "Project");
            Assert.DoesNotContain(
                relations,
                relation => relation.Kind is not RelationKind.Contains);
            Assert.All(relations, relation => Assert.Equal(RelationKind.Contains, relation.Kind));
            Assert.All(
                relations,
                relation =>
                {
                    Assert.Equal("csharp2md.inventory.contains", relation.Classifier.Id);
                    Assert.Equal(1, relation.Classifier.Version);
                    Assert.NotEmpty(relation.DerivedFrom.DerivedFrom);
                    Assert.Empty(relation.Facets.Entries);
                });
            Assert.Equal(EvidenceMethod.Syntactic, ContainsMinimumEvidence);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    private static EvidenceMethod ContainsMinimumEvidence =>
        Csharp2Md.Domain.Registry.TaxonomyTables.Default.Relations
            .Single(relation => relation.Kind == RelationKind.Contains)
            .MinimumEvidenceMethod;

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
}
