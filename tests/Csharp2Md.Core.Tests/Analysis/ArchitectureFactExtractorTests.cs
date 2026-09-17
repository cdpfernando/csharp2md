using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Extraction;
using Csharp2Md.Core.Analysis.Inventory;
using Csharp2Md.Core.Analysis.Semantics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Analysis;

public sealed class ArchitectureFactExtractorTests
{
    private static readonly string SyntheticRoot =
        Path.Combine(CoreTestPaths.RepoRoot, "fixtures", "SyntheticSolution");

    [Fact]
    [Trait("Requirement", "DEP-08")]
    public void Extract_ProjectNameSuggestingService_WithoutExeOrHost_DoesNotCreateDeploymentUnit()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var project = CanonicalIdentity.CreateProject(solution, "src/Payments.Service/Payments.Service.csproj");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");

        var result = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution,
            project,
            variant,
            ImmutableArray<InventoriedSourceDocument>.Empty,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """,
            Compilation: null));

        Assert.DoesNotContain(result.Entities, entity => entity.Kind == EntityKind.DeploymentUnit);
        Assert.True(ArchitectureFactExtractor.LooksLikeDeploymentUnitName("Payments.Service"));
    }

    [Fact]
    [Trait("Requirement", "DEP-08")]
    public void Extract_DirectoryOrAssemblyNameAlone_NeverCreatesDeploymentUnit()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var project = CanonicalIdentity.CreateProject(solution, "services/orders-api/orders-api.csproj");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");

        var result = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution,
            project,
            variant,
            ImmutableArray<InventoriedSourceDocument>.Empty,
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            Compilation: null));

        Assert.DoesNotContain(result.Entities, entity => entity.Kind == EntityKind.DeploymentUnit);
        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.Project);
    }

    [Fact]
    [Trait("Requirement", "PKG-02")]
    public void Extract_OutputTypeExe_CreatesDeploymentUnitWithProjectFileEvidence()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var project = CanonicalIdentity.CreateProject(solution, "src/Orders/Orders.csproj");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");

        var result = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution,
            project,
            variant,
            ImmutableArray<InventoriedSourceDocument>.Empty,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """,
            Compilation: null));

        var deployment = Assert.Single(result.Entities, entity => entity.Kind == EntityKind.DeploymentUnit);
        var occurrence = Assert.Single(result.Occurrences, item => item.EntityCanonicalKey == deployment.CanonicalKey);
        var evidence = Assert.Single(result.Evidence, record => occurrence.EvidenceCanonicalKeys.Contains(record.CanonicalKey));
        Assert.Equal("deployment:output-type-exe", occurrence.ShapeDigest);
        Assert.NotEqual(string.Empty, evidence.ContentDigest);
        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.Component);
    }

    [Fact]
    [Trait("Requirement", "PKG-02")]
    [Trait("Requirement", "CRT-08")]
    public void Extract_ControllerHttpAction_EmitsEntryPointAndBoundaryOperation()
    {
        var compilation = CreateCompilation(
            "Orders.csproj",
            """
            namespace Microsoft.AspNetCore.Mvc
            {
                public abstract class ControllerBase {}
                [System.AttributeUsage(System.AttributeTargets.Method)]
                public sealed class HttpGetAttribute : System.Attribute
                {
                    public HttpGetAttribute(string template) {}
                }
            }
            namespace App.Api
            {
                public sealed class OrdersController : Microsoft.AspNetCore.Mvc.ControllerBase
                {
                    [Microsoft.AspNetCore.Mvc.HttpGet("orders/{id}")]
                    public string Get(string id) => id;
                }
            }
            """);

        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var project = CanonicalIdentity.CreateProject(solution, "src/App/App.csproj");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");

        var result = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution,
            project,
            variant,
            ImmutableArray<InventoriedSourceDocument>.Empty,
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Library</OutputType></PropertyGroup></Project>",
            compilation));

        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.EntryPoint && entity.DisplayName == "Get");
        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.BoundaryOperation);
        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.Component);
        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.Symbol && entity.DisplayName == "OrdersController");
    }

    [Fact]
    [Trait("Requirement", "PKG-02")]
    public void Extract_MainMethod_EmitsEntryPoint()
    {
        var compilation = CreateCompilation(
            "App.csproj",
            """
            public static class Program
            {
                public static void Main(string[] args) {}
            }
            """);

        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var project = CanonicalIdentity.CreateProject(solution, "src/App/App.csproj");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");

        var result = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution,
            project,
            variant,
            ImmutableArray<InventoriedSourceDocument>.Empty,
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>",
            compilation));

        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.EntryPoint && entity.DisplayName == "Main");
    }

    [Fact]
    [Trait("Requirement", "PKG-02")]
    public void Extract_WebApplicationCreateBuilder_CreatesDeploymentUnitFromHostEvidence()
    {
        var compilation = CreateCompilation(
            "App.csproj",
            """
            namespace Microsoft.AspNetCore.Builder
            {
                public sealed class WebApplicationBuilder {}
                public static class WebApplication
                {
                    public static WebApplicationBuilder CreateBuilder(string[] args) => new();
                }
            }
            public static class Program
            {
                public static void Main(string[] args)
                {
                    _ = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);
                }
            }
            """);

        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var project = CanonicalIdentity.CreateProject(solution, "src/App/App.csproj");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");

        var result = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution,
            project,
            variant,
            ImmutableArray<InventoriedSourceDocument>.Empty,
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Library</OutputType></PropertyGroup></Project>",
            compilation));

        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.DeploymentUnit);
        Assert.Contains(result.Occurrences, occurrence => occurrence.ShapeDigest == "deployment:host-bootstrap");
    }

    [Fact]
    [Trait("Requirement", "PKG-05")]
    [Trait("Requirement", "VAR-04")]
    public void Extract_RetainsProductionAndTestProvenanceOnDocuments()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var project = CanonicalIdentity.CreateProject(solution, "src/App/App.csproj");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");
        var documents = ImmutableArray.Create(
            new InventoriedSourceDocument("src/App/Program.cs", SourceDocumentKind.CSharpSource, IsTest: false, "abc", 3),
            new InventoriedSourceDocument("tests/App.Tests/FooTests.cs", SourceDocumentKind.CSharpSource, IsTest: true, "def", 4));

        var result = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution,
            project,
            variant,
            documents,
            null,
            Compilation: null));

        Assert.Contains(result.Occurrences, occurrence => occurrence.ShapeDigest == "document:production");
        Assert.Contains(result.Occurrences, occurrence => occurrence.ShapeDigest == "document:test");
        Assert.All(result.Occurrences, occurrence => Assert.Equal(variant, occurrence.Variant));
        Assert.All(result.Occurrences, occurrence => Assert.Equal(project, occurrence.Project));
    }

    [Fact]
    [Trait("Requirement", "PKG-09")]
    public void Extract_DoesNotEmitBusinessRuleOrQualityLabels()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var project = CanonicalIdentity.CreateProject(solution, "src/App/App.csproj");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");

        var result = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution,
            project,
            variant,
            ImmutableArray<InventoriedSourceDocument>.Empty,
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>",
            Compilation: null));

        Assert.All(result.Entities, entity =>
        {
            Assert.DoesNotContain("quality", entity.DisplayName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("risk", entity.DisplayName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("business-rule", entity.CanonicalKey, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("score", entity.QualifiedName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        });
        Assert.All(result.Occurrences, occurrence =>
        {
            Assert.DoesNotContain("quality", occurrence.ShapeDigest, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("coupling-score", occurrence.ShapeDigest, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    [Trait("Requirement", "PKG-02")]
    public void Extract_AlwaysEmitsSolutionAndProjectRootsWithEvidence()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var project = CanonicalIdentity.CreateProject(solution, "src/App/App.csproj");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");

        var result = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution,
            project,
            variant,
            ImmutableArray<InventoriedSourceDocument>.Empty,
            null,
            Compilation: null));

        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.Solution);
        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.Project);
        Assert.NotEmpty(result.Evidence);
        Assert.All(result.Occurrences, occurrence => Assert.NotEmpty(occurrence.EvidenceCanonicalKeys));
    }

    [Fact]
    [Trait("Requirement", "CRT-08")]
    [Trait("Requirement", "DEP-08")]
    public async Task Extract_AcmeOrders_EmitsDeploymentComponentEntryAndBoundary()
    {
        var variant = new PlannedProjectVariant("Acme.Orders/Acme.Orders.csproj", "net10.0");
        await using var workspace = await ProjectVariantWorkspace.OpenAsync(SyntheticRoot, variant);
        var compilation = await workspace.GetRootCompilationAsync();
        Assert.NotNull(compilation);

        var solution = CanonicalIdentity.CreateSolution("acme-orders", "Acme.Orders/Acme.Orders.slnx");
        var project = CanonicalIdentity.CreateProject(solution, "Acme.Orders/Acme.Orders.csproj");
        var analysisVariant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");
        var projectFile = await File.ReadAllTextAsync(Path.Combine(SyntheticRoot, "Acme.Orders", "Acme.Orders.csproj"));
        var inventory = SourceInventory.Collect(
            SyntheticRoot,
            Path.Combine(SyntheticRoot, "Acme.Orders"),
            new AnalysisPolicy(IncludeTests: false));

        var result = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution,
            project,
            analysisVariant,
            inventory.Accepted,
            projectFile,
            compilation));

        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.DeploymentUnit);
        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.Component);
        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.EntryPoint);
        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.BoundaryOperation);
        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.Document);
        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.Symbol);
    }

    [Fact]
    [Trait("Requirement", "DEP-08")]
    public async Task Extract_AcmeSharedContracts_DoesNotCreateDeploymentUnitFromLibraryName()
    {
        var variant = new PlannedProjectVariant("Acme.Shared.Contracts/Acme.Shared.Contracts.csproj", "net10.0");
        await using var workspace = await ProjectVariantWorkspace.OpenAsync(SyntheticRoot, variant);
        var compilation = await workspace.GetRootCompilationAsync();

        var solution = CanonicalIdentity.CreateSolution("acme-contracts", "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");
        var project = CanonicalIdentity.CreateProject(solution, "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");
        var analysisVariant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");
        var projectFile = await File.ReadAllTextAsync(Path.Combine(SyntheticRoot, "Acme.Shared.Contracts", "Acme.Shared.Contracts.csproj"));

        var result = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution,
            project,
            analysisVariant,
            ImmutableArray<InventoriedSourceDocument>.Empty,
            projectFile,
            compilation));

        Assert.DoesNotContain(result.Entities, entity => entity.Kind == EntityKind.DeploymentUnit);
        Assert.Contains(result.Entities, entity => entity.Kind == EntityKind.Project);
    }

    [Fact]
    [Trait("Requirement", "VAR-04")]
    public void Extract_OccurrencesAreVariantQualified()
    {
        var solution = CanonicalIdentity.CreateSolution("acme", "src/Acme.sln");
        var project = CanonicalIdentity.CreateProject(solution, "src/App/App.csproj");
        var net8 = CanonicalIdentity.CreateVariant("net8.0", "Release", [], "ci");
        var net10 = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");
        var projectFile = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>";

        var first = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution, project, net8, ImmutableArray<InventoriedSourceDocument>.Empty, projectFile, null));
        var second = ArchitectureFactExtractor.Extract(new ArchitectureExtractionInput(
            solution, project, net10, ImmutableArray<InventoriedSourceDocument>.Empty, projectFile, null));

        Assert.All(first.Occurrences, occurrence => Assert.Equal("net8.0", occurrence.Variant.TargetFramework));
        Assert.All(second.Occurrences, occurrence => Assert.Equal("net10.0", occurrence.Variant.TargetFramework));
        Assert.Equal(
            first.Entities.Single(entity => entity.Kind == EntityKind.DeploymentUnit).CanonicalKey,
            second.Entities.Single(entity => entity.Kind == EntityKind.DeploymentUnit).CanonicalKey);
    }

    [Fact]
    [Trait("Requirement", "PKG-09")]
    public void ReadOutputType_ParsesProjectFileWithoutGuessingFromPath()
    {
        Assert.Equal("Exe", ArchitectureFactExtractor.ReadOutputType("<Project><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>"));
        Assert.Null(ArchitectureFactExtractor.ReadOutputType("<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"));
        Assert.Null(ArchitectureFactExtractor.ReadOutputType(null));
    }

    private static Compilation CreateCompilation(string assemblyName, string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "src/App/Source.cs");
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };
        return CSharpCompilation.Create(
            assemblyName,
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
