using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Cli.Tests;

/// <summary>
/// GCPC-068: <c>compose</c> rebuilds contributions from already-published packages and produces the same
/// batch artifacts a live <c>analyze</c> batch did, with no solution present.
/// </summary>
public sealed class ComposeCommandTests
{
    [Fact]
    [Trait("Requirement", "GCPC-068")]
    public async Task Compose_WithNoSolutionPresent_ReproducesTheAnalyzeBatchArtifactsByteForByte()
    {
        var solutionRoot = CopyFixtureToTemp();
        var outputPath = CliTestPaths.UniqueOutputPath();
        try
        {
            var firstSolution = Path.Combine(solutionRoot, "Acme.Orders", "Acme.Orders.slnx");
            var secondSolution = Path.Combine(solutionRoot, "Acme.Payments", "Acme.Payments.slnx");

            var (analyzeExit, _, analyzeStderr) = await CliInvoke.RunAsync(
                ["analyze", "--solution", firstSolution, "--solution", secondSolution, "--output", outputPath]);
            Assert.True(analyzeExit == ExitCodes.Degraded, $"analyze failed with exit {analyzeExit}: {analyzeStderr}");

            var originalBatchArtifacts = ReadBatchArtifacts(outputPath);
            Assert.True(originalBatchArtifacts.ContainsKey("batch-manifest.json"));

            var packageDirectories = Directory.GetDirectories(outputPath)
                .Where(static path => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), "^s-[0-9a-f]{32}$"))
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(2, packageDirectories.Length);
            Assert.Contains(
                packageDirectories,
                static package => PublishedManifestTestFile.ReadRoot(package).SolutionFileName == "Acme.Orders.slnx"
                    && PublishedManifestTestFile.HasParts(package));

            // No solution present: the temp copy compose could otherwise have opened is gone entirely.
            Directory.Delete(solutionRoot, recursive: true);

            var composeArgs = new List<string>();
            foreach (var packageDirectory in packageDirectories)
            {
                composeArgs.Add("--package");
                composeArgs.Add(packageDirectory);
            }

            composeArgs.Add("--output");
            composeArgs.Add(outputPath);

            var (composeExit, _, composeStderr) = await CliInvoke.RunAsync(["compose", .. composeArgs]);
            Assert.True(composeExit == 0, $"compose failed with exit {composeExit}: {composeStderr}");

            var recomposedBatchArtifacts = ReadBatchArtifacts(outputPath);
            Assert.Equal(
                originalBatchArtifacts.Keys.OrderBy(static key => key, StringComparer.Ordinal),
                recomposedBatchArtifacts.Keys.OrderBy(static key => key, StringComparer.Ordinal));
            foreach (var key in originalBatchArtifacts.Keys)
            {
                Assert.True(
                    originalBatchArtifacts[key].AsSpan().SequenceEqual(recomposedBatchArtifacts[key]),
                    $"'{key}' differs between the analyze batch and the recomposed batch.");
            }
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
            CliTestPaths.TryDeleteDirectory(solutionRoot);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-068")]
    [Trait("Requirement", "GCPC-072")]
    [Trait("Requirement", "GCPC-114")]
    public async Task Compose_PackageMissingFromTheOutputRoot_YieldsIncompleteScopeAndLeavesTheCommittedPackageUntouched()
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        var outputPath = CliTestPaths.UniqueOutputPath();
        try
        {
            var (analyzeExit, _, _) = await CliInvoke.RunAsync(
                ["analyze", "--solution", solutionPath, "--output", outputPath]);
            Assert.Equal(ExitCodes.Degraded, analyzeExit);

            var packageDirectory = SinglePackageDirectory(outputPath);
            var manifestBefore = File.ReadAllBytes(Path.Combine(packageDirectory, "manifest.json"));

            var missingPackage = Path.Combine(outputPath, "s-00000000000000000000000000000000");
            var (composeExit, _, _) = await CliInvoke.RunAsync(
                ["compose", "--package", packageDirectory, "--package", missingPackage, "--output", outputPath]);

            Assert.Equal(ExitCodes.PartialComposition, composeExit);

            var batchManifest = CanonicalJson.Read<BatchManifestEnvelope>(
                File.ReadAllBytes(Path.Combine(outputPath, "batch-manifest.json")));
            Assert.False(batchManifest.Complete);
            Assert.False(string.IsNullOrWhiteSpace(batchManifest.IncompleteScopeReason));

            var manifestAfter = File.ReadAllBytes(Path.Combine(packageDirectory, "manifest.json"));
            Assert.True(manifestBefore.AsSpan().SequenceEqual(manifestAfter));
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-068")]
    public void Compose_ActionBody_NeverReferencesAnalysisEngineOrSolutionLoading()
    {
        var source = File.ReadAllText(
            Path.Combine(CliTestPaths.RepoRoot, "src", "Csharp2Md.Cli", "CommandFactory.cs"));
        var composeStart = source.IndexOf(
            "private static Func<ParseResult, CancellationToken, Task<int>> ComposeAction(", StringComparison.Ordinal);
        var validateActionStart = source.IndexOf(
            "ValidateAction(Option<string> packageOption)", composeStart, StringComparison.Ordinal);
        Assert.True(composeStart >= 0 && validateActionStart > composeStart);
        var composeBody = source[composeStart..validateActionStart];

        Assert.DoesNotContain("AnalysisEngine", composeBody, StringComparison.Ordinal);
        Assert.DoesNotContain("IAnalysisEngine", composeBody, StringComparison.Ordinal);
        Assert.DoesNotContain("AnalysisRequest", composeBody, StringComparison.Ordinal);
    }

    private static Dictionary<string, byte[]> ReadBatchArtifacts(string outputPath)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var manifestPath = Path.Combine(outputPath, "batch-manifest.json");
        if (File.Exists(manifestPath))
        {
            files["batch-manifest.json"] = File.ReadAllBytes(manifestPath);
        }

        var compositionPath = Path.Combine(outputPath, "composition");
        if (Directory.Exists(compositionPath))
        {
            foreach (var file in Directory.EnumerateFiles(compositionPath, "*", SearchOption.AllDirectories))
            {
                var relative = "composition/" + Path.GetRelativePath(compositionPath, file).Replace('\\', '/');
                files[relative] = File.ReadAllBytes(file);
            }
        }

        return files;
    }

    private static string CopyFixtureToTemp()
    {
        var source = Path.Combine(CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
        var destination = Path.Combine(Path.GetTempPath(), "csharp2md-compose-fixture-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(source, destination);
        return destination;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(IsBuildOutputSegment))
            {
                continue;
            }

            var destinationFile = Path.Combine(destination, relative);
            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(file, destinationFile);
        }
    }

    private static bool IsBuildOutputSegment(string segment) =>
        segment is "bin" or "obj";

    /// <summary>The one package directory ("s-&lt;hash&gt;") under an output root that may also hold a
    /// top-level composition/ directory (GCPC-068 wired a BatchComposer into the real analyze store).</summary>
    private static string SinglePackageDirectory(string outputPath) =>
        Directory.GetDirectories(outputPath)
            .Single(static path => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), "^s-[0-9a-f]{32}$"));
}
