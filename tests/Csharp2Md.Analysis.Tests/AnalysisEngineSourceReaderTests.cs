using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests;

public sealed class AnalysisEngineSourceReaderTests
{
    [Fact]
    [Trait("Requirement", "RP-07")]
    public async Task AnalyzeAsync_TwoSolutions_EachOpenReceivesADistinctReader()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-engine-reader-distinct-");
        try
        {
            var first = CreateSolution(tree.FullName, "Alpha", "UniqueAlpha.cs", "class UniqueAlpha;");
            var second = CreateSolution(tree.FullName, "Beta", "UniqueBeta.cs", "class UniqueBeta;");
            var store = new CapturingStore();
            var engine = new AnalysisEngine(store, InventoryOnly());

            var result = await engine.AnalyzeAsync(AnalysisRequest.Create([first, second]), CancellationToken.None);

            Assert.Equal(2, result.Solutions.Length);
            Assert.Equal(2, store.Readers.Count);
            Assert.NotSame(store.Readers[0], store.Readers[1]);
            Assert.All(result.Solutions, outcome => Assert.Equal(PublicationStatus.Committed, outcome.Status));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    public async Task AnalyzeAsync_TwoSolutions_ReaderDocumentsAreDisjoint()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-engine-reader-disjoint-");
        try
        {
            var (firstReader, secondReader) = await OpenTwoSolutionsAsync(tree.FullName);

            Assert.NotEmpty(firstReader.Documents);
            Assert.NotEmpty(secondReader.Documents);
            Assert.Empty(firstReader.Documents.Intersect(secondReader.Documents));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    public async Task AnalyzeAsync_TwoSolutions_TryReadDoesNotExposeTheOtherSolutionsDocuments()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-engine-reader-isolate-");
        try
        {
            var (firstReader, secondReader) = await OpenTwoSolutionsAsync(tree.FullName);

            foreach (var id in secondReader.Documents)
            {
                Assert.False(firstReader.TryRead(id, out var bytes));
                Assert.True(bytes.IsDefaultOrEmpty);
            }

            foreach (var id in firstReader.Documents)
            {
                Assert.False(secondReader.TryRead(id, out var bytes));
                Assert.True(bytes.IsDefaultOrEmpty);
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    public async Task AnalyzeAsync_SingleSolution_ReaderCoversOnlyThatSolutionsDocuments()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-engine-reader-single-");
        try
        {
            var path = CreateSolution(tree.FullName, "Solo", "SoloOnly.cs", "class SoloOnly;");
            var store = new CapturingStore();
            var engine = new AnalysisEngine(store, InventoryOnly());

            await engine.AnalyzeAsync(AnalysisRequest.Create([path]), CancellationToken.None);

            var reader = Assert.Single(store.Readers);
            Assert.Contains(
                reader.Documents,
                id => id.Value.Contains("SoloOnly.cs", StringComparison.Ordinal));
            Assert.DoesNotContain(
                reader.Documents,
                id => id.Value.Contains("UniqueAlpha.cs", StringComparison.Ordinal)
                    || id.Value.Contains("UniqueBeta.cs", StringComparison.Ordinal));
            var solo = reader.Documents.First(id => id.Value.Contains("SoloOnly.cs", StringComparison.Ordinal));
            Assert.True(reader.TryRead(solo, out var bytes));
            Assert.False(bytes.IsDefaultOrEmpty);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static async Task<(ISourceDocumentReader First, ISourceDocumentReader Second)> OpenTwoSolutionsAsync(string root)
    {
        var first = CreateSolution(root, "Alpha", "UniqueAlpha.cs", "class UniqueAlpha;");
        var second = CreateSolution(root, "Beta", "UniqueBeta.cs", "class UniqueBeta;");
        var store = new CapturingStore();
        var engine = new AnalysisEngine(store, InventoryOnly());

        await engine.AnalyzeAsync(AnalysisRequest.Create([first, second]), CancellationToken.None);

        Assert.Equal(2, store.Readers.Count);
        return (store.Readers[0], store.Readers[1]);
    }

    private static ImmutableArray<IPipelineStage> InventoryOnly() =>
        StubStages.CreateDefault().SetItem(0, new InventoryStage());

    private static string CreateSolution(string root, string name, string sourceFileName, string sourceText)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, name + ".csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, sourceFileName), sourceText);
        var solutionPath = Path.Combine(directory, name + ".slnx");
        File.WriteAllText(solutionPath, $"""<Solution><Project Path="{name}.csproj" /></Solution>""");
        return solutionPath;
    }

    private sealed class CapturingStore : ITransactionalStore
    {
        private readonly InMemoryTransactionalStore _inner = new();

        public List<ISourceDocumentReader> Readers { get; } = [];

        public IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader)
        {
            Readers.Add(sourceReader);
            return _inner.Open(solutionKey, sourceReader);
        }
    }
}
