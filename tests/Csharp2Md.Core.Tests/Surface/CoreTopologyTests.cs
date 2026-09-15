using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Csharp2Md.Core;

namespace Csharp2Md.Core.Tests.Surface;

public sealed class CoreTopologyTests
{
    private static readonly string[] LegacySrcProjects =
    [
        "src/Csharp2Md.Analysis/Csharp2Md.Analysis.csproj",
        "src/Csharp2Md.Cli/Csharp2Md.Cli.csproj",
        "src/Csharp2Md.Domain/Csharp2Md.Domain.csproj",
        "src/Csharp2Md.Projection/Csharp2Md.Projection.csproj",
        "src/Csharp2Md.Storage/Csharp2Md.Storage.csproj",
    ];

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Core_TargetsNet10AndReferencesRoslynWorkspaces560()
    {
        var core = LoadCsproj("src", "Csharp2Md.Core", "Csharp2Md.Core.csproj");
        var packages = LoadDirectoryPackages();

        Assert.Equal("net10.0", ElementValue(core, "TargetFramework"));
        Assert.Contains(
            "Microsoft.CodeAnalysis.Workspaces.MSBuild",
            PackageReferenceNames(core),
            StringComparer.Ordinal);
        Assert.Equal("5.6.0", PackageVersion(packages, "Microsoft.CodeAnalysis.Workspaces.MSBuild"));

        var framework = typeof(AssemblyMarker).Assembly.GetCustomAttribute<TargetFrameworkAttribute>();
        Assert.Equal(".NETCoreApp,Version=v10.0", framework?.FrameworkName);
    }

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Core_DoesNotReferenceMicrosoftBuildPackages()
    {
        var core = LoadCsproj("src", "Csharp2Md.Core", "Csharp2Md.Core.csproj");
        var forbidden = PackageReferenceNames(core)
            .Where(name => name.StartsWith("Microsoft.Build", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            forbidden.Length == 0,
            "Csharp2Md.Core must not PackageReference Microsoft.Build.*: " + string.Join(", ", forbidden));
    }

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Cli_ReferencesCoreAndKeepsLegacyProjectReferences()
    {
        var names = ProjectReferenceNames(LoadCsproj("src", "Csharp2Md.Cli", "Csharp2Md.Cli.csproj"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("Csharp2Md.Core", names);
        Assert.Contains("Csharp2Md.Analysis", names);
        Assert.Contains("Csharp2Md.Storage", names);
        Assert.Contains("Csharp2Md.Projection", names);
    }

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Core_ExposesInternalsOnlyToCoreTests()
    {
        var friends = InternalsVisibleToNames(LoadCsproj("src", "Csharp2Md.Core", "Csharp2Md.Core.csproj"));
        Assert.Equal(["Csharp2Md.Core.Tests"], friends);

        var attributes = typeof(AssemblyMarker).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .ToArray();
        Assert.Equal(["Csharp2Md.Core.Tests"], attributes);
    }

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void CoreTests_UseExistingXunitAndVerifyStack()
    {
        var tests = LoadCsproj("tests", "Csharp2Md.Core.Tests", "Csharp2Md.Core.Tests.csproj");
        var packages = PackageReferenceNames(tests);

        Assert.Equal("net10.0", ElementValue(tests, "TargetFramework"));
        Assert.Contains("xunit", packages, StringComparer.Ordinal);
        Assert.Contains("Verify.Xunit", packages, StringComparer.Ordinal);
        Assert.Contains("Microsoft.NET.Test.Sdk", packages, StringComparer.Ordinal);
        Assert.Contains("Csharp2Md.Core", ProjectReferenceNames(tests));
    }

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Solution_ListsCoreAndDoesNotRemoveLegacyProjects()
    {
        var slnxPath = Path.Combine(CoreTestPaths.RepoRoot, "csharp2md.slnx");
        Assert.True(File.Exists(slnxPath), $"Solution file was not found at '{slnxPath}'.");

        var document = XDocument.Load(slnxPath);
        var projectPaths = document.Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .OfType<string>()
            .ToArray();

        Assert.Contains("src/Csharp2Md.Core/Csharp2Md.Core.csproj", projectPaths);
        Assert.Contains("tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj", projectPaths);
        foreach (var legacy in LegacySrcProjects)
        {
            Assert.Contains(legacy, projectPaths);
        }
    }

    private static XDocument LoadCsproj(params string[] relativeSegments)
    {
        var path = Path.Combine([CoreTestPaths.RepoRoot, .. relativeSegments]);
        Assert.True(File.Exists(path), $"Project file was not found at '{path}'.");
        return XDocument.Load(path);
    }

    private static XDocument LoadDirectoryPackages()
    {
        var path = Path.Combine(CoreTestPaths.RepoRoot, "Directory.Packages.props");
        Assert.True(File.Exists(path), $"Directory.Packages.props was not found at '{path}'.");
        return XDocument.Load(path);
    }

    private static string ElementValue(XDocument document, string localName)
    {
        var value = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == localName)
            ?.Value;
        Assert.False(string.IsNullOrWhiteSpace(value), $"Project is missing <{localName}>.");
        return value!;
    }

    private static string[] PackageReferenceNames(XDocument document) =>
        document.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .ToArray();

    private static string[] ProjectReferenceNames(XDocument document) =>
        document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(
                include!.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)))
            .ToArray();

    private static string[] InternalsVisibleToNames(XDocument document) =>
        document.Descendants()
            .Where(element => element.Name.LocalName == "InternalsVisibleTo")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .ToArray();

    private static string PackageVersion(XDocument packages, string packageId)
    {
        var version = packages.Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion")
            .FirstOrDefault(element => element.Attribute("Include")?.Value == packageId)
            ?.Attribute("Version")
            ?.Value;
        Assert.False(string.IsNullOrWhiteSpace(version), $"Directory.Packages.props has no PackageVersion for '{packageId}'.");
        return version!;
    }
}
