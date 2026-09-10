using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Composition;
using Csharp2Md.Analysis.Tests.Fixtures;
using Csharp2Md.Projection;
using Csharp2Md.Projection.Composition;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Determinism;

/// <summary>
/// GCPC-108, GCPC-109, GCPC-110: the whole published package -- factual, projection, provenance and
/// composition artifacts together -- is byte-identical across a repeated run, two different absolute
/// clone paths, a reversed <c>--solution</c> order, and a shuffled document enumeration order. The
/// pre-existing determinism suites (<c>Pipeline/ClonePathIndependenceTests</c> compares only structural
/// and observation fact identities; <c>Extraction/DocumentOrderIndependenceTests</c> stops at the
/// Analysis-only pipeline stages; <c>Composition/BatchDeterminismTests</c> compares only the
/// batch-manifest and composition layer) each proved one slice. This suite proves the same invariant
/// over every file a real <c>analyze</c> run publishes for the certification corpus, explicitly
/// including provenance, catalog labels and relation/posting shard keys in the comparison, not only
/// relying on whole-directory byte equality to imply it.
/// </summary>
public sealed class WholePackageDeterminismTests
{
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    [Fact]
    [Trait("Requirement", "GCPC-108")]
    public async Task Analyze_CertificationCorpusTwice_PublishesByteIdenticalPackage()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-wpd-repeat-");
        try
        {
            var runA = await CompositionBatch.AnalyzeAsync(
                Path.Combine(tree.FullName, "a"), CertificationCorpusPaths.SolutionPath);
            var runB = await CompositionBatch.AnalyzeAsync(
                Path.Combine(tree.FullName, "b"), CertificationCorpusPaths.SolutionPath);

            CompositionBatch.AssertEqualSnapshots(runA.Files, runB.Files);

            var packageA = SinglePackageFiles(runA.Files);
            var packageB = SinglePackageFiles(runB.Files);
            AssertProvenanceIdentical(packageA, packageB);
            AssertCatalogLabelsIdentical(packageA, packageB);
            AssertRelationAndPostingShardKeysIdentical(packageA, packageB);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-109")]
    public async Task Analyze_CertificationCorpusFromTwoAbsolutePaths_PublishesByteIdenticalPackage()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-wpd-path-");
        try
        {
            var cloneA = Path.Combine(tree.FullName, "clone-a");
            var cloneB = Path.Combine(tree.FullName, "clone-b");
            CopyCertificationCorpusClone(cloneA);
            CopyCertificationCorpusClone(cloneB);

            var rootA = Path.GetFullPath(cloneA);
            var rootB = Path.GetFullPath(cloneB);
            Assert.NotEqual(rootA, rootB);

            var solutionA = Path.Combine(cloneA, "CertificationCorpus", "CertificationCorpus.slnx");
            var solutionB = Path.Combine(cloneB, "CertificationCorpus", "CertificationCorpus.slnx");
            Assert.True(File.Exists(solutionA), $"Expected cloned solution at '{solutionA}'.");
            Assert.True(File.Exists(solutionB), $"Expected cloned solution at '{solutionB}'.");

            var runA = await CompositionBatch.AnalyzeAsync(Path.Combine(tree.FullName, "out-a"), solutionA);
            var runB = await CompositionBatch.AnalyzeAsync(Path.Combine(tree.FullName, "out-b"), solutionB);

            CompositionBatch.AssertEqualSnapshots(runA.Files, runB.Files);
            AssertNoClonePath(runA.Files, rootA);
            AssertNoClonePath(runB.Files, rootB);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-110")]
    public async Task Analyze_ReversedSolutionOrder_PublishesByteIdenticalBatchAndComposition()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-wpd-order-");
        try
        {
            var certification = CertificationCorpusPaths.SolutionPath;
            var configurationShapes = ConfigurationShapesSolutionPath();

            var forward = await CompositionBatch.AnalyzeAsync(
                Path.Combine(tree.FullName, "forward"), certification, configurationShapes);
            var reversed = await CompositionBatch.AnalyzeAsync(
                Path.Combine(tree.FullName, "reversed"), configurationShapes, certification);

            CompositionBatch.AssertEqualSnapshots(forward.Files, reversed.Files);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-110")]
    public async Task Analyze_ShuffledDocumentEnumerationOrder_PublishesByteIdenticalPackage()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-wpd-shuffle-");
        try
        {
            var forward = await AnalyzeWithStages(
                Path.Combine(tree.FullName, "forward"), PipelineStages.CreateDefault());
            var shuffled = await AnalyzeWithStages(
                Path.Combine(tree.FullName, "shuffled"),
                PipelineStages.CreateDefault().SetItem(0, new DocumentOrderReversingInventoryStage()));

            CompositionBatch.AssertEqualSnapshots(forward, shuffled);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static async Task<IReadOnlyDictionary<string, byte[]>> AnalyzeWithStages(
        string outputRoot,
        ImmutableArray<IPipelineStage> stages)
    {
        Directory.CreateDirectory(outputRoot);
        var store = new FilesystemTransactionalStore(outputRoot, new PackageProjector(), new BatchComposer());
        var engine = new AnalysisEngine(store, stages);
        var result = await engine.AnalyzeAsync(
            AnalysisRequest.Create([CertificationCorpusPaths.SolutionPath]),
            CancellationToken.None);

        Assert.Equal(PublicationStatus.Committed, Assert.Single(result.Solutions).Status);
        return CompositionBatch.SnapshotFiles(outputRoot);
    }

    /// <summary>Runs the real <see cref="InventoryStage"/> then reverses the resulting document group so
    /// every later stage receives the same documents in the opposite enumeration order -- the same
    /// technique <c>DocumentOrderIndependenceTests</c> (ROSE-54) uses, extended here through the full
    /// pipeline and a real filesystem commit instead of stopping at in-memory fact identities.</summary>
    private sealed class DocumentOrderReversingInventoryStage : IPipelineStage
    {
        private readonly InventoryStage _inner = new();

        public string Name => _inner.Name;

        public async ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            var result = await _inner.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            context.CSharpDocuments = context.CSharpDocuments.Reverse().ToImmutableArray();
            return result;
        }
    }

    private static IReadOnlyDictionary<string, byte[]> SinglePackageFiles(IReadOnlyDictionary<string, byte[]> batchFiles)
    {
        var manifest = CanonicalJson.Read<BatchManifestEnvelope>(RequireFile(batchFiles, "batch-manifest.json"));
        var solution = Assert.Single(manifest.Solutions);
        return CompositionBatch.PackageFiles(batchFiles, solution.PackageDirectory);
    }

    private static void AssertProvenanceIdentical(
        IReadOnlyDictionary<string, byte[]> left,
        IReadOnlyDictionary<string, byte[]> right)
    {
        var manifestLeft = CanonicalJson.Read<ManifestEnvelope>(RequireFile(left, "manifest.json"));
        var manifestRight = CanonicalJson.Read<ManifestEnvelope>(RequireFile(right, "manifest.json"));

        Assert.NotNull(manifestLeft.Provenance);
        Assert.NotNull(manifestRight.Provenance);
        Assert.Equal(manifestLeft.Provenance, manifestRight.Provenance);
    }

    private static void AssertCatalogLabelsIdentical(
        IReadOnlyDictionary<string, byte[]> left,
        IReadOnlyDictionary<string, byte[]> right)
    {
        const string catalogKey = "catalogs/entry-points.json";
        var catalogLeft = CompositionBatch.ReadArray(left, catalogKey);
        var catalogRight = CompositionBatch.ReadArray(right, catalogKey);

        Assert.NotEmpty(catalogLeft);
        Assert.True(
            JsonNode.DeepEquals(catalogLeft, catalogRight),
            $"Catalog labels at '{catalogKey}' differ between the two runs.");
    }

    private static void AssertRelationAndPostingShardKeysIdentical(
        IReadOnlyDictionary<string, byte[]> left,
        IReadOnlyDictionary<string, byte[]> right)
    {
        var relationKeysLeft = ShardAwareKeys(left, "relations/");
        var relationKeysRight = ShardAwareKeys(right, "relations/");
        Assert.NotEmpty(relationKeysLeft);
        Assert.Equal(relationKeysLeft, relationKeysRight);

        var postingKeysLeft = ShardAwareKeys(left, "postings/");
        var postingKeysRight = ShardAwareKeys(right, "postings/");
        Assert.Equal(postingKeysLeft, postingKeysRight);
    }

    private static string[] ShardAwareKeys(IReadOnlyDictionary<string, byte[]> package, string prefix) =>
        package.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static byte[] RequireFile(IReadOnlyDictionary<string, byte[]> package, string key)
    {
        Assert.True(package.TryGetValue(key, out var bytes), $"Expected published '{key}'.");
        return bytes;
    }

    private static void AssertNoClonePath(IReadOnlyDictionary<string, byte[]> package, string cloneRoot)
    {
        var slash = cloneRoot.Replace('\\', '/');
        var jsonEscaped = cloneRoot.Replace("\\", "\\\\", StringComparison.Ordinal);
        foreach (var (key, bytes) in package)
        {
            if (key.StartsWith("source/", StringComparison.Ordinal))
            {
                // source/ deliberately carries the original document text, not an identity; skip it.
                continue;
            }

            var text = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain(cloneRoot, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(slash, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(jsonEscaped, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ConfigurationShapesSolutionPath()
    {
        var path = Path.Combine(CertificationCorpusPaths.RootPath, "ConfigurationShapes", "ConfigurationShapes.sln");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    private static void CopyCertificationCorpusClone(string destination)
    {
        var fixture = CertificationCorpusPaths.RootPath;
        Assert.True(Directory.Exists(fixture), $"Expected fixture at '{fixture}'.");
        Directory.CreateDirectory(destination);
        File.Copy(
            Path.Combine(AnalysisTestPaths.RepoRoot, "Directory.Packages.props"),
            Path.Combine(destination, "Directory.Packages.props"));
        File.Copy(
            Path.Combine(AnalysisTestPaths.RepoRoot, "NuGet.Config"),
            Path.Combine(destination, "NuGet.Config"));
        File.WriteAllText(
            Path.Combine(destination, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
            </Project>
            """);

        var tree = Path.Combine(destination, "CertificationCorpus");
        foreach (var file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(fixture, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(ExcludedSegments.Contains))
            {
                continue;
            }

            var target = Path.Combine(tree, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
