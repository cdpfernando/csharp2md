using System.Reflection;
using System.Xml.Linq;

namespace Csharp2Md.Cli.Tests;

public sealed class CliAssemblyTests
{
    [Fact]
    [Trait("Requirement", "ENG-02")]
    public void CliAssembly_HasNameCsharp2MdCliAndANonNullEntryPoint()
    {
        var assembly = Assembly.Load("Csharp2Md.Cli");

        Assert.Equal("Csharp2Md.Cli", assembly.GetName().Name);
        Assert.NotNull(assembly.EntryPoint);
    }

    [Fact]
    [Trait("Requirement", "ENG-02")]
    public void CliTestsCsproj_ReferencesCsharp2MdCli()
    {
        var csprojPath = Path.Combine(
            CliTestPaths.RepoRoot,
            "tests",
            "Csharp2Md.Cli.Tests",
            "Csharp2Md.Cli.Tests.csproj");

        Assert.True(File.Exists(csprojPath), $"CLI test project file was not found at '{csprojPath}'.");

        var includes = XDocument.Load(csprojPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)))
            .ToArray();

        Assert.Contains("Csharp2Md.Cli", includes);
    }
}
