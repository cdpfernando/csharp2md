using System.Xml.Linq;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Cli.Tests;

/// <summary>
/// GCPC-063, GCPC-064, GCPC-066, GCPC-067: <c>validate</c> re-hydrates an already-published package and
/// re-runs the publication validators, touching no solution -- and, as the closure of the deferred
/// `RetrievalScenarioRunner` wiring gap (context.md, found at T47), a real `analyze` run now folds the
/// documented retrieval scenarios' measured records into `measurements.json`, and `validate` walks and
/// reports the same scenarios directly from the package on disk.
/// </summary>
public sealed class ValidateCommandTests
{
    [Fact]
    [Trait("Requirement", "GCPC-064")]
    public void Validate_DependsOnlyOnStorageAndProjection_NeitherReferencesRoslynOrMSBuildDirectly()
    {
        // `validate`'s own action body never mentions the Analysis-layer or Roslyn/MSBuild-touching types
        // that `analyze` uses to open a solution and run semantic analysis (source-level proof it is never
        // called from that branch), and the two assemblies its implementation is actually built from --
        // Storage (FactualPackageReader, PackageValidator) and Projection (ProjectionValidator) -- carry no
        // direct PackageReference to Microsoft.CodeAnalysis.* or Microsoft.Build.* (proof those types can
        // never load from validate's own dependency closure, even though the same *process* also hosts
        // `analyze` and therefore references Csharp2Md.Analysis, which does depend on Roslyn).
        var source = File.ReadAllText(
            Path.Combine(CliTestPaths.RepoRoot, "src", "Csharp2Md.Cli", "CommandFactory.cs"));
        var validateStart = source.IndexOf("ValidateAction(Option<string> packageOption)", StringComparison.Ordinal);
        var reportScenariosStart = source.IndexOf("ReportRetrievalScenarios(", validateStart, StringComparison.Ordinal);
        Assert.True(validateStart >= 0 && reportScenariosStart > validateStart);
        var validateBody = source[validateStart..reportScenariosStart];

        Assert.DoesNotContain("AnalysisEngine", validateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("IAnalysisEngine", validateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("AnalysisRequest", validateBody, StringComparison.Ordinal);

        AssertNoRoslynOrMsBuildPackageReference("Csharp2Md.Storage");
        AssertNoRoslynOrMsBuildPackageReference("Csharp2Md.Projection");
    }

    private static void AssertNoRoslynOrMsBuildPackageReference(string projectName)
    {
        var csprojPath = Path.Combine(CliTestPaths.RepoRoot, "src", projectName, projectName + ".csproj");
        Assert.True(File.Exists(csprojPath), $"Project file was not found at '{csprojPath}'.");

        var packageReferences = XDocument.Load(csprojPath)
            .Descendants()
            .Where(static element => element.Name.LocalName == "PackageReference")
            .Select(static element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            packageReferences,
            name => name.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Microsoft.Build", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Requirement", "GCPC-063")]
    [Trait("Requirement", "GCPC-067")]
    public async Task Validate_PublishedPackage_ReportsPublishedCertificationStatusAndMatchingExitCode()
    {
        var (child, outputPath) = await AnalyzeFixtureAsync();
        try
        {
            Assert.True(PublishedManifestTestFile.HasParts(child));
            var certification = CanonicalJson.Read<RunCertificationEnvelope>(
                File.ReadAllBytes(Path.Combine(child, "run-certification.json")));
            var expectedExit = certification.Status switch
            {
                "passed" => 0,
                "degraded" => 3,
                _ => 4,
            };

            var (exitCode, stdout, _) = await CliInvoke.RunAsync(["validate", "--package", child]);

            Assert.Equal(expectedExit, exitCode);
            Assert.Contains($"Certification: {certification.Status}", stdout, StringComparison.Ordinal);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-064")]
    public async Task Validate_WithNoAccessToTheOriginalSolutionDirectory_StillSucceeds()
    {
        var tempSolutionRoot = CopyFixtureToTemp();
        var outputPath = CliTestPaths.UniqueOutputPath();
        try
        {
            var solutionPath = Path.Combine(tempSolutionRoot, "Acme.Orders", "Acme.Orders.slnx");
            var (analyzeExit, _, _) = await CliInvoke.RunAsync(
                ["analyze", "--solution", solutionPath, "--output", outputPath]);
            Assert.Equal(ExitCodes.Degraded, analyzeExit);

            var child = SinglePackageDirectory(outputPath);

            // No access to the original solution directory: it is gone entirely before validate runs.
            Directory.Delete(tempSolutionRoot, recursive: true);

            var (validateExit, stdout, stderr) = await CliInvoke.RunAsync(["validate", "--package", child]);

            Assert.True(
                validateExit is 0 or 3 or 4,
                $"validate did not run to completion; exit {validateExit}, stderr: {stderr}");
            Assert.Contains("Certification:", stdout, StringComparison.Ordinal);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
            CliTestPaths.TryDeleteDirectory(tempSolutionRoot);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-064")]
    [Trait("Requirement", "GCPC-073")]
    public async Task Validate_DirectoryWithNoManifest_Exits1AndLeavesDirectoryUnchanged()
    {
        var directory = CliTestPaths.UniqueOutputPath();
        Directory.CreateDirectory(directory);
        var notesPath = Path.Combine(directory, "note.txt");
        File.WriteAllText(notesPath, "not a package");
        try
        {
            var before = Directory.GetFiles(directory).OrderBy(static path => path, StringComparer.Ordinal).ToArray();

            var (exitCode, _, stderr) = await CliInvoke.RunAsync(["validate", "--package", directory]);

            var after = Directory.GetFiles(directory).OrderBy(static path => path, StringComparer.Ordinal).ToArray();
            Assert.Equal(1, exitCode);
            Assert.Contains("csharp2md:", stderr, StringComparison.Ordinal);
            Assert.Equal(before, after);
            Assert.Equal("not a package", File.ReadAllText(notesPath));
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-052")]
    [Trait("Requirement", "GCPC-059")]
    public async Task Analyze_FoldsRetrievalScenarioMeasurementsIntoMeasurementsJson_AndValidateReportsThem()
    {
        var (child, outputPath) = await AnalyzeFixtureAsync();
        try
        {
            var measurements = CanonicalJson.Read<MeasurementsEnvelope>(
                File.ReadAllBytes(Path.Combine(child, "measurements.json")));
            Assert.Contains(
                measurements.Records,
                static record => record.Name.StartsWith("retrieval-scenario:", StringComparison.Ordinal));

            var (_, stdout, _) = await CliInvoke.RunAsync(["validate", "--package", child]);

            Assert.Contains("Scenario locate-an-identity:", stdout, StringComparison.Ordinal);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-065")]
    [Trait("Requirement", "GCPC-066")]
    public async Task Validate_PackageWithMismatchedManifestByteSize_Exits5AndNamesTheDefect()
    {
        var (child, outputPath) = await AnalyzeFixtureAsync();
        try
        {
            var manifestPath = Path.Combine(child, "manifest.json");
            var manifest = CanonicalJson.Read<ManifestEnvelope>(File.ReadAllBytes(manifestPath));
            var mutated = manifest with
            {
                Artifacts = manifest.Artifacts.SetItem(0, manifest.Artifacts[0] with { ByteSize = manifest.Artifacts[0].ByteSize + 1 }),
            };
            File.WriteAllBytes(manifestPath, CanonicalJson.Write(mutated).ToArray());

            var (exitCode, _, stderr) = await CliInvoke.RunAsync(["validate", "--package", child]);

            Assert.Equal(5, exitCode);
            Assert.Contains("manifest-size-mismatch", stderr, StringComparison.Ordinal);
            Assert.Contains(manifest.Artifacts[0].Path, stderr, StringComparison.Ordinal);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    /// <summary>The one package directory ("s-&lt;hash&gt;") under an output root that may also hold a
    /// top-level composition/ directory (GCPC-068 wired a BatchComposer into the real analyze store).</summary>
    private static string SinglePackageDirectory(string outputPath) =>
        Directory.GetDirectories(outputPath)
            .Single(static path => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), "^s-[0-9a-f]{32}$"));

    private static async Task<(string Child, string OutputPath)> AnalyzeFixtureAsync()
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        Assert.True(Path.Exists(solutionPath), $"Fixture solution was not found at '{solutionPath}'.");

        var outputPath = CliTestPaths.UniqueOutputPath();
        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--output", outputPath]);
        Assert.True(exitCode == ExitCodes.Degraded, $"analyze failed with exit {exitCode}: {stderr}");

        var child = SinglePackageDirectory(outputPath);
        return (child, outputPath);
    }

    private static string CopyFixtureToTemp()
    {
        var source = Path.Combine(CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
        var destination = Path.Combine(Path.GetTempPath(), "csharp2md-validate-fixture-" + Guid.NewGuid().ToString("N"));
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
}
