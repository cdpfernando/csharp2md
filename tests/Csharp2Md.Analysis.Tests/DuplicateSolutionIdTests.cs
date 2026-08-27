using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests;

public sealed class DuplicateSolutionIdTests
{
    [Fact]
    [Trait("Requirement", "ROSE-08")]
    public async Task AnalyzeAsync_TwoFoldersSharingAcmeOrdersSlnx_IsRejectedBeforeOpen()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-duplicate-id-");
        try
        {
            var first = CreateNamedSolution(tree.FullName, "left", "Acme.Orders.slnx");
            var second = CreateNamedSolution(tree.FullName, "right", "Acme.Orders.slnx");
            var store = new OpenMustNotBeCalledStore();
            var engine = new AnalysisEngine(store);

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => engine.AnalyzeAsync(AnalysisRequest.Create([first, second]), CancellationToken.None));

            Assert.Contains(first, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(second, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, store.OpenCount);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-08")]
    public async Task AnalyzeAsync_DistinctFilenames_StillProceedsToOpen()
    {
        var store = new CountingStore();
        var engine = new AnalysisEngine(store, StubStages.CreateDefault());

        var result = await engine.AnalyzeAsync(
            AnalysisRequest.Create(["Acme.Orders.slnx", "Acme.Payments.slnx"]),
            CancellationToken.None);

        Assert.Equal(2, result.Solutions.Length);
        Assert.Equal(2, store.OpenCount);
        Assert.All(result.Solutions, outcome => Assert.Equal(PublicationStatus.Committed, outcome.Status));
    }

    private static string CreateNamedSolution(string root, string folder, string fileName)
    {
        var directory = Path.Combine(root, folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "<Solution />");
        return path;
    }

    private sealed class OpenMustNotBeCalledStore : ITransactionalStore
    {
        public int OpenCount { get; private set; }

        public IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader)
        {
            OpenCount++;
            Assert.Fail($"Open must not be called for a duplicate SolutionId, but was called with '{solutionKey}'.");
            return null!;
        }
    }

    private sealed class CountingStore : ITransactionalStore
    {
        private readonly InMemoryTransactionalStore _inner = new();

        public int OpenCount { get; private set; }

        public IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader)
        {
            OpenCount++;
            return _inner.Open(solutionKey, sourceReader);
        }
    }
}
