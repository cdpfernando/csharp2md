using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Storage;
using Microsoft.CodeAnalysis;

namespace Csharp2Md.Analysis.Tests.Semantics;

public sealed class SemanticAnalysisStageTests
{
    [Fact]
    [Trait("Requirement", "ROSE-22")]
    public async Task AnalyzeAsync_OpenThrows_UnpublishedThatSolutionOnlyAndSecondFixtureCommits()
    {
        var failingPath = AcmeOrdersSolutionPath();
        var survivingPath = AcmePaymentsSolutionPath();
        var stages = StubStages.CreateDefault()
            .SetItem(0, new InventoryStage())
            .SetItem(1, new SemanticAnalysisStage(new ThrowingOpenFactory(failingPath, new MsBuildWorkspaceFactory())));
        var engine = new AnalysisEngine(new InMemoryTransactionalStore(), stages);

        var result = await engine.AnalyzeAsync(
            AnalysisRequest.Create([failingPath, survivingPath]),
            CancellationToken.None);

        Assert.Equal(2, result.Solutions.Length);
        Assert.True(result.HasUnpublishedSolution);

        var unpublished = Assert.Single(
            result.Solutions,
            outcome => PathsEqual(outcome.SolutionPath, failingPath));
        Assert.Equal(PublicationStatus.Unpublished, unpublished.Status);
        Assert.False(unpublished.StructuralCorruption);
        Assert.Contains("MSBuildWorkspace could not open", unpublished.Detail, StringComparison.Ordinal);
        Assert.Contains(Path.GetFileName(failingPath), unpublished.Detail, StringComparison.Ordinal);

        var committed = Assert.Single(
            result.Solutions,
            outcome => PathsEqual(outcome.SolutionPath, survivingPath));
        Assert.Equal(PublicationStatus.Committed, committed.Status);
        Assert.False(committed.StructuralCorruption);
        Assert.Null(committed.Detail);
    }

    [Fact]
    [Trait("Requirement", "ROSE-22")]
    public async Task ExecuteAsync_WorkspaceDiagnosticFailureAlone_DoesNotAbort()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var factory = new MsBuildWorkspaceFactory();
        await using (var lease = await factory.Open(solutionPath, "Debug", "net10.0", CancellationToken.None))
        {
            Assert.Contains(lease.Diagnostics, diagnostic => diagnostic.Kind == WorkspaceDiagnosticKind.Failure);
        }

        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
        var stage = new SemanticAnalysisStage(factory);

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.AbortPublication);
        Assert.False(result.StructuralCorruption);
        Assert.Null(context.Detail);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static string AcmeOrdersSolutionPath() => FixtureSolution("Acme.Orders", "Acme.Orders.slnx");

    private static string AcmePaymentsSolutionPath() => FixtureSolution("Acme.Payments", "Acme.Payments.slnx");

    private static string FixtureSolution(string folder, string fileName)
    {
        var path = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            folder,
            fileName);
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    private sealed class ThrowingOpenFactory : IMsBuildWorkspaceFactory
    {
        private readonly string _failingPath;
        private readonly IMsBuildWorkspaceFactory _inner;

        public ThrowingOpenFactory(string failingPath, IMsBuildWorkspaceFactory inner)
        {
            _failingPath = Path.GetFullPath(failingPath);
            _inner = inner;
        }

        public ValueTask<MsBuildWorkspaceLease> Open(
            string solutionPath,
            string configuration,
            string targetFramework,
            CancellationToken cancellationToken)
        {
            if (string.Equals(Path.GetFullPath(solutionPath), _failingPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "MSBuildWorkspace could not open " + Path.GetFileName(solutionPath));
            }

            return _inner.Open(solutionPath, configuration, targetFramework, cancellationToken);
        }
    }
}
