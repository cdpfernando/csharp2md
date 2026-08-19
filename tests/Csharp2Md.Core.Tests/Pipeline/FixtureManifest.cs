using System.Text.Json;
using Csharp2Md.Core.Manifests;

namespace Csharp2Md.Core.Tests.Pipeline;

internal static class FixtureManifest
{
    public static string WriteOverrides(string directory, params string[] serviceNames) =>
        Write(directory, new Manifest(serviceNames.Select(name => new ManifestEntry(
            TestPaths.SyntheticSolution(name),
            name,
            [TestPaths.SyntheticSolution(Path.Combine(name, name + ".csproj"))])).ToList()));

    public static string WriteRoots(string directory, params string[] serviceNames) =>
        Write(directory, new Manifest(serviceNames.Select(name => new ManifestEntry(TestPaths.SyntheticSolution(name))).ToList()));

    public static string Write(string directory, Manifest manifest)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, ManifestJsonContext.Default.Manifest));
        return path;
    }
}
