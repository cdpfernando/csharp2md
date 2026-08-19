using Csharp2Md.Core.Output;
using Csharp2Md.Core.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Pipeline;

/// <summary>
/// WIKI-12: a frontmatter validation failure never aborts the run, surfaces on
/// <see cref="PipelineRunResult"/> naming the failing file and its error, and warrants exit code
/// <c>1</c> — distinct from a degraded project load (AD-005), which stays exit code <c>0</c>.
/// </summary>
/// <remarks>
/// An empty <c>--domain</c> is the forcing mechanism: <c>TopicOptions.Create</c> validates only the
/// <c>topic</c> slug pattern (WIKI-14), not <c>domain</c>, so a caller-supplied empty domain reaches
/// <see cref="FrontmatterYaml.Validate"/> as a genuinely empty required field — the real validation
/// path, not a test-only hook into internals.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class FrontmatterValidationTests : IDisposable
{
    private readonly string _workspace =
        Directory.CreateTempSubdirectory("csharp2md-frontmatter-validation-").FullName;

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    private static TopicOptions FailingOptions() =>
        TopicOptions.Create("acme-shop", domain: "", inputRoot: "input-root").Options!;

    [Fact]
    public async Task RunAsync_FrontmatterValidationFailure_DoesNotStopTheRunAndStillWritesEveryDocument()
    {
        var outputRoot = Path.Combine(_workspace, "output");
        var manifestPath = FixtureManifest.WriteOverrides(_workspace, SyntheticFixtureRun.Orders);

        await new AnalysisPipeline().RunAsync(
            manifestPath, outputRoot, CancellationToken.None, forceOutput: false, topicOptions: FailingOptions());

        var expected = FixtureManifest
            .ExpectedSourceFiles(TestPaths.SyntheticSolution(SyntheticFixtureRun.Orders))
            .Count;
        var generated = GeneratedDocuments(TopicLayout.CodebaseRoot(outputRoot));

        Assert.Equal(expected, generated.Count);
    }

    [Fact]
    public async Task RunAsync_FrontmatterValidationFailure_SurfacesEachFailingPathAndError()
    {
        var outputRoot = Path.Combine(_workspace, "output");
        var manifestPath = FixtureManifest.WriteOverrides(_workspace, SyntheticFixtureRun.Orders);

        var result = await new AnalysisPipeline().RunAsync(
            manifestPath, outputRoot, CancellationToken.None, forceOutput: false, topicOptions: FailingOptions());

        var expectedCount = FixtureManifest
            .ExpectedSourceFiles(TestPaths.SyntheticSolution(SyntheticFixtureRun.Orders))
            .Count;

        Assert.Equal(expectedCount, result.FrontmatterFailures.Count);
        Assert.All(result.FrontmatterFailures, failure =>
        {
            Assert.StartsWith(SyntheticFixtureRun.Orders + "/", failure.SourcePath, StringComparison.Ordinal);
            Assert.Contains("domain", failure.Error, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task RunAsync_FrontmatterValidationFailure_ExitCodeIsOneWithEveryOtherArtifactPresent()
    {
        var outputRoot = Path.Combine(_workspace, "output");
        var manifestPath = FixtureManifest.WriteOverrides(_workspace, SyntheticFixtureRun.Orders);

        var result = await new AnalysisPipeline().RunAsync(
            manifestPath, outputRoot, CancellationToken.None, forceOutput: false, topicOptions: FailingOptions());

        Assert.Equal(1, result.ExitCode);

        var rawRoot = TopicLayout.RawRoot(outputRoot);
        var codebaseRoot = TopicLayout.CodebaseRoot(outputRoot);
        var ordersRoot = TopicLayout.ServiceRoot(outputRoot, new ServiceName(SyntheticFixtureRun.Orders));

        Assert.True(File.Exists(Path.Combine(rawRoot, DependencyJsonWriter.FileName)));
        Assert.True(File.Exists(Path.Combine(rawRoot, MermaidWriter.FileName)));
        Assert.True(File.Exists(Path.Combine(codebaseRoot, IndexWriter.FileName)));
        Assert.True(File.Exists(Path.Combine(ordersRoot, IndexWriter.FileName)));
        Assert.True(File.Exists(Path.Combine(outputRoot, ".csharp2md-output")));
    }

    // AD-005: a degraded project load (Acme.Payments never restores) is an existing, unrelated
    // condition. Frontmatter derivation is syntax-only (WIKI-13), so it never fails validation just
    // because the project is degraded — the two conditions must never collapse into one signal.
    [Fact]
    public async Task RunAsync_DegradedProjectLoad_StaysExitCodeZero_DistinctFromFrontmatterFailure()
    {
        var outputRoot = Path.Combine(_workspace, "output");
        var manifestPath = FixtureManifest.WriteOverrides(_workspace, SyntheticFixtureRun.Payments);

        var result = await new AnalysisPipeline().RunAsync(manifestPath, outputRoot, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.FrontmatterFailures);
        Assert.Equal(0, result.ExitCode);
    }

    private static IReadOnlyList<string> GeneratedDocuments(string codebaseRoot) =>
        Directory.EnumerateFiles(codebaseRoot, "*.md", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != IndexWriter.FileName)
            .ToList();
}
