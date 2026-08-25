using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PipelineStageContractTests
{
    private static readonly string[] DeclaredStageNames =
    [
        "Inventory",
        "Semantic Analysis",
        "Observation Extraction",
        "Classification and Promotion",
        "Validation and Coverage",
        "Persistence",
        "Retrieval Projection",
        "Batch Composition",
    ];

    [Fact]
    [Trait("Requirement", "ENG-13")]
    public void DefaultStubs_UseTheEightArchitectureNamesInDeclaredOrder()
    {
        var names = StubStages.CreateDefault().Select(stage => stage.Name).ToArray();

        Assert.Equal(DeclaredStageNames, names);
    }

    [Fact]
    [Trait("Requirement", "ENG-15")]
    public async Task DefaultStubs_ReturnZeroCountsAndNoCorruption()
    {
        var context = new PipelineContext(new SwallowingSession(), "unused.sln");

        foreach (var stage in StubStages.CreateDefault())
        {
            var result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.Equal(0, result.FactCount);
            Assert.Equal(0, result.ObservationCount);
            Assert.Equal(0, result.RelationCount);
            Assert.False(result.StructuralCorruption);
            Assert.False(result.HasUnknownsOrCandidatesOrFrontiers);
        }
    }

    [Fact]
    [Trait("Requirement", "ENG-12")]
    public void PublicSurface_DoesNotExposeIPipelineStageOrStubClasses()
    {
        var exportedNames = typeof(AssemblyMarker).Assembly
            .GetExportedTypes()
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("IPipelineStage", exportedNames);
        Assert.DoesNotContain("InventoryStub", exportedNames);
        Assert.DoesNotContain("SemanticAnalysisStub", exportedNames);
        Assert.DoesNotContain("ObservationExtractionStub", exportedNames);
        Assert.DoesNotContain("ClassificationAndPromotionStub", exportedNames);
        Assert.DoesNotContain("ValidationAndCoverageStub", exportedNames);
        Assert.DoesNotContain("PersistenceStub", exportedNames);
        Assert.DoesNotContain("RetrievalProjectionStub", exportedNames);
        Assert.DoesNotContain("BatchCompositionStub", exportedNames);
    }

    private sealed class SwallowingSession : IStoreSession
    {
        public void Stage(StagedFragment fragment)
        {
        }

        public CommittedPublication Commit() => new("unused", []);

        public void Abort()
        {
        }
    }
}
