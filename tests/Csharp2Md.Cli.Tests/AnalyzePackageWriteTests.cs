using System.Security.Cryptography;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Cli.Tests;

public sealed class AnalyzePackageWriteTests
{
    private static readonly string[] SnapshotRoots = ["src", "tests", "contracts", "fixtures"];
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "TestResults",
    };

    [Fact]
    [Trait("Requirement", "STOR-49")]
    [Trait("Requirement", "STOR-50")]
    [Trait("Requirement", "STOR-51")]
    public async Task Analyze_WritesSchemaValidPackageOnlyUnderOutput()
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(Path.Exists(solutionPath), $"Fixture solution was not found at '{solutionPath}'.");

        var outputPath = Path.Combine(
            CliTestPaths.RepoRoot,
            "fixtures",
            "csharp2md-analyze-out-" + Guid.NewGuid().ToString("N"));

        try
        {
            var before = FileSetHash(outputPath);
            var (exitCode, _, _) = await CliInvoke.RunAsync(
                ["analyze", "--solution", solutionPath, "--output", outputPath]);
            var after = FileSetHash(outputPath);

            Assert.Equal(0, exitCode);
            Assert.Equal(before, after);

            var child = Assert.Single(Directory.GetDirectories(outputPath));
            Assert.Matches("^s-[0-9a-f]{32}$", Path.GetFileName(child));

            var result = FactualPackageReader.Read(child);
            Assert.NotEqual(FactualSnapshot.Empty, result.Snapshot);
            Assert.Contains(result.Snapshot.Facts, static fact => fact is Solution);
            Assert.Contains(result.Snapshot.Facts, static fact => fact is Project);
            Assert.Contains(result.Snapshot.Facts, static fact => fact is Document);
            Assert.Contains(result.Snapshot.Facts, static fact => fact is Symbol);
            Assert.NotEmpty(result.Snapshot.Observations);
            Assert.Contains(
                result.Snapshot.ConfirmedRelations,
                static relation => relation.Kind is RelationKind.Contains);
            Assert.Contains(
                result.Snapshot.Candidates,
                static link => link.Kind is RelationKind.Targets);
            Assert.DoesNotContain(
                result.Snapshot.ConfirmedRelations,
                static relation => relation.Kind is RelationKind.Targets);
            Assert.Contains(
                result.Snapshot.Frontiers,
                static frontier => frontier.Frontier is Frontier.Open
                    && frontier.Cause is FrontierCause.FurtherContinuationObserved);

            var registryPath = Path.Combine(child, "contracts", "taxonomy-registry.json");
            Assert.True(File.Exists(registryPath), $"taxonomy-registry copy was not found at '{registryPath}'.");
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(CliTestPaths.RepoRoot, "contracts", "taxonomy-registry.json")),
                File.ReadAllBytes(registryPath));

            var manifestPath = Path.Combine(child, "manifest.json");
            Assert.True(File.Exists(manifestPath), $"manifest was not found at '{manifestPath}'.");
            var manifest = CanonicalJson.Read<ManifestEnvelope>(File.ReadAllBytes(manifestPath));
            Assert.Contains(manifest.Artifacts, static entry => entry.CanonicalKey == "facts/structural" && entry.Count > 0);
            Assert.Contains(
                manifest.Artifacts,
                static entry => entry.CanonicalKey.StartsWith("observations/", StringComparison.Ordinal) && entry.Count > 0);
            Assert.Contains(
                manifest.Artifacts,
                static entry => entry.CanonicalKey == "relations/confirmed/contains" && entry.Count > 0);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    private static string FileSetHash(string outputPath)
    {
        var repo = CliTestPaths.RepoRoot;
        var outputRelative = Path.GetRelativePath(repo, outputPath).Replace('\\', '/');
        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var relative in EnumerateSnapshotFiles(repo, outputRelative))
        {
            incremental.AppendData(System.Text.Encoding.UTF8.GetBytes(relative));
            incremental.AppendData([0]);
            incremental.AppendData(File.ReadAllBytes(Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar))));
            incremental.AppendData([0]);
        }

        return Convert.ToHexString(incremental.GetCurrentHash());
    }

    private static IReadOnlyList<string> EnumerateSnapshotFiles(string repo, string outputRelative)
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

                var normalized = string.Join('/', segments);
                if (IsUnderOutput(normalized, outputRelative))
                {
                    continue;
                }

                files.Add(normalized);
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static bool IsUnderOutput(string relative, string outputRelative)
    {
        return relative.Equals(outputRelative, StringComparison.OrdinalIgnoreCase)
            || relative.StartsWith(outputRelative + "/", StringComparison.OrdinalIgnoreCase);
    }
}
