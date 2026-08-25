using System.Reflection;
using System.Xml.Linq;
using Csharp2Md.Storage;

namespace Csharp2Md.Storage.Tests.Isolation;

public sealed class StorageIsolationTests
{
    private static readonly string[] ForbiddenTypeNameTokens =
    [
        "Classifier",
        "Promoter",
        "Extractor",
    ];

    private static Assembly StorageAssembly => typeof(AssemblyMarker).Assembly;

    [Fact]
    [Trait("Requirement", "STOR-11")]
    public void StorageCsproj_DeclaresProjectReferenceToDomain()
    {
        var csprojPath = StorageCsprojPath();

        Assert.True(File.Exists(csprojPath), $"Storage project file was not found at '{csprojPath}'.");

        var domainReference = ReadIncludes(csprojPath, "ProjectReference")
            .FirstOrDefault(include => ReferencesProject(include, "Csharp2Md.Domain"));

        Assert.False(
            string.IsNullOrWhiteSpace(domainReference),
            "Csharp2Md.Storage must declare a project reference to Csharp2Md.Domain.");
    }

    [Fact]
    [Trait("Requirement", "STOR-11")]
    public void StorageCsproj_EmbedsTaxonomyRegistry()
    {
        var csprojPath = StorageCsprojPath();

        Assert.True(File.Exists(csprojPath), $"Storage project file was not found at '{csprojPath}'.");

        var embed = ReadIncludes(csprojPath, "EmbeddedResource")
            .FirstOrDefault(include =>
                include.Replace('\\', '/').EndsWith("taxonomy-registry.json", StringComparison.Ordinal));

        Assert.False(
            string.IsNullOrWhiteSpace(embed),
            "Csharp2Md.Storage must embed contracts/taxonomy-registry.json as an EmbeddedResource.");
    }

    [Fact]
    [Trait("Requirement", "STOR-12")]
    public void PublicOrInternalSurface_DoesNotDeclareClassifierPromoterOrExtractorType()
    {
        var offending = StorageAssembly.GetTypes()
            .Where(IsPublicOrInternal)
            .FirstOrDefault(type => ForbiddenTypeNameTokens.Any(token =>
                type.Name.Contains(token, StringComparison.Ordinal)));

        Assert.True(
            offending is null,
            $"Storage type '{offending?.FullName}' classifies, promotes or extracts facts.");
    }

    private static string StorageCsprojPath() =>
        Path.Combine(
            StorageTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Storage",
            "Csharp2Md.Storage.csproj");

    private static IReadOnlyList<string> ReadIncludes(string csprojPath, string elementName)
    {
        var document = XDocument.Load(csprojPath);
        return document.Descendants()
            .Where(element => element.Name.LocalName == elementName)
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Cast<string>()
            .ToArray();
    }

    private static bool ReferencesProject(string include, string projectName)
    {
        var normalized = include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return string.Equals(Path.GetFileNameWithoutExtension(normalized), projectName, StringComparison.Ordinal);
    }

    private static bool IsPublicOrInternal(Type type) =>
        !type.IsNested || type.IsNestedPublic || type.IsNestedAssembly || type.IsNestedFamORAssem;
}
