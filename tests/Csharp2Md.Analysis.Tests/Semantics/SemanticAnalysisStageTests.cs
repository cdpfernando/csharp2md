using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Storage;
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

    [Fact]
    [Trait("Requirement", "ROSE-12")]
    public async Task ExecuteAsync_UnresolvableSdk_RecordsDiagnosticNamesProjectAndCompilesLoadableProjects()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        await new InventoryStage().ExecuteAsync(context, CancellationToken.None);

        var result = await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.AbortPublication);
        Assert.False(result.StructuralCorruption);
        Assert.True(result.HasUnknownsOrCandidatesOrFrontiers);

        var sdk = Assert.Single(
            context.Accumulator.ToSnapshot().Diagnostics,
            record => string.Equals(record.Code, "unresolvable-sdk", StringComparison.Ordinal));
        Assert.Equal("Acme.Broken/Acme.Broken.csproj", sdk.IdentityOrKey);
        Assert.False(Path.IsPathRooted(sdk.IdentityOrKey));
        Assert.Contains("Acme.Broken", sdk.Message, StringComparison.Ordinal);

        Assert.Contains(
            context.Compilations,
            compilation => compilation.SyntaxTrees.Any(tree =>
                tree.FilePath.Contains("OrdersController.cs", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    [Trait("Requirement", "ROSE-23")]
    public async Task ExecuteAsync_CompileErrorProject_RecordsCompilationErrorAndKeepsLoadableCompilation()
    {
        // Acme.Broken never yields compile diagnostics because its SDK never loads.
        // This temp tree is the documented stand-in: one healthy project and one C# error.
        var tree = Directory.CreateTempSubdirectory("csharp2md-cserror-");
        try
        {
            var solutionPath = WriteCompileErrorSolution(tree.FullName);
            var context = new PipelineContext(new SwallowingSession(), solutionPath);
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);

            var result = await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.AbortPublication);
            Assert.False(result.StructuralCorruption);
            Assert.True(result.HasUnknownsOrCandidatesOrFrontiers);

            var compileError = Assert.Single(
                context.Accumulator.ToSnapshot().Diagnostics,
                record => string.Equals(record.Code, "compilation-error", StringComparison.Ordinal));
            Assert.Equal("Broken/Broken.csproj", compileError.IdentityOrKey);
            Assert.False(Path.IsPathRooted(compileError.IdentityOrKey));
            Assert.Contains("Broken/Broken.csproj", compileError.Message, StringComparison.Ordinal);

            Assert.Contains(
                context.Compilations,
                compilation => compilation.SyntaxTrees.Any(tree =>
                    tree.FilePath.Contains("Good.cs", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains(
                context.Compilations,
                compilation => compilation.SyntaxTrees.Any(tree =>
                    tree.FilePath.Contains("Broken.cs", StringComparison.OrdinalIgnoreCase)));
            Assert.Empty(context.Accumulator.ToSnapshot().Facts.OfType<Csharp2Md.Domain.Facts.Symbol>());
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-23")]
    public async Task AnalyzeAsync_AcmeOrdersWithSemanticStage_StillCommits()
    {
        var stages = StubStages.CreateDefault()
            .SetItem(0, new InventoryStage())
            .SetItem(1, new SemanticAnalysisStage());
        var engine = new AnalysisEngine(new InMemoryTransactionalStore(), stages);

        var result = await engine.AnalyzeAsync(
            AnalysisRequest.Create([AcmeOrdersSolutionPath()]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.False(outcome.StructuralCorruption);
        Assert.True(outcome.HasUnknownsOrCandidatesOrFrontiers);
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

    private static string WriteCompileErrorSolution(string root)
    {
        var goodDir = Path.Combine(root, "Good");
        var brokenDir = Path.Combine(root, "Broken");
        Directory.CreateDirectory(goodDir);
        Directory.CreateDirectory(brokenDir);

        const string sdkProject = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;
        File.WriteAllText(Path.Combine(goodDir, "Good.csproj"), sdkProject);
        File.WriteAllText(Path.Combine(goodDir, "Good.cs"), "class Good;");
        File.WriteAllText(Path.Combine(brokenDir, "Broken.csproj"), sdkProject);
        File.WriteAllText(Path.Combine(brokenDir, "Broken.cs"), """class Broken { int M() => "compile-error"; }""");

        var solutionPath = Path.Combine(root, "App.slnx");
        File.WriteAllText(
            solutionPath,
            """
            <Solution>
              <Project Path="Good/Good.csproj" />
              <Project Path="Broken/Broken.csproj" />
            </Solution>
            """);
        return solutionPath;
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
