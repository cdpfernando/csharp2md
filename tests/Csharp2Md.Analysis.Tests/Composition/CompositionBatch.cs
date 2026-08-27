using System.Text.Json.Nodes;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection;
using Csharp2Md.Projection.Composition;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Composition;

internal static class CompositionBatch
{
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    internal static string OrdersSolutionPath() => FixtureSolution("Acme.Orders", "Acme.Orders.slnx");

    internal static string ShippingSolutionPath() => FixtureSolution("Acme.Shipping", "Acme.Shipping.slnx");

    internal static string FixtureSolution(string folder, string file)
    {
        var path = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution", folder, file);
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    internal static IReadOnlyDictionary<string, byte[]> SnapshotFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(directory, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);
    }

    internal static void AssertEqualSnapshots(
        IReadOnlyDictionary<string, byte[]> left,
        IReadOnlyDictionary<string, byte[]> right)
    {
        Assert.Equal(left.Keys.Order(StringComparer.Ordinal), right.Keys.Order(StringComparer.Ordinal));
        foreach (var key in left.Keys)
        {
            Assert.True(left[key].AsSpan().SequenceEqual(right[key]), $"Bytes at '{key}' changed.");
        }
    }

    internal static async Task<BatchRun> AnalyzeAsync(string outputRoot, params string[] solutionPaths)
    {
        Directory.CreateDirectory(outputRoot);
        var store = new FilesystemTransactionalStore(outputRoot, new PackageProjector(), new BatchComposer());
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([.. solutionPaths]),
            CancellationToken.None);
        var files = SnapshotFiles(outputRoot);
        Assert.True(files.ContainsKey("batch-manifest.json"), "Expected published batch-manifest.json.");
        return new BatchRun(outputRoot, result, files);
    }

    internal static JsonArray ReadArray(IReadOnlyDictionary<string, byte[]> files, string relativePath)
    {
        Assert.True(files.TryGetValue(relativePath, out var bytes), $"Expected published '{relativePath}'.");
        var node = JsonNode.Parse(bytes);
        var array = Assert.IsType<JsonArray>(node);
        return array;
    }

    internal static JsonNode ElementAt(IReadOnlyDictionary<string, byte[]> package, string artifactKey, int ordinal)
    {
        Assert.True(package.TryGetValue(artifactKey, out var bytes), $"Missing cited artifact '{artifactKey}'.");
        var node = JsonNode.Parse(bytes);
        Assert.NotNull(node);
        switch (node)
        {
            case JsonArray array:
                Assert.InRange(ordinal, 0, array.Count - 1);
                return array[ordinal]!;
            case JsonObject obj:
                var items = new List<JsonNode?>();
                foreach (var property in obj)
                {
                    if (property.Value is JsonArray family)
                    {
                        items.AddRange(family);
                    }
                }

                Assert.InRange(ordinal, 0, items.Count - 1);
                return items[ordinal]!;
            default:
                Assert.Equal(0, ordinal);
                return node;
        }
    }

    internal static string Text(JsonNode node, params string[] path)
    {
        JsonNode? current = node;
        foreach (var segment in path)
        {
            current = current is JsonObject obj && obj.TryGetPropertyValue(segment, out var next)
                ? next
                : null;
            if (current is null)
            {
                return string.Empty;
            }
        }

        return current is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
    }

    internal static IReadOnlyDictionary<string, byte[]> PackageFiles(
        IReadOnlyDictionary<string, byte[]> output,
        string packageDirectory)
    {
        var prefix = packageDirectory.TrimEnd('/') + "/";
        return output
            .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(
                pair => pair.Key[prefix.Length..],
                pair => pair.Value,
                StringComparer.Ordinal);
    }

    internal static void CopyFixtureClone(string destination)
    {
        var fixture = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
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

        var tree = Path.Combine(destination, "SyntheticSolution");
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

    internal sealed record BatchRun(
        string OutputRoot,
        AnalysisResult Result,
        IReadOnlyDictionary<string, byte[]> Files);
}
