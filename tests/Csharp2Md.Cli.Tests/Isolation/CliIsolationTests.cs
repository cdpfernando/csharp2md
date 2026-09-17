using System.Xml.Linq;

namespace Csharp2Md.Cli.Tests.Isolation;

public sealed class CliIsolationTests
{
    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void CliCsproj_PacksAsCsharp2MdDotnetToolTargetingNet10()
    {
        var document = LoadCliCsproj();

        Assert.Equal("net10.0", ElementValue(document, "TargetFramework"));
        Assert.Equal("true", ElementValue(document, "PackAsTool"), StringComparer.OrdinalIgnoreCase);
        Assert.Equal("csharp2md", ElementValue(document, "ToolCommandName"));
        Assert.Equal("csharp2md", ElementValue(document, "PackageId"));
    }

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
