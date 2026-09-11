using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Cli.Tests;

/// <summary>
/// GCPC-069..GCPC-073: the seven-value exit-code space, each exercised by an invocation that produces
/// only that one outcome, extending the pre-existing <c>0</c>, <c>1</c> and <c>2</c> meanings.
/// </summary>
public sealed class ExitCodeTests
{
    [Fact]
    [Trait("Requirement", "GCPC-069")]
    public async Task Certification_Passed_MapsToZero()
    {
        var (child, outputPath) = await AnalyzeFixtureAsync();
        try
        {
            RewriteCertification(child, "passed");

            var (exitCode, _, _) = await CliInvoke.RunAsync(["validate", "--package", child]);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-070")]
    public async Task Certification_Degraded_MapsToThree()
    {
        var (child, outputPath) = await AnalyzeFixtureAsync();
        try
        {
            RewriteCertification(child, "degraded");

            var (exitCode, _, _) = await CliInvoke.RunAsync(["validate", "--package", child]);

            Assert.Equal(ExitCodes.Degraded, exitCode);
            Assert.Equal(3, exitCode);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-070")]
    public async Task Certification_Failed_MapsToFour()
    {
        var (child, outputPath) = await AnalyzeFixtureAsync();
        try
        {
            RewriteCertification(child, "failed");

            var (exitCode, _, _) = await CliInvoke.RunAsync(["validate", "--package", child]);

            Assert.Equal(ExitCodes.CertificationFailed, exitCode);
            Assert.Equal(4, exitCode);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-071")]
    public async Task StructuralCorruption_MapsToFive()
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

            var (exitCode, _, _) = await CliInvoke.RunAsync(["validate", "--package", child]);

            Assert.Equal(ExitCodes.StructuralCorruption, exitCode);
            Assert.Equal(5, exitCode);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-071")]
    public async Task IncompatibleProvenance_MapsToSix()
    {
        var (child, outputPath) = await AnalyzeFixtureAsync();
        try
        {
            var manifestPath = Path.Combine(child, "manifest.json");
            var manifest = CanonicalJson.Read<ManifestEnvelope>(File.ReadAllBytes(manifestPath));
            Assert.NotNull(manifest.Provenance);
            var mutated = manifest with { Provenance = manifest.Provenance! with { GeneratorVersion = "999.0.0.0" } };
            File.WriteAllBytes(manifestPath, CanonicalJson.Write(mutated).ToArray());

            var (exitCode, _, _) = await CliInvoke.RunAsync(["validate", "--package", child]);

            Assert.Equal(ExitCodes.IncompatibleProvenance, exitCode);
            Assert.Equal(6, exitCode);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    /// <summary>
    /// F3: exit 6 covers a newer contract version, not only a newer generator build -- a package whose
    /// SchemaVersion exceeds the running generator's is refused the same way a newer GeneratorVersion
    /// already was, and GeneratorVersion stays untouched here so this proves the schema-version axis
    /// specifically, not a restatement of the generator-version case above.
    /// </summary>
    [Fact]
    [Trait("Requirement", "GCPC-071")]
    public async Task IncompatibleSchemaVersion_MapsToSix()
    {
        var (child, outputPath) = await AnalyzeFixtureAsync();
        try
        {
            var manifestPath = Path.Combine(child, "manifest.json");
            var manifest = CanonicalJson.Read<ManifestEnvelope>(File.ReadAllBytes(manifestPath));
            Assert.NotNull(manifest.Provenance);
            var mutated = manifest with { Provenance = manifest.Provenance! with { SchemaVersion = manifest.Provenance.SchemaVersion + 1 } };
            File.WriteAllBytes(manifestPath, CanonicalJson.Write(mutated).ToArray());

            var (exitCode, _, _) = await CliInvoke.RunAsync(["validate", "--package", child]);

            Assert.Equal(ExitCodes.IncompatibleProvenance, exitCode);
            Assert.Equal(6, exitCode);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-073")]
    public async Task InvalidInvocation_MapsToOneAndPublishesNothing()
    {
        var outputPath = CliTestPaths.UniqueOutputPath();
        var missingSolution = Path.Combine(CliTestPaths.RepoRoot, "fixtures", "does-not-exist.slnx");
        try
        {
            var (exitCode, _, stderr) = await CliInvoke.RunAsync(
                ["analyze", "--solution", missingSolution, "--output", outputPath]);

            Assert.Equal(ExitCodes.InvalidInvocation, exitCode);
            Assert.Equal(1, exitCode);
            Assert.Contains("csharp2md:", stderr, StringComparison.Ordinal);
            Assert.False(Directory.Exists(outputPath), "An invalid invocation must publish nothing.");
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-072")]
    public async Task PartialComposition_UnpublishedSolutionInABatch_MapsToTwo()
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        IAnalysisEngine engine = new FakeAnalysisEngine(new AnalysisResult(
        [
            new SolutionOutcome(
                solutionPath,
                solutionPath.Replace('\\', '/'),
                PublicationStatus.Unpublished,
                failingStage: "Inventory",
                structuralCorruption: false,
                hasUnknownsOrCandidatesOrFrontiers: false,
                stages: []),
        ]));

        var (exitCode, _, _) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--output", CliTestPaths.UniqueOutputPath()],
            engine);

        Assert.Equal(ExitCodes.PartialComposition, exitCode);
        Assert.Equal(2, exitCode);
    }

    [Fact]
    [Trait("Requirement", "GCPC-069")]
    [Trait("Requirement", "GCPC-070")]
    [Trait("Requirement", "GCPC-071")]
    [Trait("Requirement", "GCPC-072")]
    [Trait("Requirement", "GCPC-073")]
    public void ExitCodes_ExtendRatherThanRenumberTheExistingThree()
    {
        // The pre-existing 0/1/2 meanings (success, invalid invocation, partial composition) are asserted
        // unchanged against every other passing CLI test in this project (AnalyzeExitCodeTests,
        // AnalyzeBatchFailureTests, AnalyzeMultiSolutionTests); this is the deterministic proof that the
        // constants themselves still carry those exact values, so a future edit cannot silently renumber
        // them.
        Assert.Equal(0, ExitCodes.Success);
        Assert.Equal(1, ExitCodes.InvalidInvocation);
        Assert.Equal(2, ExitCodes.PartialComposition);
        Assert.Equal(3, ExitCodes.Degraded);
        Assert.Equal(4, ExitCodes.CertificationFailed);
        Assert.Equal(5, ExitCodes.StructuralCorruption);
        Assert.Equal(6, ExitCodes.IncompatibleProvenance);
    }

    /// <summary>
    /// Rewrites <c>run-certification.json</c>'s status and keeps the manifest's declared byte size for it
    /// in step -- otherwise the differing status string's differing length would trip the coarser
    /// manifest-size-mismatch check before this test ever reaches the certification-status mapping it is
    /// actually proving (the same technique T49's corruption tests use deliberately in reverse).
    /// </summary>
    private static void RewriteCertification(string packageDirectory, string status)
    {
        var certificationPath = Path.Combine(packageDirectory, "run-certification.json");
        var current = CanonicalJson.Read<RunCertificationEnvelope>(File.ReadAllBytes(certificationPath));
        var mutated = new RunCertificationEnvelope(status, current.Reasons);
        var certificationBytes = CanonicalJson.Write(mutated);
        File.WriteAllBytes(certificationPath, certificationBytes.ToArray());

        PublishedManifestTestFile.RewriteEntry(
            packageDirectory,
            static entry => entry.Path == "run-certification.json",
            entry => entry with { ByteSize = certificationBytes.Length });
    }

    private static async Task<(string Child, string OutputPath)> AnalyzeFixtureAsync()
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        Assert.True(Path.Exists(solutionPath), $"Fixture solution was not found at '{solutionPath}'.");

        var outputPath = CliTestPaths.UniqueOutputPath();
        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--output", outputPath]);
        Assert.True(exitCode == 0, $"analyze failed with exit {exitCode}: {stderr}");

        // GCPC-068 (T51) wired a BatchComposer into the real analyze store, so this fixture's own
        // composition facts also produce a top-level composition/ directory under --output -- filter for
        // the package's deterministic "s-<hash>" name.
        var child = Directory.GetDirectories(outputPath)
            .Single(static path => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), "^s-[0-9a-f]{32}$"));
        return (child, outputPath);
    }

    private sealed class FakeAnalysisEngine : IAnalysisEngine
    {
        private readonly AnalysisResult _result;

        public FakeAnalysisEngine(AnalysisResult result) => _result = result;

        public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(_result);
    }
}
