using System.Reflection;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class MultiSolutionIsolationTests
{
    [Fact]
    [Trait("Requirement", "ENG-32")]
    [Trait("Requirement", "ENG-33")]
    [Trait("Requirement", "ENG-35")]
    [Trait("Requirement", "ENG-36")]
    public async Task AnalyzeAsync_ThreeSolutions_IsolatesAFailureToTheMiddleOutcome()
    {
        var contexts = new List<PipelineContext>();
        var stages = StubStages.CreateDefault()
            .SetItem(0, new RecordingStage("Inventory", [], contexts.Add))
            .SetItem(3, new RecordingStage("Classification and Promotion", [], context =>
            {
                if (context.SolutionPath == "b.sln")
                {
                    throw new InvalidOperationException("forced isolation failure");
                }
            }));
        var store = new InMemoryTransactionalStore();
        var engine = new AnalysisEngine(store, stages);

        var result = await engine.AnalyzeAsync(
            AnalysisRequest.Create(["a.sln", "b.sln", "c.sln"]),
            CancellationToken.None);

        Assert.Equal(3, result.Solutions.Length);
        Assert.True(result.HasUnpublishedSolution);

        Assert.Equal(PublicationStatus.Committed, result.Solutions[0].Status);
        Assert.Null(result.Solutions[0].FailingStage);
        Assert.False(result.Solutions[0].StructuralCorruption);
        Assert.Equal(Path.GetFullPath("a.sln"), result.Solutions[0].SolutionPath);
        Assert.True(store.TryGetPublication(result.Solutions[0].SolutionPath, out var publicationA));
        Assert.Equal(ArtifactRole.Manifest, Assert.Single(publicationA.ArtifactsInPublicationOrder).Role);

        Assert.Equal(PublicationStatus.Unpublished, result.Solutions[1].Status);
        Assert.Equal("Classification and Promotion", result.Solutions[1].FailingStage);
        Assert.False(result.Solutions[1].StructuralCorruption);
        Assert.Equal(Path.GetFullPath("b.sln"), result.Solutions[1].SolutionPath);
        Assert.False(store.TryGetPublication(result.Solutions[1].SolutionPath, out _));

        Assert.Equal(PublicationStatus.Committed, result.Solutions[2].Status);
        Assert.Null(result.Solutions[2].FailingStage);
        Assert.False(result.Solutions[2].StructuralCorruption);
        Assert.Equal(Path.GetFullPath("c.sln"), result.Solutions[2].SolutionPath);
        Assert.True(store.TryGetPublication(result.Solutions[2].SolutionPath, out var publicationC));
        Assert.Equal(ArtifactRole.Manifest, Assert.Single(publicationC.ArtifactsInPublicationOrder).Role);
        Assert.NotEqual(publicationA.SolutionKey, publicationC.SolutionKey);

        Assert.Equal(3, contexts.Count);
        Assert.NotSame(contexts[0], contexts[1]);
        Assert.NotSame(contexts[1], contexts[2]);
        Assert.NotSame(contexts[0], contexts[2]);
        Assert.Equal("a.sln", contexts[0].SolutionPath);
        Assert.Equal("b.sln", contexts[1].SolutionPath);
        Assert.Equal("c.sln", contexts[2].SolutionPath);

        var compilationFields = typeof(AnalysisEngine)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => IsCompilation(field.FieldType) || IsCompilation(field.GetValue(engine)?.GetType()))
            .Select(field => field.Name)
            .ToArray();
        Assert.True(
            compilationFields.Length == 0,
            "AnalysisEngine must not hold a Microsoft.CodeAnalysis.Compilation instance field, but found: "
            + string.Join(", ", compilationFields));
    }

    private static bool IsCompilation(Type? type) =>
        type?.FullName is string name
        && (name == "Microsoft.CodeAnalysis.Compilation"
            || name.StartsWith("Microsoft.CodeAnalysis.Compilation`", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.CodeAnalysis.Compilation+", StringComparison.Ordinal));
}
