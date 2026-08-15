using System.Text.Json;

namespace Csharp2Md.Core.Manifests;

public static class ManifestLoader
{
    public static ManifestLoadResult Load(string path)
    {
        if (!File.Exists(path))
        {
            return ManifestLoadResult.Failed(
                new ManifestError(ManifestErrorCode.FileMissing, $"Manifest file not found: {path}"));
        }

        var json = File.ReadAllText(path);

        Manifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(json, ManifestJsonContext.Default.Manifest);
        }
        catch (JsonException ex)
        {
            return ManifestLoadResult.Failed(
                new ManifestError(ManifestErrorCode.MalformedJson, $"Manifest is not valid JSON: {ex.Message}"));
        }

        if (manifest is null || manifest.Services.Count == 0)
        {
            return ManifestLoadResult.Failed(
                new ManifestError(ManifestErrorCode.ZeroEntries, "Manifest contains zero service entries."));
        }

        return ManifestLoadResult.Success(manifest);
    }
}
