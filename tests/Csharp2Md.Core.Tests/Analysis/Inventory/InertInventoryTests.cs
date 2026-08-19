using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Analysis.Inventory;

namespace Csharp2Md.Core.Tests.Analysis.Inventory;

[Trait("Category", "Integration")]
public sealed class InertInventoryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-v3-inventory-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Inventory_SyntaxOnly_DoesNotInvokeExecutableAnalysis()
    {
        CreateProject("App", "<Project Sdk=\"Microsoft.NET.Sdk\" />", ("Program.cs", "class Program { }"));
        var observer = new RecordingObserver();

        _ = new InertInventory(observer).Inventory(Request());

        Assert.Empty(observer.Invocations);
    }

    [Fact]
    public void Inventory_BrokenProject_StillIncludesEligibleSources()
    {
        CreateProject("Broken", "<Project><Broken>", ("Broken.cs", "class Broken {"));

        var result = new InertInventory().Inventory(Request());

        var project = Assert.Single(Assert.Single(result.Services).Projects);
        Assert.Equal(["Broken/Broken.cs"], project.SourceFiles.ToArray());
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "inventory.project-xml");
    }

    [Fact]
    public void Inventory_UnrestoredProject_StillIncludesEligibleSources()
    {
        CreateProject(
            "Unrestored",
            "<Project Sdk=\"Missing.Sdk/99.0.0\"><ItemGroup><PackageReference Include=\"Missing\" Version=\"1.0.0\" /></ItemGroup></Project>",
            ("Feature.cs", "namespace Features; public class Feature;"));

        var result = new InertInventory().Inventory(Request());

        Assert.Equal(["Unrestored/Feature.cs"], Assert.Single(Assert.Single(result.Services).Projects).SourceFiles.ToArray());
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Inventory_DeclaredExtensions_ArePathsOnly()
    {
        CreateProject(
            "Extensions",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <Analyzer Include="tools/checks.dll" />
                <Generator Include="tools/source.dll" />
                <ProjectReference Include="../Gen/Gen.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
            </Project>
            """);

        var project = Assert.Single(Assert.Single(new InertInventory().Inventory(Request()).Services).Projects);

        Assert.Equal(["Extensions/tools/checks.dll"], project.AnalyzerPaths.ToArray());
        Assert.Equal(["Extensions/tools/source.dll", "Gen/Gen.csproj"], project.GeneratorPaths.ToArray());
    }

    [Fact]
    public void Inventory_DeclaredImports_IncludeLiteralAndUnevaluatedPaths()
    {
        CreateProject(
            "Imports",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <Import Project="build/common.props" />
              <Import Project="$(RepoRoot)/shared.targets" />
            </Project>
            """);

        var project = Assert.Single(Assert.Single(new InertInventory().Inventory(Request()).Services).Projects);

        Assert.Equal(["$(RepoRoot)/shared.targets", "Imports/build/common.props"], project.DeclaredImports.ToArray());
    }

    [Fact]
    public void Inventory_ConfigurationFiles_AreIncludedWithoutParsing()
    {
        var projectDirectory = CreateProject("Configured", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(projectDirectory, "appsettings.Broken.json"), "{not-json");
        File.WriteAllText(Path.Combine(projectDirectory, "docker-compose.yml"), "not: [yaml");

        var project = Assert.Single(Assert.Single(new InertInventory().Inventory(Request()).Services).Projects);

        Assert.Equal(
            ["Configured/appsettings.Broken.json", "Configured/docker-compose.yml"],
            project.ConfigurationFiles.ToArray());
    }

    [Fact]
    public void Inventory_ExcludedBuildAndGeneratedSources_AreOmitted()
    {
        var projectDirectory = CreateProject(
            "Filtered",
            "<Project Sdk=\"Microsoft.NET.Sdk\" />",
            ("Keep.cs", "class Keep { }"),
            ("Generated.g.cs", "class Generated { }"));
        Directory.CreateDirectory(Path.Combine(projectDirectory, "obj"));
        File.WriteAllText(Path.Combine(projectDirectory, "obj", "AssemblyInfo.cs"), "class Noise { }");

        var project = Assert.Single(Assert.Single(new InertInventory().Inventory(Request()).Services).Projects);

        Assert.Equal(["Filtered/Keep.cs"], project.SourceFiles.ToArray());
    }

    [Fact]
    public void Inventory_CatalogAndPaths_AreCanonicalAndRootRelative()
    {
        CreateProject("Zulu", "<Project Sdk=\"Microsoft.NET.Sdk\" />", ("Z.cs", "class Z { }"));
        CreateProject("Alpha", "<Project Sdk=\"Microsoft.NET.Sdk\" />", ("B.cs", "class B { }"), ("A.cs", "class A { }"));

        var result = new InertInventory().Inventory(Request());

        Assert.Equal(["Alpha", "Zulu"], result.Services.Select(service => service.RootPath));
        Assert.Equal(["Alpha/A.cs", "Alpha/B.cs"], result.Services[0].Projects[0].SourceFiles.ToArray());
        Assert.All(result.Services.SelectMany(service => service.Projects), project =>
        {
            Assert.False(Path.IsPathRooted(project.RelativePath));
            Assert.DoesNotContain('\\', project.RelativePath);
        });
    }

    private AnalysisRequest Request()
    {
        var request = AnalysisRequest.Create(_root, Path.Combine(Path.GetTempPath(), $"out-{Guid.NewGuid():N}"));
        return Assert.IsType<AnalysisRequest>(request.Request);
    }

    private string CreateProject(string name, string projectXml, params (string Name, string Source)[] sources)
    {
        var directory = Path.Combine(_root, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{name}.csproj"), projectXml);
        foreach (var source in sources)
        {
            File.WriteAllText(Path.Combine(directory, source.Name), source.Source);
        }

        return directory;
    }

    private sealed class RecordingObserver : IInventoryExecutionObserver
    {
        public List<string> Invocations { get; } = [];

        public void ExecutableAdapterInvoked(string adapterKind) => Invocations.Add(adapterKind);
    }
}
