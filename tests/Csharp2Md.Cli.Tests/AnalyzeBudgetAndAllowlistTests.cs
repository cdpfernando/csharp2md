using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Cli.Tests;

/// <summary>
/// GCPC-030, GCPC-036: <c>analyze</c> accepts the document allowlist (T12) and the declared reading
/// budget (T33), and rejects malformed values before any analysis begins.
/// </summary>
public sealed class AnalyzeBudgetAndAllowlistTests
{
    [Fact]
    [Trait("Requirement", "GCPC-030")]
    public async Task Analyze_AllowlistPathOutsideAuthorizedRoot_Exits1AndPublishesNothing()
    {
        // A genuinely escaping entry, matching AnalysisRequestAllowlistTests's own technique: a real file
        // outside the solution's tree, named by its path relative to that tree's root.
        var tree = Directory.CreateTempSubdirectory("csharp2md-cli-allowlist-escape-");
        var outsideDirectory = Directory.CreateTempSubdirectory("csharp2md-cli-allowlist-outside-");
        try
        {
            var projectDir = Path.Combine(tree.FullName, "App");
            Directory.CreateDirectory(projectDir);
            File.WriteAllText(
                Path.Combine(projectDir, "App.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(projectDir, "Program.cs"), "class Program;");
            var solutionPath = Path.Combine(tree.FullName, "App.slnx");
            File.WriteAllText(solutionPath, """<Solution><Project Path="App/App.csproj" /></Solution>""");

            File.WriteAllText(Path.Combine(outsideDirectory.FullName, "secret.txt"), "outside");
            var escapingEntry = Path.GetRelativePath(tree.FullName, Path.Combine(outsideDirectory.FullName, "secret.txt"))
                .Replace('\\', '/');

            var outputPath = CliTestPaths.UniqueOutputPath();
            try
            {
                var (exitCode, _, stderr) = await CliInvoke.RunAsync(
                    ["analyze", "--solution", solutionPath, "--output", outputPath, "--allowlist", escapingEntry]);

                Assert.Equal(1, exitCode);
                Assert.Contains("csharp2md:", stderr, StringComparison.Ordinal);
                Assert.False(Directory.Exists(outputPath), "A rejected allowlist entry must publish nothing.");
            }
            finally
            {
                CliTestPaths.TryDeleteDirectory(outputPath);
            }
        }
        finally
        {
            outsideDirectory.Delete(recursive: true);
            tree.Delete(recursive: true);
        }
    }

    [Theory]
    [Trait("Requirement", "GCPC-036")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task Analyze_NonPositiveReadingBudgetTokens_Exits1AndPublishesNothing(string budget)
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        var outputPath = CliTestPaths.UniqueOutputPath();
        try
        {
            var (exitCode, _, stderr) = await CliInvoke.RunAsync(
                ["analyze", "--solution", solutionPath, "--output", outputPath, "--reading-budget-tokens", budget]);

            Assert.Equal(1, exitCode);
            Assert.Contains("csharp2md:", stderr, StringComparison.Ordinal);
            Assert.False(Directory.Exists(outputPath), "A rejected reading budget must publish nothing.");
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Theory]
    [Trait("Requirement", "GCPC-036")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task Analyze_NonPositiveMaxFileReadsPerScenario_Exits1AndPublishesNothing(string maxReads)
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        var outputPath = CliTestPaths.UniqueOutputPath();
        try
        {
            var (exitCode, _, stderr) = await CliInvoke.RunAsync(
                ["analyze", "--solution", solutionPath, "--output", outputPath, "--max-file-reads-per-scenario", maxReads]);

            Assert.Equal(1, exitCode);
            Assert.Contains("csharp2md:", stderr, StringComparison.Ordinal);
            Assert.False(Directory.Exists(outputPath), "A rejected max-file-reads value must publish nothing.");
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-036")]
    [Trait("Requirement", "GCPC-037")]
    public async Task Analyze_SuppliedReadingBudget_ReachesThePublishedProvenance()
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        var outputPath = CliTestPaths.UniqueOutputPath();
        try
        {
            const int suppliedTokens = 50_000;
            const int suppliedMaxReads = 10;
            var (exitCode, _, stderr) = await CliInvoke.RunAsync(
                [
                    "analyze",
                    "--solution", solutionPath,
                    "--output", outputPath,
                    "--reading-budget-tokens", suppliedTokens.ToString(),
                    "--max-file-reads-per-scenario", suppliedMaxReads.ToString(),
                ]);
            Assert.True(exitCode is 0 or 2 or 3 or 4, $"analyze did not run to completion: exit {exitCode}, {stderr}");

            var packageDirectory = Directory.GetDirectories(outputPath)
                .Single(static path => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), "^s-[0-9a-f]{32}$"));
            var manifest = CanonicalJson.Read<ManifestEnvelope>(
                File.ReadAllBytes(Path.Combine(packageDirectory, "manifest.json")));

            Assert.NotNull(manifest.Provenance);
            var expectedCeilingBytes = Storage.Mapping.CeilingCalculator
                .Derive(suppliedTokens, suppliedMaxReads)
                .CeilingBytes;
            Assert.Equal(expectedCeilingBytes, manifest.Provenance!.ArtifactCeilingBytes);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-030")]
    public async Task Analyze_AllowlistedDocument_IsInventoried()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-cli-allowlist-");
        try
        {
            var projectDir = Path.Combine(tree.FullName, "App");
            Directory.CreateDirectory(projectDir);
            File.WriteAllText(
                Path.Combine(projectDir, "App.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(projectDir, "Program.cs"), "class Program;");
            File.WriteAllText(Path.Combine(projectDir, "app.ts"), "export const x = 1;");
            var solutionPath = Path.Combine(tree.FullName, "App.slnx");
            File.WriteAllText(solutionPath, """<Solution><Project Path="App/App.csproj" /></Solution>""");

            var outputPath = CliTestPaths.UniqueOutputPath();
            try
            {
                var (exitCode, _, stderr) = await CliInvoke.RunAsync(
                    ["analyze", "--solution", solutionPath, "--output", outputPath, "--allowlist", "App/app.ts"]);
                Assert.True(exitCode is 0 or 2 or 3 or 4, $"analyze did not run to completion: exit {exitCode}, {stderr}");

                var packageDirectory = Directory.GetDirectories(outputPath)
                    .Single(static path => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), "^s-[0-9a-f]{32}$"));
                var structural = CanonicalJson.Read<StructuralFactsShard>(
                    File.ReadAllBytes(Path.Combine(packageDirectory, "facts", "structural.json")));
                Assert.Contains(structural.Documents, document => document.RelativePath == "App/app.ts");
            }
            finally
            {
                CliTestPaths.TryDeleteDirectory(outputPath);
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }
}
