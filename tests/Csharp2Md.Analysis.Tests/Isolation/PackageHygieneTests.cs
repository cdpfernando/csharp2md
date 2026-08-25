using System.Xml.Linq;

namespace Csharp2Md.Analysis.Tests.Isolation;

public sealed class PackageHygieneTests
{
    private static readonly string[] DroppedPackageVersions =
    [
        "YamlDotNet",
    ];

    private static readonly string[] FixturePackageVersions =
    [
        "Microsoft.Extensions.Http",
        "Grpc.AspNetCore",
    ];

    [Fact]
    [Trait("Requirement", "ROSE-26")]
    public void DroppedPackageVersions_OmitWorkspacesPackagesAndKeepYamlDotNet()
    {
        Assert.DoesNotContain("Microsoft.CodeAnalysis.Workspaces.MSBuild", DroppedPackageVersions);
        Assert.DoesNotContain("Microsoft.CodeAnalysis.CSharp.Workspaces", DroppedPackageVersions);
        Assert.Contains("YamlDotNet", DroppedPackageVersions);
    }

    [Theory]
    [Trait("Requirement", "ROSE-26")]
    [InlineData("Microsoft.CodeAnalysis.Workspaces.MSBuild")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.Workspaces")]
    public void AnalysisCsproj_ReferencesWorkspacesPackageAtCentralVersion(string packageId)
    {
        var csprojPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Analysis",
            "Csharp2Md.Analysis.csproj");
        Assert.True(File.Exists(csprojPath), $"Analysis project file was not found at '{csprojPath}'.");

        var referenced = AttributeIncludes(XDocument.Load(csprojPath), "PackageReference");
        Assert.True(
            referenced.Contains(packageId, StringComparer.Ordinal),
            $"Csharp2Md.Analysis.csproj must declare a PackageReference to '{packageId}'.");

        var versionsPath = Path.Combine(AnalysisTestPaths.RepoRoot, "Directory.Packages.props");
        Assert.True(File.Exists(versionsPath), $"Directory.Packages.props was not found at '{versionsPath}'.");

        var version = XDocument.Load(versionsPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion"
                && string.Equals(element.Attribute("Include")?.Value, packageId, StringComparison.Ordinal))
            .Select(element => element.Attribute("Version")?.Value)
            .FirstOrDefault();

        Assert.True(
            version == "5.6.0",
            $"Directory.Packages.props PackageVersion '{packageId}' must be 5.6.0, but is '{version}'.");
    }

    [Theory]
    [Trait("Requirement", "ENG-51")]
    [Trait("Requirement", "ROSE-26")]
    [MemberData(nameof(DroppedPackageVersionCases))]
    public void DirectoryPackages_DoesNotDeclareDroppedPackageVersion(string packageId)
    {
        var declared = ReadPackageVersionIncludes();
        Assert.True(
            !declared.Contains(packageId, StringComparer.Ordinal),
            $"Directory.Packages.props must not declare PackageVersion '{packageId}'.");
    }

    [Theory]
    [Trait("Requirement", "ENG-51")]
    [MemberData(nameof(FixturePackageVersionCases))]
    public void DirectoryPackages_KeepsFixturePackageVersion(string packageId)
    {
        var declared = ReadPackageVersionIncludes();
        Assert.True(
            declared.Contains(packageId, StringComparer.Ordinal),
            $"Directory.Packages.props must keep fixture PackageVersion '{packageId}'.");
    }

    [Fact]
    [Trait("Requirement", "ENG-51")]
    public void DirectoryPackages_EveryPackageVersionHasAConsumer()
    {
        var consumers = ReadPackageReferenceIncludes();
        var unused = ReadPackageVersionIncludes()
            .Where(packageId => !consumers.Contains(packageId, StringComparer.Ordinal))
            .ToArray();

        Assert.True(
            unused.Length == 0,
            $"Directory.Packages.props declares PackageVersion with no consumer in the solution or fixtures/: {string.Join(", ", unused)}.");
    }

    public static TheoryData<string> DroppedPackageVersionCases()
    {
        var data = new TheoryData<string>();
        foreach (var packageId in DroppedPackageVersions)
        {
            data.Add(packageId);
        }

        return data;
    }

    public static TheoryData<string> FixturePackageVersionCases()
    {
        var data = new TheoryData<string>();
        foreach (var packageId in FixturePackageVersions)
        {
            data.Add(packageId);
        }

        return data;
    }

    private static IReadOnlyList<string> ReadPackageVersionIncludes()
    {
        var path = Path.Combine(AnalysisTestPaths.RepoRoot, "Directory.Packages.props");
        Assert.True(File.Exists(path), $"Directory.Packages.props was not found at '{path}'.");
        return AttributeIncludes(XDocument.Load(path), "PackageVersion");
    }

    private static HashSet<string> ReadPackageReferenceIncludes()
    {
        var consumers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in ConsumerProjectPaths())
        {
            foreach (var packageId in AttributeIncludes(XDocument.Load(path), "PackageReference"))
            {
                consumers.Add(packageId);
            }
        }

        return consumers;
    }

    private static IEnumerable<string> ConsumerProjectPaths()
    {
        var slnxPath = Path.Combine(AnalysisTestPaths.RepoRoot, "csharp2md.slnx");
        Assert.True(File.Exists(slnxPath), $"Solution file was not found at '{slnxPath}'.");

        var document = XDocument.Load(slnxPath);
        foreach (var relative in document.Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .OfType<string>())
        {
            yield return Path.Combine(
                AnalysisTestPaths.RepoRoot,
                relative.Replace('/', Path.DirectorySeparatorChar));
        }

        var buildProps = Path.Combine(AnalysisTestPaths.RepoRoot, "Directory.Build.props");
        if (File.Exists(buildProps))
        {
            yield return buildProps;
        }

        var fixtures = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures");
        if (!Directory.Exists(fixtures))
        {
            yield break;
        }

        foreach (var csproj in Directory.EnumerateFiles(fixtures, "*.csproj", SearchOption.AllDirectories))
        {
            yield return csproj;
        }
    }

    private static IReadOnlyList<string> AttributeIncludes(XDocument document, string elementName) =>
        document.Descendants()
            .Where(element => element.Name.LocalName == elementName)
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .ToArray();
}
