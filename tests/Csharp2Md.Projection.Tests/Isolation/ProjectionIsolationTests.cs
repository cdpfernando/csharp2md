using System.Xml.Linq;

namespace Csharp2Md.Projection.Tests.Isolation;

public sealed class ProjectionIsolationTests
{
    [Fact]
    [Trait("Requirement", "ENG-04")]
    public void ProjectionCsproj_DeclaresNoProjectReferenceToAnalysis() =>
        AssertNoProjectReference("Csharp2Md.Analysis");

    [Fact]
    [Trait("Requirement", "RP-01")]
    [Trait("Requirement", "ENG-04")]
    public void ProjectionCsproj_ProjectReferencesEqualStorageAndDomain()
    {
        var names = ReadProjectReferenceIncludes(ProjectionCsprojPath())
            .Select(include => Path.GetFileNameWithoutExtension(
                include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Csharp2Md.Domain", "Csharp2Md.Storage"], names);
    }

    [Fact]
    [Trait("Requirement", "ENG-04")]
    public void ProjectionCsproj_DeclaresNoProjectReferenceToCli() =>
        AssertNoProjectReference("Csharp2Md.Cli");

    private static void AssertNoProjectReference(string forbiddenProject)
    {
        var csprojPath = ProjectionCsprojPath();

        Assert.True(File.Exists(csprojPath), $"Projection project file was not found at '{csprojPath}'.");

        var offending = ReadProjectReferenceIncludes(csprojPath)
            .FirstOrDefault(include => ReferencesProject(include, forbiddenProject));

        Assert.True(
            offending is null,
            $"Csharp2Md.Projection must not declare a project reference to {forbiddenProject}, but found '{offending}'.");
    }

    private static string ProjectionCsprojPath() =>
        Path.Combine(
            ProjectionTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Projection",
            "Csharp2Md.Projection.csproj");

    private static IReadOnlyList<string> ReadProjectReferenceIncludes(string csprojPath)
    {
        var document = XDocument.Load(csprojPath);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
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
}
