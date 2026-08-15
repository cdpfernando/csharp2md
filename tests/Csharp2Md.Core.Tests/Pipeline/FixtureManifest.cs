using System.Text.Json;
using Csharp2Md.Core.Manifests;

namespace Csharp2Md.Core.Tests.Pipeline;

/// <summary>
/// Writes manifests pointing at the synthetic fixture.
/// </summary>
/// <remarks>
/// The default shape uses a manifest boundary override (P1-04) so each fixture service owns exactly
/// its own project. Without it, <c>Acme.Orders.slnx</c> — which T3 deliberately extended to bundle
/// the other services' projects for load-health testing — would make two services claim the same
/// project file. <see cref="WriteRoots"/> keeps the automatic <c>.sln</c> heuristic (P1-02) for the
/// tests that need it.
/// </remarks>
internal static class FixtureManifest
{
    public static string WriteOverrides(string directory, params string[] serviceNames)
    {
        var entries = serviceNames
            .Select(name => new ManifestEntry(
                TestPaths.SyntheticSolution(name),
                name,
                [TestPaths.SyntheticSolution(Path.Combine(name, name + ".csproj"))]))
            .ToList();

        return Write(directory, new Manifest(entries));
    }

    public static string WriteRoots(string directory, params string[] serviceNames)
    {
        var entries = serviceNames
            .Select(name => new ManifestEntry(TestPaths.SyntheticSolution(name)))
            .ToList();

        return Write(directory, new Manifest(entries));
    }

    public static string Write(string directory, Manifest manifest)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, ManifestJsonContext.Default.Manifest));

        return path;
    }

    /// <summary>Source files a run is expected to mirror: everything but build output (spec Assumptions).</summary>
    public static IReadOnlyList<string> ExpectedSourceFiles(string serviceRoot) =>
        Directory.EnumerateFiles(serviceRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(serviceRoot, path).Replace('\\', '/'))
            .Where(relative => !relative.Split('/').SkipLast(1).Any(segment => segment is "bin" or "obj"))
            .Where(relative => !relative.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
                && !relative.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(relative => relative, StringComparer.Ordinal)
            .ToList();
}
