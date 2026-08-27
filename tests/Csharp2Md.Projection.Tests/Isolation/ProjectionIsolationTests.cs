using System.Xml.Linq;

namespace Csharp2Md.Projection.Tests.Isolation;

public sealed class ProjectionIsolationTests
{
    public static TheoryData<string> ForbiddenAnalysisProjectReferences() =>
        new()
        {
            "Csharp2Md.Storage",
            "Csharp2Md.Projection",
            "Csharp2Md.Cli",
        };

    [Fact]
    [Trait("Requirement", "ENG-04")]
    public void ProjectionCsproj_DeclaresNoProjectReferenceToAnalysis() =>
        AssertNoProjectReference(ProjectionCsprojPath(), "Csharp2Md.Analysis");

    [Fact]
    [Trait("Requirement", "RP-01")]
    [Trait("Requirement", "RP-50")]
    [Trait("Requirement", "ENG-04")]
    public void ProjectionCsproj_ProjectReferencesEqualStorageAndDomain()
    {
        var names = ReadProjectReferenceNames(ProjectionCsprojPath());

        Assert.Equal(["Csharp2Md.Domain", "Csharp2Md.Storage"], names);
    }

    [Fact]
    [Trait("Requirement", "ENG-04")]
    public void ProjectionCsproj_DeclaresNoProjectReferenceToCli() =>
        AssertNoProjectReference(ProjectionCsprojPath(), "Csharp2Md.Cli");

    [Theory]
    [Trait("Requirement", "RP-50")]
    [MemberData(nameof(ForbiddenAnalysisProjectReferences))]
    public void AnalysisCsproj_DeclaresNoProjectReferenceTo(string forbiddenProject) =>
        AssertNoProjectReference(CsprojPath("Csharp2Md.Analysis"), forbiddenProject);

    [Fact]
    [Trait("Requirement", "RP-50")]
    public void AnalysisCsproj_ProjectReferencesEqualDomainOnly()
    {
        var names = ReadProjectReferenceNames(CsprojPath("Csharp2Md.Analysis"));

        Assert.Equal(["Csharp2Md.Domain"], names);
    }

    [Fact]
    [Trait("Requirement", "RP-50")]
    public void CliCsproj_DeclaresNoDirectProjectReferenceToDomain() =>
        AssertNoProjectReference(CsprojPath("Csharp2Md.Cli"), "Csharp2Md.Domain");

    [Fact]
    [Trait("Requirement", "RP-50")]
    public void CliCsproj_ProjectReferencesEqualAnalysisStorageAndProjection()
    {
        var names = ReadProjectReferenceNames(CsprojPath("Csharp2Md.Cli"));

        Assert.DoesNotContain("Csharp2Md.Domain", names);
        Assert.Equal(["Csharp2Md.Analysis", "Csharp2Md.Projection", "Csharp2Md.Storage"], names);
    }

    private static void AssertNoProjectReference(string csprojPath, string forbiddenProject)
    {
        Assert.True(File.Exists(csprojPath), $"Project file was not found at '{csprojPath}'.");

        var offending = ReadProjectReferenceIncludes(csprojPath)
            .FirstOrDefault(include => ReferencesProject(include, forbiddenProject));

        Assert.True(
            offending is null,
            $"{Path.GetFileNameWithoutExtension(csprojPath)} must not declare a project reference to {forbiddenProject}, but found '{offending}'.");
    }

    private static string ProjectionCsprojPath() => CsprojPath("Csharp2Md.Projection");

    private static string CsprojPath(string projectName) =>
        Path.Combine(ProjectionTestPaths.RepoRoot, "src", projectName, projectName + ".csproj");

    private static string[] ReadProjectReferenceNames(string csprojPath) =>
        ReadProjectReferenceIncludes(csprojPath)
            .Select(include => Path.GetFileNameWithoutExtension(
                include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

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
