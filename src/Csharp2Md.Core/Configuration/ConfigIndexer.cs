using System.Text.Json;
using Csharp2Md.Core.Discovery;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Csharp2Md.Core.Configuration;

public static class ConfigIndexer
{
    public static ConfigIndexResult Index(ServiceCatalog catalog)
    {
        var logicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var composeServiceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        foreach (var service in catalog.Services)
        {
            foreach (var path in FindConfigFiles(service.RootPath, "appsettings*.json"))
            {
                IndexAppSettings(path, logicalNames, warnings);
            }

            foreach (var path in FindConfigFiles(service.RootPath, "docker-compose.yml"))
            {
                IndexDockerCompose(path, composeServiceNames, warnings);
            }
        }

        return new ConfigIndexResult(new ConfigIndex(logicalNames, composeServiceNames), warnings);
    }

    private static IEnumerable<string> FindConfigFiles(string root, string searchPattern) =>
        Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories)
            .Where(path => !IsUnderBuildOutput(root, path));

    private static bool IsUnderBuildOutput(string root, string path) =>
        Path.GetRelativePath(root, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    private static void IndexAppSettings(string path, Dictionary<string, string> logicalNames, List<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("Services", out var services)
                || services.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in services.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    logicalNames[property.Name] = property.Value.GetString()!;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            warnings.Add($"Skipped malformed appsettings file '{path}': {ex.Message}");
        }
    }

    private static void IndexDockerCompose(string path, HashSet<string> composeServiceNames, List<string> warnings)
    {
        try
        {
            var deserializer = new Deserializer();
            var document = deserializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));

            if (document is not null
                && document.TryGetValue("services", out var servicesObj)
                && servicesObj is Dictionary<object, object> servicesMap)
            {
                foreach (var name in servicesMap.Keys.OfType<string>())
                {
                    composeServiceNames.Add(name);
                }
            }
        }
        catch (Exception ex) when (ex is YamlException or IOException)
        {
            warnings.Add($"Skipped malformed docker-compose file '{path}': {ex.Message}");
        }
    }
}
