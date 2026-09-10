using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-117: guards `fixtures/SyntheticSolution` against silent change. AD-026 amends the standing
/// "only one versioned analysis fixture" constraint to admit `fixtures/CertificationCorpus`
/// specifically because SyntheticSolution must stay byte-identical — every prior workstream's tests
/// assert against it. This test hashes every tracked file under the tree (approximated, like the
/// existing clone-path-independence determinism test, by walking the filesystem and excluding the
/// standard untracked build directories) and fails, naming the differing files, if that tree ever
/// changes.
/// </summary>
public sealed class SyntheticSolutionImmutabilityTests
{
    private const string ExpectedDigest = "40dd18b021d265ff9a94f1b99523770848a9cd6dc10bc6fccf321fd65d078c86";

    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    [Fact]
    [Trait("Requirement", "GCPC-117")]
    public void Digest_SyntheticSolutionTree_MatchesTheCommittedManifestAndDigest()
    {
        var root = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
        Assert.True(Directory.Exists(root), $"Expected fixture at '{root}'.");

        var actual = ComputeManifest(root);
        var expected = LoadExpectedManifest();

        var added = actual.Keys.Except(expected.Keys, StringComparer.Ordinal).OrderBy(static path => path, StringComparer.Ordinal).ToArray();
        var removed = expected.Keys.Except(actual.Keys, StringComparer.Ordinal).OrderBy(static path => path, StringComparer.Ordinal).ToArray();
        var changed = actual.Keys
            .Intersect(expected.Keys, StringComparer.Ordinal)
            .Where(path => !string.Equals(actual[path], expected[path], StringComparison.Ordinal))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        if (added.Length > 0 || removed.Length > 0 || changed.Length > 0)
        {
            Assert.Fail(
                "fixtures/SyntheticSolution changed since the committed manifest. "
                + $"Added: [{string.Join(", ", added)}]. "
                + $"Removed: [{string.Join(", ", removed)}]. "
                + $"Changed: [{string.Join(", ", changed)}].");
        }

        Assert.Equal(ExpectedDigest, ComputeDigest(actual));
    }

    private static SortedDictionary<string, string> ComputeManifest(string root)
    {
        var manifest = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in EnumerateTrackedFiles(root))
        {
            var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            manifest[relative] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        }

        return manifest;
    }

    private static IEnumerable<string> EnumerateTrackedFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => ExcludedSegments.Contains(segment)));

    private static string ComputeDigest(IReadOnlyDictionary<string, string> manifest)
    {
        var builder = new StringBuilder();
        foreach (var path in manifest.Keys.OrderBy(static path => path, StringComparer.Ordinal))
        {
            builder.Append(path).Append('\n').Append(manifest[path]).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static Dictionary<string, string> LoadExpectedManifest()
    {
        var manifestPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "tests",
            "Csharp2Md.Analysis.Tests",
            "Fixtures",
            "SyntheticSolutionManifest.json");
        Assert.True(File.Exists(manifestPath), $"Expected committed manifest at '{manifestPath}'.");

        using var stream = File.OpenRead(manifestPath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"'{manifestPath}' did not deserialize to a manifest.");
    }
}
