using System.Text;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Projection;

public sealed class ProjectionDeterminismTests
{
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    [Fact]
    [Trait("Requirement", "RP-46")]
    public async Task AnalyzeAsync_SameSolutionTwice_ProducesByteIdenticalProjectionArtifacts()
    {
        var first = await PublishAsync(AcmeOrdersSolutionPath());
        var second = await PublishAsync(AcmeOrdersSolutionPath());

        Assert.Equal(PublicationStatus.Committed, first.Outcome.Status);
        Assert.Equal(PublicationStatus.Committed, second.Outcome.Status);
        AssertEqualProjectionBytes(first.Publication, second.Publication);
    }

    [Fact]
    [Trait("Requirement", "RP-46")]
    public async Task AnalyzeAsync_SameSolutionTwice_PublishesNonEmptyCatalogsAndPostings()
    {
        var first = await PublishAsync(AcmeOrdersSolutionPath());
        var second = await PublishAsync(AcmeOrdersSolutionPath());

        var keys = ProjectionKeys(first.Publication);
        Assert.Contains(keys, key => key.StartsWith("catalogs/", StringComparison.Ordinal));
        Assert.Contains(keys, key => key.StartsWith("postings/", StringComparison.Ordinal));
        Assert.Equal(keys, ProjectionKeys(second.Publication));
    }

    [Fact]
    [Trait("Requirement", "RP-47")]
    public async Task AnalyzeAsync_TwoFixtureClones_ProduceByteIdenticalProjectionArtifacts()
    {
        var fixture = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
        Assert.True(Directory.Exists(fixture), $"Expected fixture at '{fixture}'.");

        var tree = Directory.CreateTempSubdirectory("csharp2md-projection-clone-");
        try
        {
            var cloneA = Path.Combine(tree.FullName, "clone-a");
            var cloneB = Path.Combine(tree.FullName, "clone-b");
            CopyClone(fixture, cloneA);
            CopyClone(fixture, cloneB);

            var rootA = Path.GetFullPath(cloneA);
            var rootB = Path.GetFullPath(cloneB);
            Assert.NotEqual(rootA, rootB);

            var publicationA = await AnalyzeCloneAsync(cloneA);
            var publicationB = await AnalyzeCloneAsync(cloneB);
            AssertEqualProjectionBytes(publicationA, publicationB);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-47")]
    public async Task AnalyzeAsync_TwoFixtureClones_ProjectionArtifactsContainNoClonePath()
    {
        var fixture = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
        Assert.True(Directory.Exists(fixture), $"Expected fixture at '{fixture}'.");

        var tree = Directory.CreateTempSubdirectory("csharp2md-projection-clone-path-");
        try
        {
            var cloneA = Path.Combine(tree.FullName, "clone-a");
            var cloneB = Path.Combine(tree.FullName, "clone-b");
            CopyClone(fixture, cloneA);
            CopyClone(fixture, cloneB);

            var publicationA = await AnalyzeCloneAsync(cloneA);
            var publicationB = await AnalyzeCloneAsync(cloneB);
            AssertNoClonePath(publicationA, Path.GetFullPath(cloneA));
            AssertNoClonePath(publicationB, Path.GetFullPath(cloneB));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-48")]
    public async Task AnalyzeAsync_ReversedCSharpDocuments_ProduceByteIdenticalProjectionArtifacts()
    {
        var forward = await ProjectWithDocumentOrderAsync(reverse: false);
        var reversed = await ProjectWithDocumentOrderAsync(reverse: true);

        AssertEqualProjectionBytes(forward, reversed);
    }

    [Fact]
    [Trait("Requirement", "RP-48")]
    public async Task AnalyzeAsync_ReversedCSharpDocuments_PreserveCatalogAndPostingKeys()
    {
        var forward = await ProjectWithDocumentOrderAsync(reverse: false);
        var reversed = await ProjectWithDocumentOrderAsync(reverse: true);

        var forwardKeys = ProjectionKeys(forward);
        Assert.Contains(forwardKeys, key => key.StartsWith("catalogs/", StringComparison.Ordinal));
        Assert.Contains(forwardKeys, key => key.StartsWith("postings/", StringComparison.Ordinal));
        Assert.Equal(forwardKeys, ProjectionKeys(reversed));
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

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> PublishAsync(
        string solutionPath)
    {
        var store = new InMemoryTransactionalStore(new PackageProjector());
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        var outcome = Assert.Single(result.Solutions);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return (outcome, publication);
    }

    private static async Task<CommittedPublication> AnalyzeCloneAsync(string cloneRoot)
    {
        var solutionPath = Path.Combine(cloneRoot, "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected cloned solution at '{solutionPath}'.");
        var (_, publication) = await PublishAsync(solutionPath);
        return publication;
    }

    private static async Task<CommittedPublication> ProjectWithDocumentOrderAsync(bool reverse)
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var store = new InMemoryTransactionalStore(new PackageProjector());
        var reader = new FilesystemSourceDocumentReader();
        var session = store.Open(Path.GetFullPath(solutionPath), reader);
        var context = new PipelineContext(session, solutionPath) { SourceDocumentReader = reader };
        try
        {
            var stages = PipelineStages.CreateDefault();
            for (var index = 0; index < stages.Length; index++)
            {
                if (index == 1 && reverse)
                {
                    Assert.True(
                        context.CSharpDocuments.Length >= 2,
                        $"Expected at least two documents to reverse, found {context.CSharpDocuments.Length}.");
                    var originalFirst = context.CSharpDocuments[0].RelativePath;
                    context.CSharpDocuments = context.CSharpDocuments.Reverse().ToImmutableArray();
                    Assert.NotEqual(originalFirst, context.CSharpDocuments[0].RelativePath);
                }

                var result = await stages[index].ExecuteAsync(context, CancellationToken.None);
                Assert.False(result.StructuralCorruption, context.Detail);
            }

            return session.Commit();
        }
        catch
        {
            session.Abort();
            throw;
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    private static void AssertEqualProjectionBytes(CommittedPublication left, CommittedPublication right)
    {
        var leftArtifacts = ProjectionArtifacts(left);
        var rightArtifacts = ProjectionArtifacts(right);
        Assert.NotEmpty(leftArtifacts);
        Assert.Equal(
            leftArtifacts.Select(static pair => pair.Key),
            rightArtifacts.Select(static pair => pair.Key));
        for (var index = 0; index < leftArtifacts.Length; index++)
        {
            Assert.True(
                leftArtifacts[index].Bytes.AsSpan().SequenceEqual(rightArtifacts[index].Bytes.AsSpan()),
                $"Projection bytes at '{leftArtifacts[index].Key}' differ.");
        }
    }

    private static void AssertNoClonePath(CommittedPublication publication, string cloneRoot)
    {
        var slash = cloneRoot.Replace('\\', '/');
        var jsonEscaped = cloneRoot.Replace("\\", "\\\\", StringComparison.Ordinal);
        foreach (var artifact in ProjectionArtifacts(publication))
        {
            var text = Encoding.UTF8.GetString(artifact.Bytes.AsSpan());
            Assert.DoesNotContain(cloneRoot, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(slash, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(jsonEscaped, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(cloneRoot, artifact.Key, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string[] ProjectionKeys(CommittedPublication publication) =>
        ProjectionArtifacts(publication).Select(static pair => pair.Key).ToArray();

    private static (string Key, ImmutableArray<byte> Bytes)[] ProjectionArtifacts(CommittedPublication publication) =>
        publication.ArtifactsInPublicationOrder
            .Where(static fragment => IsProjection(fragment.CanonicalKey))
            .Select(static fragment => (fragment.CanonicalKey, BytesOf(fragment)))
            .OrderBy(static pair => pair.CanonicalKey, StringComparer.Ordinal)
            .ToArray();

    private static ImmutableArray<byte> BytesOf(StagedFragment fragment) =>
        fragment.IsDeferred ? fragment.ReadPayload() : fragment.Payload;

    private static bool IsProjection(string key) =>
        key.StartsWith("catalogs/", StringComparison.Ordinal)
        || key.StartsWith("postings/", StringComparison.Ordinal)
        || key.StartsWith("markdown/", StringComparison.Ordinal)
        || key.StartsWith("source/", StringComparison.Ordinal)
        || key is "retrieval.md" or "AGENTS.md";

    private static void CopyClone(string fixture, string cloneRoot)
    {
        Directory.CreateDirectory(cloneRoot);
        File.Copy(
            Path.Combine(AnalysisTestPaths.RepoRoot, "Directory.Packages.props"),
            Path.Combine(cloneRoot, "Directory.Packages.props"));
        File.Copy(
            Path.Combine(AnalysisTestPaths.RepoRoot, "NuGet.Config"),
            Path.Combine(cloneRoot, "NuGet.Config"));
        File.WriteAllText(
            Path.Combine(cloneRoot, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
            </Project>
            """);

        var destination = Path.Combine(cloneRoot, "SyntheticSolution");
        foreach (var file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(fixture, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(ExcludedSegments.Contains))
            {
                continue;
            }

            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
