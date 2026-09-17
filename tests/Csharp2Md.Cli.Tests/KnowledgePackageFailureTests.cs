using System.Text;
using System.Text.Json;
using Csharp2Md.Core;

namespace Csharp2Md.Cli.Tests;

public sealed class KnowledgePackageFailureTests : IClassFixture<KnowledgePackageFailureTests.BaselinePackage>
{
    private readonly BaselinePackage _baseline;

    public KnowledgePackageFailureTests(BaselinePackage baseline) => _baseline = baseline;

    [Theory]
    [InlineData("variant", "analysis", "variant-collision")]
    [InlineData("retention", "retention", "invalid-confirmed-relation")]
    [InlineData("safety", "publication", "absolute-path")]
    [InlineData("size", "package-building", "artifacts")]
    [InlineData("materialization", "publication", "staging-or-validation")]
    [InlineData("rehydration", "validation", "invalid-artifact")]
    [InlineData("validation", "validation", "invalid-package")]
    [InlineData("equivalence", "validation", "markdown-divergence")]
    [InlineData("journey-budget", "certification", "reads-exceeded:33>32")]
    public async Task Analyze_EachSpecifiedRejectionClass_ReportsStructuredCoordinates(
        string rejectionClass,
        string stage,
        string cause)
    {
        var diagnostic = new EngineDiagnostic(
            rejectionClass + "-rejected",
            stage,
            cause,
            solution: "acme-journey",
            project: "Acme.Journey/Acme.Journey.csproj",
            variant: "net10.0|Debug|-|-",
            family: "index",
            artifact: "solutions/acme/indexes/roots.json");

        var (exitCode, stdout, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", _baseline.SolutionPath, "--output", CliTestPaths.UniqueOutputPath()],
            (_, _) => Task.FromResult(new AnalyzeResult(committed: false, [diagnostic])));

        Assert.NotEqual(ExitCodes.Success, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("code=" + rejectionClass + "-rejected", stderr, StringComparison.Ordinal);
        Assert.Contains("stage=" + stage, stderr, StringComparison.Ordinal);
        Assert.Contains("cause=" + cause, stderr, StringComparison.Ordinal);
        Assert.Contains("solution=acme-journey", stderr, StringComparison.Ordinal);
        Assert.Contains("project=Acme.Journey/Acme.Journey.csproj", stderr, StringComparison.Ordinal);
        Assert.Contains("variant=net10.0|Debug|-|-", stderr, StringComparison.Ordinal);
        Assert.Contains("family=index", stderr, StringComparison.Ordinal);
        Assert.Contains("artifact=solutions/acme/indexes/roots.json", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validate_MissingRootManifest_LeavesInjectedPackageByteIdentical()
    {
        using var candidate = _baseline.Copy();
        File.Delete(Path.Combine(candidate.Directory, "manifest.json"));

        await AssertRejectedWithoutMutationAsync(candidate, "package-corruption", "missing-artifact", "manifest", "manifest.json");
    }

    [Fact]
    public async Task Validate_InvalidRootManifest_LeavesInjectedPackageByteIdentical()
    {
        using var candidate = _baseline.Copy();
        await File.WriteAllTextAsync(Path.Combine(candidate.Directory, "manifest.json"), "not-json", Encoding.UTF8);

        await AssertRejectedWithoutMutationAsync(candidate, "package-corruption", "invalid-package", "manifest", "manifest.json");
    }

    [Fact]
    public async Task Validate_MissingDeclaredIndex_LeavesRootAndPriorGenerationByteIdentical()
    {
        using var candidate = _baseline.Copy();
        var declaredIndex = candidate.DeclaredIndexPath();
        File.Delete(Path.Combine(candidate.GenerationDirectory, declaredIndex.Replace('/', Path.DirectorySeparatorChar)));

        await AssertRejectedWithoutMutationAsync(
            candidate,
            "package-corruption",
            "missing-artifact",
            "index",
            Path.GetRelativePath(candidate.Directory, Path.Combine(candidate.GenerationDirectory, declaredIndex.Replace('/', Path.DirectorySeparatorChar))).Replace('\\', '/'));
    }

    [Fact]
    public async Task Validate_AbsolutePathInCommittedMarkdown_ReportsSafetyWithoutMutation()
    {
        using var candidate = _baseline.Copy();
        const string artifact = "markdown/index.md";
        await File.WriteAllTextAsync(
            Path.Combine(candidate.GenerationDirectory, artifact.Replace('/', Path.DirectorySeparatorChar)),
            "C:/forbidden/source.cs",
            Encoding.UTF8);

        await AssertRejectedWithoutMutationAsync(candidate, "publication-safety", "absolute-path", "markdown", artifact);
    }

    [Fact]
    public async Task Validate_MarkdownMachineDivergence_LeavesInjectedPackageByteIdentical()
    {
        using var candidate = _baseline.Copy();
        const string artifact = "markdown/index.md";
        await File.WriteAllTextAsync(
            Path.Combine(candidate.GenerationDirectory, artifact.Replace('/', Path.DirectorySeparatorChar)),
            "# divergent markdown",
            Encoding.UTF8);

        await AssertRejectedWithoutMutationAsync(candidate, "package-corruption", "invalid-artifact", "markdown", artifact);
    }

    [Fact]
    public async Task Analyze_WhenPublicationLockIsHeld_PreservesPriorCommittedPackageAndCleansStaging()
    {
        var before = PackageSnapshot.Read(_baseline.Directory);
        int exitCode;
        string stdout;
        string stderr;
        using (var heldLock = new FileStream(
            Path.Combine(_baseline.Directory, "package.lock"),
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None))
        {
            (exitCode, stdout, stderr) = await CliInvoke.RunAsync(
                ["analyze", "--solution", _baseline.SolutionPath, "--output", _baseline.Directory]);
        }

        Assert.NotEqual(ExitCodes.Success, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("code=analysis-failed", stderr, StringComparison.Ordinal);
        Assert.Contains("stage=analysis", stderr, StringComparison.Ordinal);
        Assert.Contains("cause=IOException", stderr, StringComparison.Ordinal);
        AssertSnapshotEqual(before, PackageSnapshot.Read(_baseline.Directory));
        Assert.Empty(Directory.GetDirectories(_baseline.Directory, ".staging-*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task Analyze_WhenCancelledBeforePublication_PreservesPriorCommittedPackage()
    {
        var before = PackageSnapshot.Read(_baseline.Directory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var engine = new KnowledgeEngine();

        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.AnalyzeAsync(
            new AnalyzeRequest([_baseline.SolutionPath], _baseline.Directory),
            cancellation.Token));

        AssertSnapshotEqual(before, PackageSnapshot.Read(_baseline.Directory));
        Assert.Empty(Directory.GetDirectories(_baseline.Directory, ".staging-*", SearchOption.TopDirectoryOnly));
    }

    private static async Task AssertRejectedWithoutMutationAsync(
        CandidatePackage candidate,
        string code,
        string cause,
        string family,
        string artifact)
    {
        var before = PackageSnapshot.Read(candidate.Directory);
        var (exitCode, stdout, stderr) = await CliInvoke.RunAsync(["validate", "--package", candidate.Directory]);

        Assert.Equal(ExitCodes.StructuralCorruption, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("code=" + code, stderr, StringComparison.Ordinal);
        Assert.Contains("stage=validation", stderr, StringComparison.Ordinal);
        Assert.Contains("cause=" + cause, stderr, StringComparison.Ordinal);
        Assert.Contains("family=" + family, stderr, StringComparison.Ordinal);
        Assert.Contains("artifact=" + artifact, stderr, StringComparison.Ordinal);
        AssertSnapshotEqual(before, PackageSnapshot.Read(candidate.Directory));
        Assert.Empty(Directory.GetDirectories(candidate.Directory, ".staging-*", SearchOption.TopDirectoryOnly));
    }

    private static void AssertSnapshotEqual(PackageSnapshot expected, PackageSnapshot actual)
    {
        Assert.Equal(expected.Files.Keys.Order(), actual.Files.Keys.Order());
        foreach (var path in expected.Files.Keys)
        {
            Assert.Equal(expected.Files[path], actual.Files[path]);
        }
    }

    public sealed class BaselinePackage : IAsyncLifetime
    {
        internal string Directory { get; private set; } = null!;
        internal string SourceDirectory { get; private set; } = null!;
        internal string SolutionPath => Path.Combine(SourceDirectory, "Acme.Journey.slnx");

        public async Task InitializeAsync()
        {
            SourceDirectory = CliTestPaths.UniqueOutputPath();
            CopyDirectory(Path.Combine(CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution"), SourceDirectory);
            Directory = CliTestPaths.UniqueOutputPath();
            var (exitCode, _, stderr) = await CliInvoke.RunAsync(
                ["analyze", "--solution", SolutionPath, "--output", Directory]);
            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.True(File.Exists(Path.Combine(Directory, "manifest.json")), stderr);
        }

        public Task DisposeAsync()
        {
            CliTestPaths.TryDeleteDirectory(Directory);
            CliTestPaths.TryDeleteDirectory(SourceDirectory);
            return Task.CompletedTask;
        }

        internal CandidatePackage Copy()
        {
            var target = CliTestPaths.UniqueOutputPath();
            CopyDirectory(Directory, target);
            return new CandidatePackage(target);
        }
    }

    internal sealed class CandidatePackage : IDisposable
    {
        internal CandidatePackage(string directory)
        {
            Directory = directory;
            using var root = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "manifest.json")));
            var generation = root.RootElement.GetProperty("generation").GetString();
            Assert.False(string.IsNullOrWhiteSpace(generation));
            GenerationDirectory = Path.Combine(directory, "generations", generation!);
        }

        internal string Directory { get; }
        internal string GenerationDirectory { get; }

        internal string DeclaredIndexPath()
        {
            using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(GenerationDirectory, "manifest.json")));
            return manifest.RootElement.GetProperty("solutions")[0].GetProperty("indexes")[0]
                .GetProperty("entry_path").GetString()!;
        }

        public void Dispose() => CliTestPaths.TryDeleteDirectory(Directory);
    }

    private sealed record PackageSnapshot(IReadOnlyDictionary<string, byte[]> Files)
    {
        internal static PackageSnapshot Read(string directory) => new(
            Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .OrderBy(path => Path.GetRelativePath(directory, path), StringComparer.Ordinal)
                .ToDictionary(
                    path => Path.GetRelativePath(directory, path).Replace('\\', '/'),
                    File.ReadAllBytes,
                    StringComparer.Ordinal));
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
