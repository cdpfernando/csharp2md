using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;

namespace Csharp2Md.Analysis.Tests.Inventory;

public sealed class InventoryStageTests
{
    [Fact]
    [Trait("Requirement", "ROSE-11")]
    public async Task ExecuteAsync_AcmeDoesNotExist_RecordsMissingProjectAndSucceeds()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        var stage = new InventoryStage();

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.AbortPublication);
        Assert.False(result.StructuralCorruption);
        var missing = Assert.Single(
            context.Accumulator.ToSnapshot().Diagnostics,
            record => string.Equals(record.Code, "missing-project", StringComparison.Ordinal));
        Assert.Equal("Acme.DoesNotExist/Acme.DoesNotExist.csproj", missing.IdentityOrKey);
        Assert.False(Path.IsPathRooted(missing.IdentityOrKey));
        Assert.Contains("Acme.DoesNotExist", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-11")]
    public async Task ExecuteAsync_MissingListedProject_SetsUnknownsAndDoesNotAbort()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        var stage = new InventoryStage();

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.HasUnknownsOrCandidatesOrFrontiers);
        Assert.False(result.AbortPublication);
        Assert.False(result.StructuralCorruption);
        Assert.Equal("Inventory", stage.Name);
    }

    [Fact]
    [Trait("Requirement", "ROSE-13")]
    public async Task ExecuteAsync_AcmeOrders_EmitsExactlyOneSolutionFactAndNonZeroFactCount()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        var stage = new InventoryStage();

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.FactCount > 0);
        Assert.False(result.AbortPublication);
        Assert.Single(context.Accumulator.ToSnapshot().Facts.OfType<Solution>());
        Assert.Contains(
            context.Accumulator.ToSnapshot().Facts.OfType<Document>(),
            document => string.Equals(document.RelativePath, "Acme.Orders/Api/OrdersController.cs", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "ROSE-13")]
    public async Task ExecuteAsync_AcmeOrders_RecordsDeclaredTargetFrameworkFromCsproj()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);

        await new InventoryStage().ExecuteAsync(context, CancellationToken.None);

        Assert.Contains("net10.0", context.DeclaredTargetFrameworks, StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-13")]
    public async Task ExecuteAsync_TargetFrameworksElement_RecordsEachDeclaredTfm()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-inv-tfms-");
        try
        {
            var projectDir = Path.Combine(tree.FullName, "App");
            Directory.CreateDirectory(projectDir);
            File.WriteAllText(
                Path.Combine(projectDir, "App.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>net10.0;net9.0</TargetFrameworks>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(projectDir, "Program.cs"), "class Program;");
            var solutionPath = Path.Combine(projectDir, "App.slnx");
            File.WriteAllText(solutionPath, """<Solution><Project Path="App.csproj" /></Solution>""");
            var context = new PipelineContext(new SwallowingSession(), solutionPath);

            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);

            Assert.Contains("net10.0", context.DeclaredTargetFrameworks, StringComparer.Ordinal);
            Assert.Contains("net9.0", context.DeclaredTargetFrameworks, StringComparer.Ordinal);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-25")]
    [Trait("Requirement", "CDC-34")]
    public async Task ExecuteAsync_AcmeOrders_PublishesTheAuthorizedRootUsedByThePathGuard()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
        var expectedRoot = AuthorizedRoot.Compute(solutionPath, ExistingAbsolutePaths(solutionPath, listed));
        var context = new PipelineContext(new SwallowingSession(), solutionPath);

        var result = await new InventoryStage().ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.AbortPublication);
        AssertEqualPaths(expectedRoot, context.AuthorizedRoot);
    }

    [Fact]
    [Trait("Requirement", "CDC-25")]
    [Trait("Requirement", "CDC-34")]
    [Trait("Requirement", "GCPC-026")]
    [Trait("Requirement", "GCPC-027")]
    [Trait("Requirement", "GCPC-028")]
    [Trait("Requirement", "GCPC-033")]
    public async Task ExecuteAsync_AcmeOrders_PublishesBothAppsettingsFilesAndCsprojWithNoIndividualUnsupportedDiagnostic()
    {
        // Acme.Orders carries .cs, .csproj and appsettings*.json documents, all accepted by the
        // supported-document policy (GCPC-026, GCPC-027); its own Acme.Orders.slnx solution file also
        // lives in the project directory and is excluded (no classifier consumes a .slnx), so exactly
        // one aggregated diagnostic is published for it (GCPC-033) and no individual unsupported-document
        // diagnostic remains for any document (GCPC-028). fixtures/SyntheticSolution is immutable.
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);

        var result = await new InventoryStage().ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.AbortPublication);
        Assert.True(result.FactCount > 0);
        Assert.Equal(
            [
                "Acme.Orders/appsettings.Development.json",
                "Acme.Orders/appsettings.json",
            ],
            context.ConfigurationDocuments.Select(document => document.RelativePath).ToArray());
        var diagnostics = context.Accumulator.ToSnapshot().Diagnostics;
        Assert.DoesNotContain(
            diagnostics,
            record => string.Equals(record.Code, "unsupported-document", StringComparison.Ordinal)
                && record.IdentityOrKey is not null);
        var aggregated = Assert.Single(
            diagnostics,
            record => string.Equals(record.Code, "unsupported-document", StringComparison.Ordinal));
        Assert.Contains("1 document(s)", aggregated.Message, StringComparison.Ordinal);
        Assert.Contains(".slnx", aggregated.Message, StringComparison.Ordinal);
        Assert.Contains(
            context.Accumulator.ToSnapshot().Facts.OfType<Document>(),
            document => string.Equals(document.RelativePath, "Acme.Orders/Acme.Orders.csproj", StringComparison.Ordinal));
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

    private static IReadOnlyList<string> ExistingAbsolutePaths(string solutionPath, IEnumerable<string> listed)
    {
        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath))
            ?? throw new InvalidOperationException($"'{solutionPath}' has no directory.");
        return listed
            .Select(path => Path.GetFullPath(Path.Combine(solutionDirectory, path)))
            .Where(File.Exists)
            .ToArray();
    }

    private static void AssertEqualPaths(string expected, string actual)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Assert.Equal(Normalize(expected), Normalize(actual), StringComparer.FromComparison(comparison));
    }

    private static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private sealed class SwallowingSession : IStoreSession
    {
        public void Stage(FactualSnapshot snapshot)
        {
        }

        public CommittedPublication Commit() => new("unused", []);

        public void Abort()
        {
        }
    }
}
