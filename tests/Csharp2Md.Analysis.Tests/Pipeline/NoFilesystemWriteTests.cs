using System.Security.Cryptography;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class NoFilesystemWriteTests
{
    private static readonly string[] SnapshotRoots = ["src", "tests", "contracts", "fixtures"];
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "TestResults",
    };

    [Fact]
    [Trait("Requirement", "ENG-16")]
    [Trait("Requirement", "STOR-24")]
    public async Task AnalyzeAsync_CompletedStubRun_DoesNotChangeTheWorkingTree()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var before = FileSetHash();
        var store = new InMemoryTransactionalStore();
        var engine = new AnalysisEngine(store);

        var result = await engine.AnalyzeAsync(AnalysisRequest.Create([solutionPath]), CancellationToken.None);

        var after = FileSetHash();
        Assert.Equal(before, after);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.False(result.HasUnpublishedSolution);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        Assert.Equal(ArtifactRole.Manifest, publication.ArtifactsInPublicationOrder[^1].Role);
    }

    private static string FileSetHash()
    {
        var repo = AnalysisTestPaths.RepoRoot;
        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var relative in EnumerateSnapshotFiles(repo))
        {
            incremental.AppendData(System.Text.Encoding.UTF8.GetBytes(relative));
            incremental.AppendData([0]);
            incremental.AppendData(File.ReadAllBytes(Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar))));
            incremental.AppendData([0]);
        }

        return Convert.ToHexString(incremental.GetCurrentHash());
    }

    private static IReadOnlyList<string> EnumerateSnapshotFiles(string repo)
    {
        var files = new List<string>();
        foreach (var root in SnapshotRoots)
        {
            var fullRoot = Path.Combine(repo, root);
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(repo, file);
                var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (segments.Any(ExcludedSegments.Contains))
                {
                    continue;
                }

                files.Add(string.Join('/', segments));
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }
}
