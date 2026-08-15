using Csharp2Md.Core.Discovery;
using Csharp2Md.Core.Manifests;

namespace Csharp2Md.Core.Tests.Discovery;

public sealed class ServiceDiscovererTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-discovery-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string CreateServiceDir(string name, params string[] fileNames)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        foreach (var file in fileNames)
        {
            File.WriteAllText(Path.Combine(dir, file), string.Empty);
        }

        return dir;
    }

    [Fact]
    public void Discover_WildcardPattern_ExpandsToMatchingDirectories()
    {
        CreateServiceDir("Acme.Orders", "Acme.Orders.csproj");
        CreateServiceDir("Acme.Payments", "Acme.Payments.csproj");
        var manifest = new Manifest([new ManifestEntry(Path.Combine(_root, "Acme.*"))]);

        var result = ServiceDiscoverer.Discover(manifest);

        Assert.Equal(2, result.Catalog.Services.Count);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Discover_DirectoryWithSingleSln_UsesSolutionBoundary()
    {
        var dir = CreateServiceDir("WithSln", "App.sln", "App.csproj");
        var manifest = new Manifest([new ManifestEntry(dir)]);

        var result = ServiceDiscoverer.Discover(manifest);

        var service = Assert.Single(result.Catalog.Services);
        Assert.Equal(ServiceBoundaryKind.Solution, service.BoundaryKind);
        Assert.EndsWith("App.sln", service.SolutionPath);
    }

    [Fact]
    public void Discover_SlnxSolution_PopulatesProjectPathsFromSolutionFile()
    {
        var dir = Path.Combine(_root, "WithSlnx");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "App.csproj"), string.Empty);
        File.WriteAllText(
            Path.Combine(dir, "App.slnx"),
            """<Solution><Project Path="App.csproj" /></Solution>""");
        var manifest = new Manifest([new ManifestEntry(dir)]);

        var result = ServiceDiscoverer.Discover(manifest);

        var service = Assert.Single(result.Catalog.Services);
        Assert.Equal(ServiceBoundaryKind.Solution, service.BoundaryKind);
        var projectPath = Assert.Single(service.ProjectPaths);
        Assert.Equal(Path.Combine(dir, "App.csproj"), projectPath);
    }

    [Fact]
    public void Discover_ClassicSlnSolution_PopulatesProjectPathsFromSolutionFile()
    {
        var dir = Path.Combine(_root, "WithClassicSln");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "App.csproj"), string.Empty);
        File.WriteAllText(
            Path.Combine(dir, "App.sln"),
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            """);
        var manifest = new Manifest([new ManifestEntry(dir)]);

        var result = ServiceDiscoverer.Discover(manifest);

        var service = Assert.Single(result.Catalog.Services);
        var projectPath = Assert.Single(service.ProjectPaths);
        Assert.Equal(Path.Combine(dir, "App.csproj"), projectPath);
    }

    [Fact]
    public void Discover_DirectoryWithNoSlnButCsproj_UsesLooseProjectsBoundary()
    {
        var dir = CreateServiceDir("LooseProjects", "One.csproj", "Two.csproj");
        var manifest = new Manifest([new ManifestEntry(dir)]);

        var result = ServiceDiscoverer.Discover(manifest);

        var service = Assert.Single(result.Catalog.Services);
        Assert.Equal(ServiceBoundaryKind.LooseProjects, service.BoundaryKind);
        Assert.Equal(2, service.ProjectPaths.Count);
    }

    [Fact]
    public void Discover_ManifestOverride_UsesOverrideProjectsRegardlessOfSlnOrCsproj()
    {
        var dir = CreateServiceDir("Overridden", "App.sln", "App.csproj", "Other.csproj");
        var manifest = new Manifest(
            [new ManifestEntry(dir, Projects: [Path.Combine(dir, "Other.csproj")])]);

        var result = ServiceDiscoverer.Discover(manifest);

        var service = Assert.Single(result.Catalog.Services);
        Assert.Equal(ServiceBoundaryKind.ManifestOverride, service.BoundaryKind);
        Assert.Equal([Path.Combine(dir, "Other.csproj")], service.ProjectPaths);
    }

    [Fact]
    public void Discover_UnmatchedGlob_WarnsAndContinues()
    {
        var manifest = new Manifest([new ManifestEntry(Path.Combine(_root, "DoesNotExist.*"))]);

        var result = ServiceDiscoverer.Discover(manifest);

        Assert.Empty(result.Catalog.Services);
        Assert.Single(result.Warnings);
        Assert.Contains("zero directories", result.Warnings[0]);
    }

    [Fact]
    public void Discover_DuplicateRoot_ProcessedOnceWithWarning()
    {
        var dir = CreateServiceDir("Dup", "App.csproj");
        var manifest = new Manifest([new ManifestEntry(dir), new ManifestEntry(dir)]);

        var result = ServiceDiscoverer.Discover(manifest);

        Assert.Single(result.Catalog.Services);
        Assert.Single(result.Warnings);
        Assert.Contains("Duplicate service root", result.Warnings[0]);
    }

    [Fact]
    public void Discover_MultipleSlnFiles_WarnsAndSkipsRatherThanGuessing()
    {
        var dir = CreateServiceDir("Ambiguous", "One.sln", "Two.sln");
        var manifest = new Manifest([new ManifestEntry(dir)]);

        var result = ServiceDiscoverer.Discover(manifest);

        Assert.Empty(result.Catalog.Services);
        Assert.Single(result.Warnings);
        Assert.Contains("expected exactly one", result.Warnings[0]);
    }

    [Fact]
    public void Discover_NoSlnNoCsproj_WarnsAndSkips()
    {
        var dir = CreateServiceDir("Empty");
        var manifest = new Manifest([new ManifestEntry(dir)]);

        var result = ServiceDiscoverer.Discover(manifest);

        Assert.Empty(result.Catalog.Services);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Discover_NoExplicitName_DefaultsServiceNameToDirectoryName()
    {
        var dir = CreateServiceDir("Acme.Orders", "Acme.Orders.csproj");
        var manifest = new Manifest([new ManifestEntry(dir)]);

        var result = ServiceDiscoverer.Discover(manifest);

        var service = Assert.Single(result.Catalog.Services);
        Assert.Equal("Acme.Orders", service.Name.Value);
    }

    [Fact]
    public void Discover_ExplicitName_OverridesDirectoryNameDefault()
    {
        var dir = CreateServiceDir("Acme.Orders", "Acme.Orders.csproj");
        var manifest = new Manifest([new ManifestEntry(dir, Name: "OrdersService")]);

        var result = ServiceDiscoverer.Discover(manifest);

        var service = Assert.Single(result.Catalog.Services);
        Assert.Equal("OrdersService", service.Name.Value);
    }
}
