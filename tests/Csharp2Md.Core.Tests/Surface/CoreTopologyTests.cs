using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Csharp2Md.Core;

namespace Csharp2Md.Core.Tests.Surface;

/// <summary>
/// PKG-10: after the replacement the repository carries the current contract only. These tests read the
/// repository itself -- solution, project files and product sources -- so a legacy assembly, a version
/// dispatch or a compatibility path cannot reappear without turning one of them red.
/// </summary>
public sealed class CoreTopologyTests
{
    private static readonly string[] CurrentProjects =
    [
        "src/Csharp2Md.Cli/Csharp2Md.Cli.csproj",
        "src/Csharp2Md.Core/Csharp2Md.Core.csproj",
        "tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj",
        "tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj",
    ];

    private static readonly string[] LegacyContractNames =
    [
        "BatchComposer",
        "PackageProjector",
        "ComposeAction",
        "IncompatibleProvenance",
        "taxonomy-registry",
    ];

    /// <summary>
    /// Composed at run time from the assembly prefix so this file carries no literal legacy name of its
    /// own -- the scan below reads product sources and must not be able to match itself.
    /// </summary>
    private static string[] LegacyAssemblyNames() =>
        new[] { "Analysis", "Domain", "Projection", "Storage" }
            .Select(static suffix => "Csharp2Md." + suffix)
            .ToArray();

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Solution_ListsExactlyTheFourCurrentProjects()
    {
        var slnxPath = Path.Combine(CoreTestPaths.RepoRoot, "csharp2md.slnx");
        Assert.True(File.Exists(slnxPath), $"Solution file was not found at '{slnxPath}'.");

        var projectPaths = XDocument.Load(slnxPath)
            .Descendants()
            .Where(static element => element.Name.LocalName == "Project")
            .Select(static element => element.Attribute("Path")?.Value)
            .OfType<string>()
            .Select(static path => path.Replace('\\', '/'))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(CurrentProjects, projectPaths);
    }

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Repository_ContainsNoProjectFileOutsideTheCurrentFour() =>
        Assert.Equal(CurrentProjects, ProjectFiles());

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Repository_CarriesNoLegacySchemaOrTaxonomyRegistry()
    {
        var contracts = Path.Combine(CoreTestPaths.RepoRoot, "contracts");

        Assert.False(
            Directory.Exists(contracts),
            $"The legacy JSON-schema and taxonomy-registry tree still exists at '{contracts}'.");
    }

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void ProductSources_NameNoLegacyAssembly()
    {
        var legacy = LegacyAssemblyNames();
        var offenders = ProductSources()
            .SelectMany(file => legacy
                .Where(name => file.Text.Contains(name, StringComparison.Ordinal))
                .Select(name => $"{file.RelativePath}: {name}"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Product sources still name legacy assemblies: " + string.Join(", ", offenders));
    }

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void ProductSources_CarryNoVersionDispatchOrCompatibilityPath()
    {
        var offenders = ProductSources()
            .SelectMany(file => LegacyContractNames
                .Where(name => file.Text.Contains(name, StringComparison.Ordinal))
                .Select(name => $"{file.RelativePath}: {name}"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Product sources still carry a legacy contract path: " + string.Join(", ", offenders));
    }

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void ProductSources_NeverCallMSBuildLocatorRegisterDefaults()
    {
        var offenders = ProductSources()
            .Where(static file => file.Text.Contains("MSBuildLocator", StringComparison.Ordinal))
            .Select(static file => file.RelativePath)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Roslyn 4.9+ loads projects out of process (AD-003); offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void NoProject_ReferencesMicrosoftBuildPackages()
    {
        var offenders = ProjectFiles()
            .SelectMany(relative => PackageReferenceNames(LoadCsproj(relative))
                .Where(static name => name.StartsWith("Microsoft.Build", StringComparison.Ordinal))
                .Select(name => $"{relative}: {name}"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "No project may reference Microsoft.Build.* (AD-003): " + string.Join(", ", offenders));
    }

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Core_TargetsNet10AndReferencesRoslynWorkspaces560()
    {
        var core = LoadCsproj("src/Csharp2Md.Core/Csharp2Md.Core.csproj");
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
    public void Core_ReferencesNoOtherProject() =>
        Assert.Empty(ProjectReferenceNames(LoadCsproj("src/Csharp2Md.Core/Csharp2Md.Core.csproj")));

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Cli_ReferencesCoreAndNothingElse() =>
        Assert.Equal(
            ["Csharp2Md.Core"],
            ProjectReferenceNames(LoadCsproj("src/Csharp2Md.Cli/Csharp2Md.Cli.csproj")));

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Core_ExposesInternalsOnlyToCoreTests()
    {
        Assert.Equal(
            ["Csharp2Md.Core.Tests"],
            InternalsVisibleToNames(LoadCsproj("src/Csharp2Md.Core/Csharp2Md.Core.csproj")));

        var attributes = typeof(AssemblyMarker).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(static attribute => attribute.AssemblyName)
            .ToArray();
        Assert.Equal(["Csharp2Md.Core.Tests"], attributes);
    }

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Cli_ExposesInternalsOnlyToCliTests() =>
        Assert.Equal(
            ["Csharp2Md.Cli.Tests"],
            InternalsVisibleToNames(LoadCsproj("src/Csharp2Md.Cli/Csharp2Md.Cli.csproj")));

    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void CoreTests_UseExistingXunitAndVerifyStack()
    {
        var tests = LoadCsproj("tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj");
        var packages = PackageReferenceNames(tests);

        Assert.Equal("net10.0", ElementValue(tests, "TargetFramework"));
        Assert.Contains("xunit", packages, StringComparer.Ordinal);
        Assert.Contains("Verify.Xunit", packages, StringComparer.Ordinal);
        Assert.Contains("Microsoft.NET.Test.Sdk", packages, StringComparer.Ordinal);
        Assert.Equal(["Csharp2Md.Core"], ProjectReferenceNames(tests));
    }

    private static string[] ProjectFiles() =>
        Directory.EnumerateFiles(CoreTestPaths.RepoRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(CoreTestPaths.RepoRoot, path).Replace('\\', '/'))
            .Where(static relative => relative.StartsWith("src/", StringComparison.Ordinal)
                || relative.StartsWith("tests/", StringComparison.Ordinal))
            .Where(static relative => !relative.Contains("/bin/", StringComparison.Ordinal)
                && !relative.Contains("/obj/", StringComparison.Ordinal))
            .OrderBy(static relative => relative, StringComparer.Ordinal)
            .ToArray();

    private static (string RelativePath, string Text)[] ProductSources() =>
        Directory.EnumerateFiles(Path.Combine(CoreTestPaths.RepoRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Select(path => (RelativePath: Path.GetRelativePath(CoreTestPaths.RepoRoot, path).Replace('\\', '/'), Path: path))
            .Where(static file => !file.RelativePath.Contains("/bin/", StringComparison.Ordinal)
                && !file.RelativePath.Contains("/obj/", StringComparison.Ordinal))
            .Select(static file => (file.RelativePath, Text: File.ReadAllText(file.Path)))
            .ToArray();

    private static XDocument LoadCsproj(string relativePath)
    {
        var path = Path.Combine(CoreTestPaths.RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
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
            .Where(static element => element.Name.LocalName == "PackageReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => include!)
            .ToArray();

    private static string[] ProjectReferenceNames(XDocument document) =>
        document.Descendants()
            .Where(static element => element.Name.LocalName == "ProjectReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => Path.GetFileNameWithoutExtension(
                include!.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

    private static string[] InternalsVisibleToNames(XDocument document) =>
        document.Descendants()
            .Where(static element => element.Name.LocalName == "InternalsVisibleTo")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => include!)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

    private static string PackageVersion(XDocument packages, string packageId)
    {
        var version = packages.Descendants()
            .Where(static element => element.Name.LocalName == "PackageVersion")
            .FirstOrDefault(element => element.Attribute("Include")?.Value == packageId)
            ?.Attribute("Version")
            ?.Value;
        Assert.False(string.IsNullOrWhiteSpace(version), $"Directory.Packages.props has no PackageVersion for '{packageId}'.");
        return version!;
    }
}
