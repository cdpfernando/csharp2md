using Csharp2Md.Core.Discovery;

namespace Csharp2Md.Core.Tests.Discovery;

public sealed class ProjectIdentityReaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-identity-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteProject(string fileName, string content)
    {
        var path = Path.Combine(_root, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static ServiceDescriptor DescriptorFor(params string[] projectPaths) => new(
        new ServiceName("Test"), "root", ServiceBoundaryKind.LooseProjects, null, projectPaths, []);

    [Fact]
    public void ReadPackageIds_ExplicitPackageId_UsesItVerbatim()
    {
        var path = WriteProject(
            "Explicit.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Acme.Shared.Contracts</PackageId>
                <AssemblyName>SomethingElse</AssemblyName>
              </PropertyGroup>
            </Project>
            """);

        var ids = ProjectIdentityReader.ReadPackageIds(DescriptorFor(path));

        Assert.Equal([new PackageId("Acme.Shared.Contracts")], ids);
    }

    [Fact]
    public void ReadPackageIds_NoPackageIdButExplicitAssemblyName_FallsBackToAssemblyName()
    {
        var path = WriteProject(
            "AssemblyOnly.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>Acme.Renamed</AssemblyName>
              </PropertyGroup>
            </Project>
            """);

        var ids = ProjectIdentityReader.ReadPackageIds(DescriptorFor(path));

        Assert.Equal([new PackageId("Acme.Renamed")], ids);
    }

    [Fact]
    public void ReadPackageIds_NeitherPackageIdNorAssemblyName_FallsBackToProjectFileName()
    {
        var path = WriteProject(
            "Acme.Bare.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var ids = ProjectIdentityReader.ReadPackageIds(DescriptorFor(path));

        Assert.Equal([new PackageId("Acme.Bare")], ids);
    }

    [Fact]
    public void ReadPackageIds_MalformedXml_FallsBackToProjectFileNameWithoutThrowing()
    {
        var path = WriteProject("Acme.Malformed.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><Unclosed>");

        var ids = ProjectIdentityReader.ReadPackageIds(DescriptorFor(path));

        Assert.Equal([new PackageId("Acme.Malformed")], ids);
    }

    [Fact]
    public void ReadPackageIds_MultipleProjects_ReturnsOneIdPerProjectInOrder()
    {
        var first = WriteProject(
            "First.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><PackageId>Acme.First</PackageId></PropertyGroup></Project>");
        var second = WriteProject(
            "Second.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><PackageId>Acme.Second</PackageId></PropertyGroup></Project>");

        var ids = ProjectIdentityReader.ReadPackageIds(DescriptorFor(first, second));

        Assert.Equal([new PackageId("Acme.First"), new PackageId("Acme.Second")], ids);
    }

    [Fact]
    public void ReadPackageIds_EmptyPackageIdElement_FallsBackRatherThanReturningEmptyString()
    {
        var path = WriteProject(
            "Acme.EmptyId.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><PackageId></PackageId></PropertyGroup></Project>");

        var ids = ProjectIdentityReader.ReadPackageIds(DescriptorFor(path));

        Assert.Equal([new PackageId("Acme.EmptyId")], ids);
    }
}
