using System.Security.Cryptography;
using Csharp2Md.Analysis.Storage;
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
    public async Task Analyze_WritesSchemaValidEmptyPackageOnlyUnderOutput()
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
            Assert.Equal(FactualSnapshot.Empty, result.Snapshot);
            Assert.True(result.Snapshot.Facts.IsEmpty);
            Assert.True(result.Snapshot.Observations.IsEmpty);
            Assert.True(result.Snapshot.ConfirmedRelations.IsEmpty);
            Assert.True(result.Snapshot.Candidates.IsEmpty);
            Assert.True(result.Snapshot.Unresolved.IsEmpty);
            Assert.True(result.Snapshot.Frontiers.IsEmpty);

            var registryPath = Path.Combine(child, "contracts", "taxonomy-registry.json");
            Assert.True(File.Exists(registryPath), $"taxonomy-registry copy was not found at '{registryPath}'.");
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(CliTestPaths.RepoRoot, "contracts", "taxonomy-registry.json")),
                File.ReadAllBytes(registryPath));

            var manifestPath = Path.Combine(child, "manifest.json");
            Assert.True(File.Exists(manifestPath), $"manifest was not found at '{manifestPath}'.");
            var manifest = CanonicalJson.Read<ManifestEnvelope>(File.ReadAllBytes(manifestPath));
            Assert.All(manifest.Artifacts, static entry => Assert.Equal(0, entry.Count));
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
