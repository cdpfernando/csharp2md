using System.Xml.Linq;

namespace Csharp2Md.Cli.Tests.Isolation;

public sealed class CliIsolationTests
{
    private static readonly string[] ExpectedProjectReferences =
    [
        "Csharp2Md.Analysis",
        "Csharp2Md.Projection",
        "Csharp2Md.Storage",
    ];

    [Fact]
    [Trait("Requirement", "ENG-08")]
    public void CliCsproj_ProjectReferencesEqualAnalysisStorageAndProjection()
    {
        var names = ReadProjectReferenceNames()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.DoesNotContain("Csharp2Md.Domain", names);
        Assert.DoesNotContain("Csharp2Md.Core", names);
        Assert.Equal(ExpectedProjectReferences, names);
    }

    [Fact]
    [Trait("Requirement", "ENG-02")]
    public void CliCsproj_PacksAsCsharp2MdDotnetToolTargetingNet10()
    {
        var document = LoadCliCsproj();

        Assert.Equal("net10.0", ElementValue(document, "TargetFramework"));
        Assert.Equal("true", ElementValue(document, "PackAsTool"), StringComparer.OrdinalIgnoreCase);
        Assert.Equal("csharp2md", ElementValue(document, "ToolCommandName"));
    }

    [Fact]
    [Trait("Requirement", "ENG-02")]
    public void Slnx_ListsCliUnderSrc()
    {
        var slnxPath = Path.Combine(CliTestPaths.RepoRoot, "csharp2md.slnx");
        Assert.True(File.Exists(slnxPath), $"Solution file was not found at '{slnxPath}'.");

        var document = XDocument.Load(slnxPath);
        var srcFolder = document.Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "Folder"
                && element.Attribute("Name")?.Value == "/src/");

        Assert.True(srcFolder is not null, "csharp2md.slnx has no Folder named '/src/'.");

        var srcPaths = srcFolder.Elements()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .OfType<string>()
            .ToArray();

        Assert.Contains("src/Csharp2Md.Cli/Csharp2Md.Cli.csproj", srcPaths);
    }

    private static IReadOnlyList<string> ReadProjectReferenceNames() =>
        LoadCliCsproj()
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(
                include!.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)))
            .ToArray();

    private static XDocument LoadCliCsproj()
    {
        var csprojPath = Path.Combine(
            CliTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Cli",
            "Csharp2Md.Cli.csproj");
        Assert.True(File.Exists(csprojPath), $"CLI project file was not found at '{csprojPath}'.");
        return XDocument.Load(csprojPath);
    }

    private static string ElementValue(XDocument document, string localName)
    {
        var value = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == localName)
            ?.Value;
        Assert.False(string.IsNullOrWhiteSpace(value), $"CLI csproj is missing <{localName}>.");
        return value!;
    }
}
