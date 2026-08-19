using System.Security;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;

namespace Csharp2Md.Core.Tests.Analysis.Viability;

/// <summary>
/// Roslyn 5.6 sanitation evidence. Installed XML and the official API contract state that
/// <see cref="Project.WithAnalyzerReferences(IEnumerable{Microsoft.CodeAnalysis.Diagnostics.AnalyzerReference})"/>
/// replaces all analyzer references. The probe calls it before the first
/// <see cref="Project.GetCompilationAsync(CancellationToken)"/> request.
/// See https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.project.withanalyzerreferences.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RoslynSanitationProbeTests
{
    [Fact]
    public async Task WorkspaceOpening_ExecutesCustomTargetButDoesNotLoadExtensions()
    {
        using var fixture = RoslynFixture.Create();
        fixture.CreateMarkerExtensions();
        var projectPath = fixture.WriteProject("OpenProbe.csproj", assemblyName: "OpenProbe");

        using var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: CancellationToken.None);
        var evidence = fixture.ObserveWorkspaceOpen(project);

        Assert.NotEmpty(project.AnalyzerReferences);
        Assert.True(evidence.CustomTargetExecuted);
        Assert.False(evidence.AnalyzerAssemblyLoaded);
        Assert.False(evidence.GeneratorAssemblyLoaded);
    }

    [Fact]
    public async Task SanitizedProject_RemovesAnalyzerReferencesBeforeCompilationAndBindsWithoutExtensionExecution()
    {
        using var fixture = RoslynFixture.Create();
        fixture.CreateMarkerExtensions();
        var projectPath = fixture.WriteProject("Sanitized.csproj", assemblyName: "Sanitized");

        using var workspace = MSBuildWorkspace.Create();
        var openedProject = await workspace.OpenProjectAsync(projectPath, cancellationToken: CancellationToken.None);
        Assert.NotEmpty(openedProject.AnalyzerReferences);

        var sanitizedProject = openedProject.WithAnalyzerReferences([]);
        Assert.Empty(sanitizedProject.AnalyzerReferences);
        var compilation = await sanitizedProject.GetCompilationAsync(CancellationToken.None);
        var syntaxTree = Assert.Single(compilation!.SyntaxTrees, tree => tree.FilePath.EndsWith("Probe.cs", StringComparison.Ordinal));
        var model = compilation.GetSemanticModel(syntaxTree);
        var declaration = syntaxTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Single();

        Assert.Equal("Probe", model.GetDeclaredSymbol(declaration)!.Name);
        Assert.False(File.Exists(fixture.AnalyzerLoadedMarker));
        Assert.False(File.Exists(fixture.GeneratorLoadedMarker));
        Assert.False(File.Exists(fixture.GeneratorExecutedMarker));
        Assert.True(File.Exists(fixture.TargetMarker));
    }

    [Fact]
    public async Task MultiTargetSanitizedCompilations_RetainDistinctTargetIdentities()
    {
        using var fixture = RoslynFixture.Create();
        fixture.CreateMarkerExtensions();
        var projectPath = fixture.WriteMultiTargetProject();

        using var net9Workspace = MSBuildWorkspace.Create(new Dictionary<string, string> { ["TargetFramework"] = "net9.0" });
        using var net10Workspace = MSBuildWorkspace.Create(new Dictionary<string, string> { ["TargetFramework"] = "net10.0" });
        var net9Project = await net9Workspace.OpenProjectAsync(projectPath, cancellationToken: CancellationToken.None);
        var net10Project = await net10Workspace.OpenProjectAsync(projectPath, cancellationToken: CancellationToken.None);

        var net9Compilation = await net9Project.WithAnalyzerReferences([]).GetCompilationAsync(CancellationToken.None);
        var net10Compilation = await net10Project.WithAnalyzerReferences([]).GetCompilationAsync(CancellationToken.None);

        Assert.Equal("Probe.Net9", net9Compilation!.AssemblyName);
        Assert.Equal("Probe.Net10", net10Compilation!.AssemblyName);
        Assert.NotEqual(net9Compilation.AssemblyName, net10Compilation.AssemblyName);
        Assert.False(File.Exists(fixture.AnalyzerLoadedMarker));
        Assert.False(File.Exists(fixture.GeneratorLoadedMarker));
        Assert.False(File.Exists(fixture.GeneratorExecutedMarker));
    }

    [Fact]
    public async Task InvalidReference_SanitizedCompilationRetainsSourceAndScopesTheFailure()
    {
        using var fixture = RoslynFixture.Create();
        fixture.CreateMarkerExtensions();
        var projectPath = fixture.WriteProject(
            "BrokenReference.csproj",
            assemblyName: "BrokenReference",
            additionalXml: "<ItemGroup><Reference Include=\"Missing.Reference\"><HintPath>missing.dll</HintPath></Reference></ItemGroup>");
        File.WriteAllText(
            Path.Combine(fixture.Root, "Probe.cs"),
            "namespace MarkerFixture; public sealed class Probe : Missing.Reference.Type;");

        using var workspace = MSBuildWorkspace.Create();
        var openedProject = await workspace.OpenProjectAsync(projectPath, cancellationToken: CancellationToken.None);
        var compilation = await openedProject.WithAnalyzerReferences([]).GetCompilationAsync(CancellationToken.None);

        Assert.NotNull(compilation);
        Assert.Contains(compilation.SyntaxTrees, tree => tree.FilePath.EndsWith("Probe.cs", StringComparison.Ordinal));
        Assert.Contains(
            compilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                && diagnostic.GetMessage().Contains("Missing", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(fixture.AnalyzerLoadedMarker));
        Assert.False(File.Exists(fixture.GeneratorLoadedMarker));
    }

    private sealed record WorkspaceOpenEvidence(
        bool CustomTargetExecuted,
        bool AnalyzerAssemblyLoaded,
        bool GeneratorAssemblyLoaded);

    private sealed class RoslynFixture : IDisposable
    {
        private RoslynFixture(string root)
        {
            Root = root;
            TargetMarker = Path.Combine(root, "target-executed.marker");
            AnalyzerLoadedMarker = Path.Combine(root, "analyzer-loaded.marker");
            GeneratorLoadedMarker = Path.Combine(root, "generator-loaded.marker");
            GeneratorExecutedMarker = Path.Combine(root, "generator-executed.marker");
            AnalyzerPath = Path.Combine(root, "MarkerAnalyzer.dll");
            GeneratorPath = Path.Combine(root, "MarkerGenerator.dll");
        }

        public string Root { get; }

        public string TargetMarker { get; }

        public string AnalyzerLoadedMarker { get; }

        public string GeneratorLoadedMarker { get; }

        public string GeneratorExecutedMarker { get; }

        public string AnalyzerPath { get; }

        public string GeneratorPath { get; }

        public static RoslynFixture Create() => new(Directory.CreateTempSubdirectory("c2m-roslyn-").FullName);

        public void CreateMarkerExtensions()
        {
            EmitExtension(
                AnalyzerPath,
                $$"""
                using System.Collections.Immutable;
                using System.IO;
                using System.Runtime.CompilerServices;
                using Microsoft.CodeAnalysis;
                using Microsoft.CodeAnalysis.Diagnostics;

                internal static class LoadMarker
                {
                    [ModuleInitializer]
                    internal static void Initialize() => File.WriteAllText(@"{{AnalyzerLoadedMarker}}", "loaded");
                }

                [DiagnosticAnalyzer(LanguageNames.CSharp)]
                public sealed class MarkerAnalyzer : DiagnosticAnalyzer
                {
                    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [];
                    public override void Initialize(AnalysisContext context) { }
                }
                """,
                "MarkerAnalyzer");
            EmitExtension(
                GeneratorPath,
                $$"""
                using System.IO;
                using System.Runtime.CompilerServices;
                using Microsoft.CodeAnalysis;

                internal static class LoadMarker
                {
                    [ModuleInitializer]
                    internal static void Initialize() => File.WriteAllText(@"{{GeneratorLoadedMarker}}", "loaded");
                }

                [Generator]
                public sealed class MarkerGenerator : ISourceGenerator
                {
                    public void Initialize(GeneratorInitializationContext context) { }
                    public void Execute(GeneratorExecutionContext context)
                    {
                        File.WriteAllText(@"{{GeneratorExecutedMarker}}", "executed");
                        context.AddSource("Generated.g.cs", "internal sealed class Generated;");
                    }
                }
                """,
                "MarkerGenerator");
        }

        public string WriteProject(string fileName, string assemblyName, string additionalXml = "")
        {
            File.WriteAllText(Path.Combine(Root, "Probe.cs"), "namespace MarkerFixture; public sealed class Probe;");
            var projectPath = Path.Combine(Root, fileName);
            File.WriteAllText(
                projectPath,
                ProjectXml("<TargetFramework>net10.0</TargetFramework>", $"<AssemblyName>{assemblyName}</AssemblyName>", additionalXml));
            return projectPath;
        }

        public string WriteMultiTargetProject()
        {
            File.WriteAllText(Path.Combine(Root, "Probe.cs"), "namespace MarkerFixture; public sealed class Probe;");
            var projectPath = Path.Combine(Root, "Multi.csproj");
            File.WriteAllText(
                projectPath,
                ProjectXml(
                    "<TargetFrameworks>net9.0;net10.0</TargetFrameworks>",
                    "<AssemblyName Condition=\"'$(TargetFramework)' == 'net9.0'\">Probe.Net9</AssemblyName>" +
                    "<AssemblyName Condition=\"'$(TargetFramework)' == 'net10.0'\">Probe.Net10</AssemblyName>",
                    ""));
            return projectPath;
        }

        public WorkspaceOpenEvidence ObserveWorkspaceOpen(Project project) =>
            new(
                File.Exists(TargetMarker),
                File.Exists(AnalyzerLoadedMarker),
                File.Exists(GeneratorLoadedMarker));

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private string ProjectXml(string targetFrameworkXml, string assemblyNameXml, string additionalXml) =>
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {{targetFrameworkXml}}
                {{assemblyNameXml}}
                <EnableNETAnalyzers>false</EnableNETAnalyzers>
                <RunAnalyzers>false</RunAnalyzers>
              </PropertyGroup>
              <ItemGroup>
                <Analyzer Include="{{SecurityElement.Escape(AnalyzerPath)}}" />
                <Analyzer Include="{{SecurityElement.Escape(GeneratorPath)}}" />
              </ItemGroup>
              {{additionalXml}}
              <Target Name="ForbiddenWorkspaceTarget" BeforeTargets="Compile">
                <WriteLinesToFile File="{{SecurityElement.Escape(TargetMarker)}}" Lines="executed" />
              </Target>
            </Project>
            """;

        private static void EmitExtension(string outputPath, string source, string assemblyName)
        {
            var trustedPlatformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToList();
            trustedPlatformAssemblies.Add(MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location));
            trustedPlatformAssemblies.Add(MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location));
            var compilation = CSharpCompilation.Create(
                assemblyName,
                [CSharpSyntaxTree.ParseText(source)],
                trustedPlatformAssemblies,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var result = compilation.Emit(outputPath);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        }
    }
}
