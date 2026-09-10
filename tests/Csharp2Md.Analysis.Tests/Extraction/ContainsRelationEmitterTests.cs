using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Observations;
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

    [Fact]
    [Trait("Requirement", "GCPC-039")]
    [Trait("Requirement", "GCPC-044")]
    public async Task ExecuteAsync_AcmeOrders_DocumentContainsSymbol_CitesOnlyTheSymbolsOwnDeclarationEvidence()
    {
        var context = new PipelineContext(new SwallowingSession(), AcmeOrdersSolutionPath());
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
            await new ObservationExtractionStage().ExecuteAsync(context, CancellationToken.None);
            var snapshot = context.Accumulator.ToSnapshot();

            var controllerType = Assert.Single(
                snapshot.Facts.OfType<Symbol>(),
                symbol => symbol.Signature.Value.Contains("metadata=OrdersController", StringComparison.Ordinal)
                    && symbol.Signature.Value.Contains("kind=namedtype", StringComparison.Ordinal));

            var relation = Assert.Single(
                snapshot.ConfirmedRelations,
                candidate => candidate.Kind is RelationKind.Contains && candidate.Target.Equals(controllerType.Reference));

            // GCPC-039/044 (partial): scoped to OrdersController's own declaration evidence (its base
            // type observation) and nothing owned by an unrelated symbol in the same document.
            Assert.NotEmpty(relation.DerivedFrom.DerivedFrom);
            Assert.All(
                relation.DerivedFrom.DerivedFrom,
                identity => Assert.Equal(controllerType.Reference, identity.Owner));
            Assert.Contains(relation.DerivedFrom.DerivedFrom, identity => identity.Kind is ObservationKind.BaseType);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-039")]
    [Trait("Requirement", "GCPC-044")]
    public async Task ExecuteAsync_AcmeOrders_ProjectContainsDocument_CitesDifferentEvidenceThanTheSymbolEdgesBeneathIt()
    {
        var context = new PipelineContext(new SwallowingSession(), AcmeOrdersSolutionPath());
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
            await new ObservationExtractionStage().ExecuteAsync(context, CancellationToken.None);
            var snapshot = context.Accumulator.ToSnapshot();

            var controllerType = Assert.Single(
                snapshot.Facts.OfType<Symbol>(),
                symbol => symbol.Signature.Value.Contains("metadata=OrdersController", StringComparison.Ordinal)
                    && symbol.Signature.Value.Contains("kind=namedtype", StringComparison.Ordinal));
            var document = Assert.Single(
                snapshot.Facts.OfType<Document>(),
                candidate => candidate.RelativePath.EndsWith("OrdersController.cs", StringComparison.Ordinal));

            var documentRelation = Assert.Single(
                snapshot.ConfirmedRelations,
                candidate => candidate.Kind is RelationKind.Contains && candidate.Target.Equals(document.Reference));
            var symbolRelation = Assert.Single(
                snapshot.ConfirmedRelations,
                candidate => candidate.Kind is RelationKind.Contains && candidate.Target.Equals(controllerType.Reference));

            // The project-to-document edge pools declaration evidence across every symbol the document
            // declares (ControllerBase, HttpGetAttribute, IConfiguration, ConfigurationRoot,
            // OrdersController and its members) -- strictly more, and citing owners the narrower
            // symbol-level edge never does.
            Assert.True(
                documentRelation.DerivedFrom.DerivedFrom.Length > symbolRelation.DerivedFrom.DerivedFrom.Length,
                $"Expected the document edge ({documentRelation.DerivedFrom.DerivedFrom.Length}) to cite more evidence than the symbol edge ({symbolRelation.DerivedFrom.DerivedFrom.Length}).");
            Assert.Contains(
                documentRelation.DerivedFrom.DerivedFrom,
                identity => !identity.Owner.Equals(controllerType.Reference));
            Assert.NotEqual(documentRelation.DerivedFrom, symbolRelation.DerivedFrom);
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
