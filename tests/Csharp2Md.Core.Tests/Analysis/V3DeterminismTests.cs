using System.Security.Cryptography;
using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Manifests;
using Csharp2Md.Core.Projection.Aggregates;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;
using VerifyXunit;

namespace Csharp2Md.Core.Tests.Analysis;

[Trait("Category", "Integration")]
public sealed class V3DeterminismTests(V3DeterminismFixture fixture) : IClassFixture<V3DeterminismFixture>
{
    [Fact]
    public void AnalyzeAsync_SameFixtureFromTwoAbsoluteRoots_ProducesByteIdenticalTreesExceptLog()
    {
        var rawA = TopicLayout.RawRoot(fixture.OutputA);
        var rawB = TopicLayout.RawRoot(fixture.OutputB);

        var relativeA = RelativeFiles(rawA);
        var relativeB = RelativeFiles(rawB);
        Assert.NotEmpty(relativeA);
        Assert.Equal(relativeA, relativeB);

        foreach (var relative in relativeA.Where(static path => path != "log.md"))
        {
            var bytesA = File.ReadAllBytes(Path.Combine(rawA, relative));
            var bytesB = File.ReadAllBytes(Path.Combine(rawB, relative));
            Assert.True(bytesA.AsSpan().SequenceEqual(bytesB), $"byte mismatch outside raw/log.md: {relative}");
        }
    }

    [Fact]
    public void AnalyzeAsync_Output_NeverLeaksEitherAbsoluteInputRoot()
    {
        foreach (var output in new[] { fixture.OutputA, fixture.OutputB })
        {
            var rawRoot = TopicLayout.RawRoot(output);
            foreach (var file in Directory.EnumerateFiles(rawRoot, "*", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                Assert.DoesNotContain(fixture.RootA, content, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(fixture.RootB, content, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void AnalyzeAsync_ManifestFragments_ResolveAndHashesMatchIndependentlyComputedBytes()
    {
        var rawRoot = TopicLayout.RawRoot(fixture.OutputA);
        var manifest = JsonSerializer.Deserialize(
            File.ReadAllBytes(Path.Combine(rawRoot, "facts", "manifest.json")),
            AggregateJsonContext.Default.FactualManifest)
            ?? throw new InvalidOperationException("manifest.json deserialized to null.");

        Assert.NotEmpty(manifest.Fragments);
        foreach (var fragment in manifest.Fragments)
        {
            var path = Path.Combine(rawRoot, fragment.Reference.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"manifest references a missing fragment: {fragment.Reference}");

            var bytes = File.ReadAllBytes(path);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            Assert.Equal(fragment.Sha256, hash);
            Assert.Equal(fragment.ByteLength, bytes.Length);
        }

        Assert.Equal(
            manifest.Fragments.Select(static fragment => fragment.Sha256).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
            manifest.Hashes);
    }

    [Fact]
    public void AnalyzeAsync_Output_OmitsV2DependenciesJsonAndWritesRelationDerivedMermaid()
    {
        foreach (var output in new[] { fixture.OutputA, fixture.OutputB })
        {
            var rawRoot = TopicLayout.RawRoot(output);
            Assert.False(File.Exists(Path.Combine(rawRoot, "dependencies.json")));

            // Syntax-only mode confirms no runtime relation without semantic binding, so the diagram is
            // built (not merely copied) from an empty validated relation set rather than a static template.
            var mermaid = File.ReadAllText(Path.Combine(rawRoot, "dependencies.mmd"));
            Assert.Equal("flowchart LR\n", mermaid);
        }
    }

    [Fact]
    public void AnalyzeAsync_RepresentativeDocument_PersistedFragmentReconstructsOriginalSourceBytes()
    {
        var originalPath = Path.Combine(fixture.RootA, "Acme.Orders", "OrderService.cs");
        var original = File.ReadAllText(originalPath);

        var fragment = FindDocumentFragment(fixture.OutputA, "OrderService.cs");
        var reconstructed = string.Concat(
            fragment.SourceSections.OrderBy(static section => section.StartOffset).Select(static section => section.Source));

        Assert.Equal(original, reconstructed);
    }

    [Fact]
    public Task AnalyzeAsync_RepresentativeDocument_MatchesApprovedMarkdownSnapshot()
    {
        var codebaseRoot = TopicLayout.CodebaseRoot(fixture.OutputA);
        var generated = Directory.EnumerateFiles(codebaseRoot, "OrderService.cs.md", SearchOption.AllDirectories).Single();
        return Verifier.Verify(File.ReadAllText(generated), "md").UseDirectory("snapshots");
    }

    private static FactualJsonDocument FindDocumentFragment(string outputRoot, string fileName)
    {
        var documentRoot = Path.Combine(TopicLayout.RawRoot(outputRoot), "facts", "document");
        foreach (var path in Directory.EnumerateFiles(documentRoot, "*.json", SearchOption.AllDirectories))
        {
            var candidate = FactualJsonSerializer.Deserialize(File.ReadAllBytes(path));
            if (candidate.Documents.Length == 1
                && candidate.Documents[0].RelativePath.EndsWith(fileName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"No persisted document fragment found for {fileName}.");
    }

    private static string[] RelativeFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
}

public sealed class V3DeterminismFixture : IAsyncLifetime
{
    private static readonly string[] Projects =
        [SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts];

    public string RootA { get; } = Directory.CreateTempSubdirectory("csharp2md-det-a-").FullName;
    public string RootB { get; } = Directory.CreateTempSubdirectory("csharp2md-det-b-").FullName;
    public string OutputA { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-det-out-a-{Guid.NewGuid():N}");
    public string OutputB { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-det-out-b-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        foreach (var project in Projects)
        {
            CopyProject(project, RootA);
            CopyProject(project, RootB);
        }

        // Acme.Orders and Acme.Payments each carry their own .slnx that references the shared
        // Acme.Shared.Contracts project. Directory-mode auto-discovery would resolve that .slnx as
        // each service's boundary and re-inventory the shared project under two services at once,
        // colliding on its documents' fact identities. An explicit manifest (the same override shape
        // every other test against this fixture already uses) names each project directly instead.
        var manifestA = FixtureManifest.Write(RootA, BuildManifest(RootA));
        var manifestB = FixtureManifest.Write(RootB, BuildManifest(RootB));

        await Analyze(manifestA, OutputA);
        await Analyze(manifestB, OutputB);
    }

    private static Manifest BuildManifest(string root) =>
        new(Projects.Select(name => new ManifestEntry(
            Path.Combine(root, name),
            name,
            new[] { Path.Combine(root, name, name + ".csproj") })).ToList());

    public Task DisposeAsync()
    {
        Directory.Delete(RootA, recursive: true);
        Directory.Delete(RootB, recursive: true);
        Directory.Delete(OutputA, recursive: true);
        Directory.Delete(OutputB, recursive: true);
        return Task.CompletedTask;
    }

    private static async Task Analyze(string input, string output)
    {
        var request = Assert.IsType<AnalysisRequest>(
            AnalysisRequest.Create(input, output, topic: "acme-determinism", domain: "system-design").Request);
        var result = await new AnalysisEngine().AnalyzeAsync(request);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"fixture analysis failed with exit {result.ExitCode}: {string.Join(" | ", result.Diagnostics)}");
        }
    }

    private static void CopyProject(string projectName, string destinationRoot)
    {
        var sourceRoot = TestPaths.SyntheticSolution(projectName);
        var destinationProjectRoot = Path.Combine(destinationRoot, projectName);
        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, sourceFile);
            if (relative.Split(Path.DirectorySeparatorChar).Any(static segment => segment is "bin" or "obj"))
            {
                continue;
            }

            var destinationFile = Path.Combine(destinationProjectRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(sourceFile, destinationFile);
        }
    }
}
