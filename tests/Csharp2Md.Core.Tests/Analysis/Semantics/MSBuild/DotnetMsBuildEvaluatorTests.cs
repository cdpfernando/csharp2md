using System.Diagnostics;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Csharp2Md.Core.Facts.Identity;

namespace Csharp2Md.Core.Tests.Analysis.Semantics.MSBuild;

[Trait("Category", "Integration")]
public sealed class DotnetMsBuildEvaluatorTests
{
    [Fact]
    public async Task HealthyProject_EvaluatesEveryRequiredPropertyAndItemPerTarget()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject(
            "Healthy.csproj",
            fixture.SdkProject(
                """
                <PropertyGroup>
                  <OutputType>Exe</OutputType>
                  <AssemblyName>Acme.Healthy</AssemblyName>
                  <RootNamespace>Acme.Root</RootNamespace>
                  <DefineConstants>ALPHA;BETA</DefineConstants>
                  <LangVersion>preview</LangVersion>
                  <Nullable>enable</Nullable>
                </PropertyGroup>
                <ItemGroup>
                  <Compile Include="Probe.cs" />
                  <ProjectReference Include="Referenced.csproj" />
                  <PackageReference Include="Example.Package" Version="1.0.0" />
                  <Reference Include="Example.Reference" />
                </ItemGroup>
                """));
        fixture.WriteFile("Probe.cs", "internal sealed class Probe;");

        var result = await EvaluateAsync(project);

        var target = Assert.Single(result.Targets);
        Assert.True(target.Succeeded);
        Assert.Equal("Microsoft.NET.Sdk", result.DeclaredSdk);
        Assert.Equal("net10.0", target.TargetFramework);
        Assert.Equal("Exe", target.Properties["OutputType"]);
        Assert.Equal("Acme.Healthy", target.Properties["AssemblyName"]);
        Assert.Equal("Acme.Root", target.Properties["RootNamespace"]);
        Assert.Contains("ALPHA", target.Properties["DefineConstants"]);
        Assert.Equal("preview", target.Properties["LangVersion"]);
        Assert.Equal("enable", target.Properties["Nullable"]);
        Assert.Contains(target.Items["Compile"], item => item.Identity.EndsWith("Probe.cs", StringComparison.Ordinal));
        Assert.Contains(target.Items["ProjectReference"], item => item.Identity.EndsWith("Referenced.csproj", StringComparison.Ordinal));
        Assert.Contains(target.Items["PackageReference"], item => item.Identity == "Example.Package");
        Assert.Contains(target.Items["Reference"], item => item.Identity == "Example.Reference");
        Assert.False(File.Exists(fixture.TargetMarkerPath));
    }

    [Fact]
    public async Task MultiTargetProject_EvaluatesEachTargetAsAnIndependentScope()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject(
            "Multi.csproj",
            fixture.SdkProject(
                """
                <PropertyGroup>
                  <TargetFramework></TargetFramework>
                  <TargetFrameworks>net9.0;net10.0</TargetFrameworks>
                  <AssemblyName Condition="'$(TargetFramework)' == 'net9.0'">Acme.Nine</AssemblyName>
                  <AssemblyName Condition="'$(TargetFramework)' == 'net10.0'">Acme.Ten</AssemblyName>
                </PropertyGroup>
                """));

        var result = await EvaluateAsync(project);

        Assert.Equal(["net10.0", "net9.0"], result.TargetFrameworks.ToArray());
        Assert.Equal(2, result.Targets.Length);
        Assert.Equal("Acme.Ten", result.Targets.Single(target => target.TargetFramework == "net10.0").Properties["AssemblyName"]);
        Assert.Equal("Acme.Nine", result.Targets.Single(target => target.TargetFramework == "net9.0").Properties["AssemblyName"]);
        Assert.NotEqual(result.Targets[0].TargetId, result.Targets[1].TargetId);
    }

    [Fact]
    public async Task Evaluation_NeverExecutesCustomTargetsOrRestore()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject("Safe.csproj", fixture.SdkProject());

        var result = await EvaluateAsync(project);

        Assert.False(result.TimedOut);
        Assert.Empty(result.Diagnostics);
        Assert.False(File.Exists(fixture.TargetMarkerPath));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "obj", "project.assets.json")));
    }

    [Fact]
    public async Task ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml()
    {
        using var fixture = EvaluationFixture.Create();
        var imported = fixture.WriteFile(
            "Imported.props",
            "<Project><PropertyGroup><ImportedValue>present</ImportedValue></PropertyGroup></Project>");
        var project = fixture.WriteProject(
            "Imported.csproj",
            fixture.SdkProject("<Import Project=\"Imported.props\" />"));

        var before = Directory.GetFiles(Path.GetTempPath(), "csharp2md-*.preprocessed.xml").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = await EvaluateAsync(project);
        var after = Directory.GetFiles(Path.GetTempPath(), "csharp2md-*.preprocessed.xml").ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.GetFullPath(imported), result.EvaluatedImports, StringComparer.OrdinalIgnoreCase);
        Assert.True(before.SetEquals(after));
    }

    [Fact]
    public async Task Extensions_AreInventoriedAsPathsWithoutLoadingAssemblies()
    {
        using var fixture = EvaluationFixture.Create();
        var analyzerMarker = Path.Combine(fixture.Root, "analyzer-loaded.marker");
        var generatorMarker = Path.Combine(fixture.Root, "generator-loaded.marker");
        var project = fixture.WriteProject(
            "Extensions.csproj",
            fixture.SdkProject(
                """
                <ItemGroup>
                  <Analyzer Include="MarkerAnalyzer.dll" />
                  <Analyzer Include="MarkerGenerator.dll" ExtensionKind="Generator" />
                </ItemGroup>
                """));

        var result = await EvaluateAsync(project);

        Assert.Contains(Path.Combine(fixture.Root, "MarkerAnalyzer.dll"), result.AnalyzerPaths);
        Assert.Contains(Path.Combine(fixture.Root, "MarkerGenerator.dll"), result.AnalyzerPaths);
        Assert.Equal([Path.Combine(fixture.Root, "MarkerGenerator.dll")], result.GeneratorPaths.ToArray());
        Assert.False(File.Exists(analyzerMarker));
        Assert.False(File.Exists(generatorMarker));
    }

    [Fact]
    public async Task MissingSdk_ReturnsProjectScopedDegradation()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject(
            "MissingSdk.csproj",
            "<Project Sdk=\"Csharp2Md.Intentionally.Missing.Sdk/99.0.0\" />");

        var result = await EvaluateAsync(project);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("C2M-EVAL-001", diagnostic.Code);
        Assert.Equal(ProjectFactId.Create("MissingSdk.csproj").ToFactId(), diagnostic.ScopeId);
        Assert.Empty(result.Targets);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task IncompleteRestore_RetainsEvaluatedPackageReferenceWithoutRestoring()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject(
            "Unrestored.csproj",
            fixture.SdkProject(
                "<ItemGroup><PackageReference Include=\"Package.That.Does.Not.Exist\" Version=\"99.0.0\" /></ItemGroup>"));

        var result = await EvaluateAsync(project);

        Assert.Contains(Assert.Single(result.Targets).Items["PackageReference"], item => item.Identity == "Package.That.Does.Not.Exist");
        Assert.False(File.Exists(Path.Combine(fixture.Root, "obj", "project.assets.json")));
    }

    [Fact]
    public async Task InvalidProjectReference_RetainsReferenceAsEvaluatedEvidence()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject(
            "InvalidReference.csproj",
            fixture.SdkProject("<ItemGroup><ProjectReference Include=\"Missing.csproj\" /></ItemGroup>"));

        var result = await EvaluateAsync(project);

        Assert.Contains(Assert.Single(result.Targets).Items["ProjectReference"], item => item.Identity.EndsWith("Missing.csproj", StringComparison.Ordinal));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void UntrustedOptions_AreRejectedBeforeAnyProcessCanStart()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject("Untrusted.csproj", fixture.SdkProject());
        var runner = new RecordingProcessRunner();
        var evaluator = new DotnetMsBuildEvaluator(runner);

        var exception = Assert.Throws<ArgumentException>(() => TrustedProjectEvaluationRequest.Create(
            ProjectFactId.Create("Untrusted.csproj"),
            project,
            AnalysisOptions.Default));

        Assert.Equal("Project evaluation requires trusted semantic analysis. (Parameter 'options')", exception.Message);
        Assert.Empty(runner.Invocations);
        _ = evaluator;
    }

    [Fact]
    public async Task CommandConstruction_UsesArgumentListAndContainsNoExecutableTargetSwitch()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject("Arguments.csproj", fixture.SdkProject());
        var runner = new RecordingProcessRunner();
        runner.Results.Enqueue(JsonResult("net10.0"));
        runner.Results.Enqueue(JsonResult("net10.0"));
        runner.Results.Enqueue(new EvaluationProcessResult(1, string.Empty, "preprocess unavailable", []));
        var evaluator = new DotnetMsBuildEvaluator(runner);

        var result = await evaluator.EvaluateAsync(Request(project), CancellationToken.None);

        Assert.Single(result.Targets);
        Assert.Equal(3, runner.Invocations.Count);
        Assert.All(runner.Invocations, invocation =>
        {
            Assert.Equal(string.Empty, invocation.Arguments);
            Assert.Equal("msbuild", invocation.ArgumentList[0]);
            Assert.DoesNotContain(invocation.ArgumentList, IsForbiddenArgument);
        });
        Assert.Single(runner.Invocations, invocation => invocation.ArgumentList.Any(argument => argument == "-property:TargetFramework=net10.0"));
    }

    [Fact]
    public async Task ServiceTimeout_ReturnsScopedDegradationWithoutCancellingCaller()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject("Timeout.csproj", fixture.SdkProject());
        var evaluator = new DotnetMsBuildEvaluator(new NeverCompletingProcessRunner());
        var request = TrustedProjectEvaluationRequest.Create(
            ProjectFactId.Create("Timeout.csproj"),
            project,
            TrustedOptions(TimeSpan.FromMilliseconds(50)));

        var result = await evaluator.EvaluateAsync(request, CancellationToken.None);

        Assert.True(result.TimedOut);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("C2M-EVAL-003", diagnostic.Code);
        Assert.Equal(request.ProjectId.ToFactId(), diagnostic.ScopeId);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesItsOriginalToken()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject("Cancelled.csproj", fixture.SdkProject());
        var evaluator = new DotnetMsBuildEvaluator(new NeverCompletingProcessRunner());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => evaluator.EvaluateAsync(Request(project), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private static bool IsForbiddenArgument(string argument) =>
        argument.Equals("build", StringComparison.OrdinalIgnoreCase)
        || argument.Equals("restore", StringComparison.OrdinalIgnoreCase)
        || argument.Equals("publish", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("-target", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("-t:", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("-getTargetResult", StringComparison.OrdinalIgnoreCase);

    private static EvaluationProcessResult JsonResult(string targetFramework) => new(
        0,
        $$"""
        {
          "Properties": {
            "TargetFramework": "{{targetFramework}}",
            "TargetFrameworks": "",
            "OutputType": "Library",
            "AssemblyName": "Arguments",
            "RootNamespace": "Arguments",
            "DefineConstants": "",
            "LangVersion": "preview",
            "Nullable": "enable"
          },
          "Items": { "Compile": [], "ProjectReference": [], "PackageReference": [], "Reference": [], "Analyzer": [] }
        }
        """,
        string.Empty,
        []);

    private static Task<ProjectEvaluationResult> EvaluateAsync(string projectPath) =>
        new DotnetMsBuildEvaluator().EvaluateAsync(Request(projectPath), CancellationToken.None);

    private static TrustedProjectEvaluationRequest Request(string projectPath) =>
        TrustedProjectEvaluationRequest.Create(
            ProjectFactId.Create(Path.GetFileName(projectPath)),
            projectPath,
            TrustedOptions());

    private static AnalysisOptions TrustedOptions(TimeSpan? timeout = null) => new()
    {
        Mode = AnalysisMode.Semantic,
        Trust = TrustMode.TrustedSolution,
        ServiceTimeout = timeout ?? TimeSpan.FromMinutes(1),
    };

    private sealed class RecordingProcessRunner : IEvaluationProcessRunner
    {
        public Queue<EvaluationProcessResult> Results { get; } = new();

        public List<ProcessInvocation> Invocations { get; } = [];

        public Task<EvaluationProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            Invocations.Add(new ProcessInvocation(startInfo.Arguments, startInfo.ArgumentList.ToImmutableArray()));
            return Task.FromResult(Results.Dequeue());
        }
    }

    private sealed class NeverCompletingProcessRunner : IEvaluationProcessRunner
    {
        public async Task<EvaluationProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        }
    }

    private sealed record ProcessInvocation(string Arguments, ImmutableArray<string> ArgumentList);

    private sealed class EvaluationFixture : IDisposable
    {
        private EvaluationFixture(string root)
        {
            Root = root;
            TargetMarkerPath = Path.Combine(root, "target-executed.marker");
        }

        public string Root { get; }

        public string TargetMarkerPath { get; }

        public static EvaluationFixture Create() =>
            new(Directory.CreateTempSubdirectory("c2m-production-eval-").FullName);

        public string WriteProject(string relativePath, string content) => WriteFile(relativePath, content);

        public string WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            File.WriteAllText(path, content);
            return path;
        }

        public string SdkProject(string additionalXml = "") =>
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <MarkerPath>__MARKER__</MarkerPath>
              </PropertyGroup>
              __ADDITIONAL__
              <Target Name="Forbidden" BeforeTargets="Build">
                <WriteLinesToFile File="$(MarkerPath)" Lines="executed" />
              </Target>
            </Project>
            """
            .Replace("__MARKER__", TargetMarkerPath, StringComparison.Ordinal)
            .Replace("__ADDITIONAL__", additionalXml, StringComparison.Ordinal);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
