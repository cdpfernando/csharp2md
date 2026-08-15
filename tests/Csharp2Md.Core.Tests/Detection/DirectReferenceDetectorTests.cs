using Csharp2Md.Core;
using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Discovery;
using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Tests.Detection;

public sealed class DirectReferenceDetectorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-directref-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteProject(string serviceFolder, string projectName, string body)
    {
        var directory = Path.Combine(_root, serviceFolder);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, projectName);
        File.WriteAllText(
            path,
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
            {body}
              </ItemGroup>
            </Project>
            """);

        return path;
    }

    private static ServiceDescriptor Service(string name, string projectPath, params string[] packageIds) => new(
        new ServiceName(name),
        Path.GetDirectoryName(projectPath)!,
        ServiceBoundaryKind.LooseProjects,
        null,
        [projectPath],
        [.. packageIds.Select(id => new PackageId(id))]);

    private static IReadOnlyList<DependencySignal> Detect(string projectPath, string sourceService, params ServiceDescriptor[] services) =>
        new DirectReferenceDetector()
            .Detect(new ProjectDetectionContext(new ServiceName(sourceService), projectPath, new ServiceCatalog(services)))
            .ToList();

    // P2-04: a project reference to another manifest service's project is a direct-reference edge.
    [Fact]
    public void Detect_ProjectReferenceToAnotherManifestService_RecordsADirectReferenceEdge()
    {
        var contracts = WriteProject("Acme.Shared.Contracts", "Acme.Shared.Contracts.csproj", "    <Compile Include=\"X.cs\" />");
        var orders = WriteProject(
            "Acme.Orders",
            "Acme.Orders.csproj",
            "    <ProjectReference Include=\"..\\Acme.Shared.Contracts\\Acme.Shared.Contracts.csproj\" />");

        var signals = Detect(orders, "Acme.Orders", Service("Acme.Shared.Contracts", contracts), Service("Acme.Orders", orders));

        var signal = Assert.Single(signals);
        Assert.Equal(new ServiceName("Acme.Orders"), signal.SourceService);
        Assert.Equal(new ServiceName("Acme.Shared.Contracts"), signal.TargetService);
        Assert.Equal(CommunicationType.DirectReference, signal.Communication);
        Assert.Equal(DependencyKind.DirectReference, signal.Kind);
    }

    // P2-04: a package reference matching another manifest service's PackageId is the same edge.
    [Fact]
    public void Detect_PackageReferenceMatchingAnotherServicePackageId_RecordsADirectReferenceEdge()
    {
        var contracts = WriteProject("Acme.Shared.Contracts", "Acme.Shared.Contracts.csproj", "    <Compile Include=\"X.cs\" />");
        var orders = WriteProject(
            "Acme.Orders",
            "Acme.Orders.csproj",
            "    <PackageReference Include=\"Acme.Shared.Contracts\" />");

        var signals = Detect(
            orders,
            "Acme.Orders",
            Service("Acme.Shared.Contracts", contracts, "Acme.Shared.Contracts"),
            Service("Acme.Orders", orders, "Acme.Orders"));

        var signal = Assert.Single(signals);
        Assert.Equal(new ServiceName("Acme.Shared.Contracts"), signal.TargetService);
        Assert.Equal("Acme.Shared.Contracts", signal.RawTarget);
        Assert.Equal(CommunicationType.DirectReference, signal.Communication);
    }

    // P2-05: a public third-party library matches no manifest service and must produce no edge.
    [Fact]
    public void Detect_PackageReferenceToAWellKnownPublicLibrary_RecordsNoEdge()
    {
        var contracts = WriteProject("Acme.Shared.Contracts", "Acme.Shared.Contracts.csproj", "    <Compile Include=\"X.cs\" />");
        var orders = WriteProject(
            "Acme.Orders",
            "Acme.Orders.csproj",
            """
                <PackageReference Include="Newtonsoft.Json" />
                <PackageReference Include="Serilog" />
                <PackageReference Include="Microsoft.Extensions.Http" />
            """);

        var signals = Detect(
            orders,
            "Acme.Orders",
            Service("Acme.Shared.Contracts", contracts, "Acme.Shared.Contracts"),
            Service("Acme.Orders", orders, "Acme.Orders"));

        Assert.Empty(signals);
    }

    [Fact]
    public void Detect_MixOfInternalAndPublicPackageReferences_RecordsOnlyTheInternalOne()
    {
        var contracts = WriteProject("Acme.Shared.Contracts", "Acme.Shared.Contracts.csproj", "    <Compile Include=\"X.cs\" />");
        var orders = WriteProject(
            "Acme.Orders",
            "Acme.Orders.csproj",
            """
                <PackageReference Include="Newtonsoft.Json" />
                <PackageReference Include="Acme.Shared.Contracts" />
            """);

        var signals = Detect(
            orders,
            "Acme.Orders",
            Service("Acme.Shared.Contracts", contracts, "Acme.Shared.Contracts"),
            Service("Acme.Orders", orders, "Acme.Orders"));

        Assert.Equal("Acme.Shared.Contracts", Assert.Single(signals).RawTarget);
    }

    // P2-05's converse: a project reference within the same service is internal structure.
    [Fact]
    public void Detect_ProjectReferenceWithinTheSameService_RecordsNoEdge()
    {
        var core = WriteProject("Acme.Orders", "Acme.Orders.Core.csproj", "    <Compile Include=\"X.cs\" />");
        var api = WriteProject("Acme.Orders", "Acme.Orders.Api.csproj", "    <ProjectReference Include=\"Acme.Orders.Core.csproj\" />");

        var sameService = new ServiceDescriptor(
            new ServiceName("Acme.Orders"),
            Path.GetDirectoryName(api)!,
            ServiceBoundaryKind.LooseProjects,
            null,
            [core, api],
            []);

        Assert.Empty(Detect(api, "Acme.Orders", sameService));
    }

    [Fact]
    public void Detect_ReferenceEvidence_PointsAtTheProjectFileLineDeclaringIt()
    {
        var contracts = WriteProject("Acme.Shared.Contracts", "Acme.Shared.Contracts.csproj", "    <Compile Include=\"X.cs\" />");
        var orders = WriteProject(
            "Acme.Orders",
            "Acme.Orders.csproj",
            "    <ProjectReference Include=\"..\\Acme.Shared.Contracts\\Acme.Shared.Contracts.csproj\" />");

        var signal = Assert.Single(Detect(orders, "Acme.Orders", Service("Acme.Shared.Contracts", contracts)));

        Assert.Equal(orders, signal.Location.FilePath);
        Assert.Equal(3, signal.Location.Line);
    }

    [Fact]
    public void Detect_DirectReferenceSignal_IsNotClassifiedThroughConfigResolution()
    {
        var contracts = WriteProject("Acme.Shared.Contracts", "Acme.Shared.Contracts.csproj", "    <Compile Include=\"X.cs\" />");
        var orders = WriteProject(
            "Acme.Orders",
            "Acme.Orders.csproj",
            "    <PackageReference Include=\"Acme.Shared.Contracts\" />");

        var signal = Assert.Single(
            Detect(orders, "Acme.Orders", Service("Acme.Shared.Contracts", contracts, "Acme.Shared.Contracts")));

        Assert.Equal(ResolutionKind.NotApplicable, signal.Resolution);
        Assert.Null(signal.Role);
    }

    [Fact]
    public void Detect_MalformedProjectFile_RecordsNoEdgeAndDoesNotThrow()
    {
        var directory = Path.Combine(_root, "Broken");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "Broken.csproj");
        File.WriteAllText(path, "<Project><ItemGroup>");

        Assert.Empty(Detect(path, "Acme.Broken"));
    }

    [Fact]
    public void DirectReferenceDetector_ImplementsTheProjectLevelContractNotTheDocumentLevelOne()
    {
        Assert.IsAssignableFrom<IProjectDependencyDetector>(new DirectReferenceDetector());
        Assert.False(typeof(IDocumentDependencyDetector).IsAssignableFrom(typeof(DirectReferenceDetector)));
    }
}
