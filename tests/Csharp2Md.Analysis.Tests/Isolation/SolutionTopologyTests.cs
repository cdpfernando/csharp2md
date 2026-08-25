using System.Xml.Linq;

namespace Csharp2Md.Analysis.Tests.Isolation;

public sealed class SolutionTopologyTests
{
    public static IEnumerable<object[]> NewProductionProjects() =>
    [
        ["Csharp2Md.Analysis", "src/Csharp2Md.Analysis/Csharp2Md.Analysis.csproj"],
        ["Csharp2Md.Storage", "src/Csharp2Md.Storage/Csharp2Md.Storage.csproj"],
        ["Csharp2Md.Projection", "src/Csharp2Md.Projection/Csharp2Md.Projection.csproj"],
    ];

    public static IEnumerable<object[]> ForbiddenPackageCases()
    {
        foreach (var project in NewProductionProjects())
        {
            yield return [project[0], project[1], "Microsoft.CodeAnalysis"];
            yield return [project[0], project[1], "Microsoft.Build"];
        }
    }

    private static readonly string[] AllowedSolutionProjectPaths =
    [
        "src/Csharp2Md.Analysis/Csharp2Md.Analysis.csproj",
        "src/Csharp2Md.Cli/Csharp2Md.Cli.csproj",
        "src/Csharp2Md.Domain/Csharp2Md.Domain.csproj",
        "src/Csharp2Md.Projection/Csharp2Md.Projection.csproj",
        "src/Csharp2Md.Storage/Csharp2Md.Storage.csproj",
        "tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj",
        "tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj",
        "tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj",
        "tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj",
        "tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj",
    ];

    [Fact]
    [Trait("Requirement", "ENG-49")]
    public void Slnx_ListsOnlyAllowlistedProjects()
    {
        var listed = ReadAllProjectPaths();
        var extras = listed.Except(AllowedSolutionProjectPaths, StringComparer.Ordinal).ToArray();
        var missing = AllowedSolutionProjectPaths.Except(listed, StringComparer.Ordinal).ToArray();

        Assert.True(
            extras.Length == 0,
            $"csharp2md.slnx lists extra project path(s): {string.Join(", ", extras)}.");
        Assert.True(
            missing.Length == 0,
            $"csharp2md.slnx is missing allowlisted project path(s): {string.Join(", ", missing)}.");
    }

    [Theory]
    [Trait("Requirement", "ENG-01")]
    [MemberData(nameof(NewProductionProjects))]
    public void Slnx_ListsProjectUnderSrc(string projectName, string relativePath)
    {
        var srcPaths = ReadSrcFolderProjectPaths();
        Assert.True(
            srcPaths.Contains(relativePath),
            $"csharp2md.slnx /src/ must list {projectName} at '{relativePath}', but the folder contains: {string.Join(", ", srcPaths)}.");
    }

    [Theory]
    [Trait("Requirement", "ENG-01")]
    [MemberData(nameof(NewProductionProjects))]
    public void Csproj_TargetsNet10(string projectName, string relativePath)
    {
        var csprojPath = Path.Combine(AnalysisTestPaths.RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(csprojPath), $"{projectName} project file was not found at '{csprojPath}'.");

        var document = XDocument.Load(csprojPath);
        var targetFramework = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "TargetFramework")
            ?.Value;

        Assert.True(
            targetFramework == "net10.0",
            $"{projectName} must target net10.0, but TargetFramework is '{targetFramework}'.");
    }

    [Theory]
    [Trait("Requirement", "ENG-06")]
    [MemberData(nameof(ForbiddenPackageCases))]
    public void Csproj_DeclaresNoForbiddenPackage(string projectName, string relativePath, string forbiddenPrefix)
    {
        var csprojPath = Path.Combine(AnalysisTestPaths.RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(csprojPath), $"{projectName} project file was not found at '{csprojPath}'.");

        var offending = ReadPackageReferenceIncludes(csprojPath)
            .FirstOrDefault(packageId => packageId.StartsWith(forbiddenPrefix, StringComparison.Ordinal));

        Assert.True(
            offending is null,
            $"{projectName} must not declare a package reference to '{offending}' (forbidden prefix '{forbiddenPrefix}').");
    }

    private static IReadOnlyList<string> ReadAllProjectPaths()
    {
        var slnxPath = Path.Combine(AnalysisTestPaths.RepoRoot, "csharp2md.slnx");
        Assert.True(File.Exists(slnxPath), $"Solution file was not found at '{slnxPath}'.");

        var document = XDocument.Load(slnxPath);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .OfType<string>()
            .Select(path => path.Replace('\\', '/'))
            .ToArray();
    }

    private static IReadOnlyList<string> ReadSrcFolderProjectPaths()
    {
        var slnxPath = Path.Combine(AnalysisTestPaths.RepoRoot, "csharp2md.slnx");
        Assert.True(File.Exists(slnxPath), $"Solution file was not found at '{slnxPath}'.");

        var document = XDocument.Load(slnxPath);
        var srcFolder = document.Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "Folder"
                && element.Attribute("Name")?.Value == "/src/");

        Assert.True(srcFolder is not null, "csharp2md.slnx has no Folder named '/src/'.");

        return srcFolder.Elements()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .OfType<string>()
            .ToArray();
    }

    private static IReadOnlyList<string> ReadPackageReferenceIncludes(string csprojPath)
    {
        var document = XDocument.Load(csprojPath);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .ToArray();
    }
}
