using System.Diagnostics;
using System.Text.Json;

namespace Csharp2Md.Core.Tests.Analysis.Viability;

/// <summary>
/// Executable evidence for the Increment-0 MSBuild boundary.
/// Contracts: https://learn.microsoft.com/visualstudio/msbuild/evaluate-items-and-properties
/// and https://learn.microsoft.com/visualstudio/msbuild/msbuild-command-line-reference.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MsBuildEvaluationProbeTests
{
    [Fact]
    public async Task HealthyProject_QueryReturnsPropertiesAndItemsWithoutExecutingTarget()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject("Healthy.csproj", SdkProject(fixture.MarkerPath));
        fixture.WriteFile("Probe.cs", "internal sealed class Probe;");

        var result = await MsBuildProbe.QueryAsync(
            project,
            ["TargetFramework", "AssemblyName"],
            ["Compile"],
            cancellationToken: CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        using var json = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("net10.0", json.RootElement.GetProperty("Properties").GetProperty("TargetFramework").GetString());
        Assert.Equal("Healthy", json.RootElement.GetProperty("Properties").GetProperty("AssemblyName").GetString());
        Assert.Contains(
            json.RootElement.GetProperty("Items").GetProperty("Compile").EnumerateArray(),
            item => item.GetProperty("Identity").GetString() == "Probe.cs");
        AssertSafeEvaluation(result, fixture.MarkerPath);
    }

    [Fact]
    public async Task MultiTargetProject_QueryEachTargetFrameworkReturnsIndependentValues()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject(
            "Multi.csproj",
            SdkProject(
                fixture.MarkerPath,
                "<PropertyGroup>\n" +
                "  <TargetFrameworks>net9.0;net10.0</TargetFrameworks>\n" +
                "  <ProbeIdentity Condition=\"'$(TargetFramework)' == 'net9.0'\">nine</ProbeIdentity>\n" +
                "  <ProbeIdentity Condition=\"'$(TargetFramework)' == 'net10.0'\">ten</ProbeIdentity>\n" +
                "</PropertyGroup>"));

        var net9 = await MsBuildProbe.QueryAsync(
            project,
            ["TargetFramework", "ProbeIdentity"],
            [],
            ["-property:TargetFramework=net9.0"],
            CancellationToken.None);
        var net10 = await MsBuildProbe.QueryAsync(
            project,
            ["TargetFramework", "ProbeIdentity"],
            [],
            ["-property:TargetFramework=net10.0"],
            CancellationToken.None);

        Assert.Equal(0, net9.ExitCode);
        Assert.Equal(0, net10.ExitCode);
        Assert.Equal("nine", Property(net9, "ProbeIdentity"));
        Assert.Equal("ten", Property(net10, "ProbeIdentity"));
        Assert.NotEqual(Property(net9, "TargetFramework"), Property(net10, "TargetFramework"));
        AssertSafeEvaluation(net9, fixture.MarkerPath);
        AssertSafeEvaluation(net10, fixture.MarkerPath);
    }

    [Fact]
    public async Task ImportedProject_PreprocessingReturnsOnlyImportPathsAndDoesNotPersistExpandedXml()
    {
        using var fixture = EvaluationFixture.Create();
        var importedProps = fixture.WriteFile("Imported.props", "<Project><PropertyGroup><ImportedValue>from-import</ImportedValue></PropertyGroup></Project>");
        var project = fixture.WriteProject(
            "Imported.csproj",
            SdkProject(fixture.MarkerPath, "<Import Project=\"Imported.props\" />"));

        var query = await MsBuildProbe.QueryAsync(
            project,
            ["ImportedValue", "TargetFramework"],
            [],
            cancellationToken: CancellationToken.None);
        var preprocessing = await MsBuildProbe.FindImportedPathsAsync(
            project,
            [importedProps],
            CancellationToken.None);

        Assert.Equal(0, query.ExitCode);
        Assert.Equal("from-import", Property(query, "ImportedValue"));
        Assert.Equal([Path.GetFullPath(importedProps)], preprocessing.ImportPaths);
        Assert.False(File.Exists(preprocessing.ExpandedProjectPath));
        AssertSafeEvaluation(query, fixture.MarkerPath);
        AssertSafeArguments(preprocessing.Arguments);
        Assert.False(File.Exists(fixture.MarkerPath));
    }

    [Fact]
    public async Task MissingSdk_QueryReturnsDegradedResultWithoutExecutingTarget()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject(
            "MissingSdk.csproj",
            "<Project Sdk=\"Csharp2Md.Intentionally.Missing.Sdk/99.0.0\"><Target Name=\"Forbidden\" BeforeTargets=\"Build\"><WriteLinesToFile File=\"" +
            fixture.MarkerPath +
            "\" Lines=\"executed\" /></Target></Project>");

        var result = await MsBuildProbe.QueryAsync(
            project,
            ["TargetFramework", "AssemblyName"],
            [],
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Csharp2Md.Intentionally.Missing.Sdk", result.StandardError + result.StandardOutput);
        AssertSafeEvaluation(result, fixture.MarkerPath);
    }

    [Fact]
    public async Task IncompleteRestore_QuerySucceedsWithoutCreatingAssetsOrExecutingTarget()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject(
            "Unrestored.csproj",
            SdkProject(
                fixture.MarkerPath,
                "<ItemGroup><PackageReference Include=\"Package.That.Does.Not.Exist\" Version=\"99.0.0\" /></ItemGroup>"));

        var result = await MsBuildProbe.QueryAsync(
            project,
            ["TargetFramework", "AssemblyName"],
            ["PackageReference"],
            cancellationToken: CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Package.That.Does.Not.Exist", ItemIdentity(result, "PackageReference"));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "obj", "project.assets.json")));
        AssertSafeEvaluation(result, fixture.MarkerPath);
    }

    [Fact]
    public async Task InvalidProjectReference_QueryReturnsReferenceWithoutExecutingTarget()
    {
        using var fixture = EvaluationFixture.Create();
        var project = fixture.WriteProject(
            "InvalidReference.csproj",
            SdkProject(
                fixture.MarkerPath,
                "<ItemGroup><ProjectReference Include=\"Missing.csproj\" /></ItemGroup>"));

        var result = await MsBuildProbe.QueryAsync(
            project,
            ["TargetFramework", "AssemblyName"],
            ["ProjectReference"],
            cancellationToken: CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Missing.csproj", ItemIdentity(result, "ProjectReference"));
        AssertSafeEvaluation(result, fixture.MarkerPath);
    }

    [Fact]
    public async Task AnalyzerAndGeneratorReferences_QueryInventoriesPathsWithoutLoadingAssemblies()
    {
        using var fixture = EvaluationFixture.Create();
        var extensionMarker = Path.Combine(fixture.Root, "extension-loaded.marker");
        var project = fixture.WriteProject(
            "Extensions.csproj",
            SdkProject(
                fixture.MarkerPath,
                "<ItemGroup>\n" +
                "  <Analyzer Include=\"MarkerAnalyzer.dll\" ExtensionKind=\"Analyzer\" />\n" +
                "  <Analyzer Include=\"MarkerGenerator.dll\" ExtensionKind=\"Generator\" />\n" +
                "</ItemGroup>"));

        var result = await MsBuildProbe.QueryAsync(
            project,
            ["TargetFramework", "AssemblyName"],
            ["Analyzer"],
            cancellationToken: CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        using var json = JsonDocument.Parse(result.StandardOutput);
        var identities = json.RootElement.GetProperty("Items").GetProperty("Analyzer")
            .EnumerateArray()
            .Select(item => item.GetProperty("Identity").GetString()!)
            .ToArray();
        Assert.Equal(
            ["MarkerAnalyzer.dll", "MarkerGenerator.dll"],
            identities.Where(identity => Path.GetFileName(identity).StartsWith("Marker", StringComparison.Ordinal)).ToArray());
        Assert.False(File.Exists(extensionMarker));
        AssertSafeEvaluation(result, fixture.MarkerPath);
    }

    private static string Property(ProbeResult result, string name)
    {
        using var json = JsonDocument.Parse(result.StandardOutput);
        return json.RootElement.GetProperty("Properties").GetProperty(name).GetString()!;
    }

    private static string ItemIdentity(ProbeResult result, string itemName)
    {
        using var json = JsonDocument.Parse(result.StandardOutput);
        return json.RootElement.GetProperty("Items").GetProperty(itemName)[0].GetProperty("Identity").GetString()!;
    }

    private static void AssertSafeEvaluation(ProbeResult result, string markerPath)
    {
        AssertSafeArguments(result.Arguments);
        Assert.False(File.Exists(markerPath));
    }

    private static void AssertSafeArguments(IReadOnlyList<string> arguments)
    {
        Assert.Equal("msbuild", arguments[0]);
        Assert.DoesNotContain(
            arguments,
            argument => argument.Equals("build", StringComparison.OrdinalIgnoreCase)
                || argument.Equals("restore", StringComparison.OrdinalIgnoreCase)
                || argument.Equals("publish", StringComparison.OrdinalIgnoreCase)
                || argument.Equals("-target", StringComparison.OrdinalIgnoreCase)
                || argument.StartsWith("-target:", StringComparison.OrdinalIgnoreCase)
                || argument.Equals("-t", StringComparison.OrdinalIgnoreCase)
                || argument.StartsWith("-t:", StringComparison.OrdinalIgnoreCase)
                || argument.StartsWith("-getTargetResult", StringComparison.OrdinalIgnoreCase));
    }

    private static string SdkProject(string markerPath, string additionalXml = "") =>
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
            <MarkerPath>__MARKER__</MarkerPath>
          </PropertyGroup>
          <ItemGroup><Compile Include="Probe.cs" /></ItemGroup>
          __ADDITIONAL__
          <Target Name="Forbidden" BeforeTargets="Build">
            <WriteLinesToFile File="$(MarkerPath)" Lines="executed" />
          </Target>
        </Project>
        """
        .Replace("__MARKER__", markerPath, StringComparison.Ordinal)
        .Replace("__ADDITIONAL__", additionalXml, StringComparison.Ordinal);

    private sealed class EvaluationFixture : IDisposable
    {
        private EvaluationFixture(string root)
        {
            Root = root;
            MarkerPath = Path.Combine(root, "target-executed.marker");
        }

        public string Root { get; }

        public string MarkerPath { get; }

        public static EvaluationFixture Create() => new(Directory.CreateTempSubdirectory("c2m-eval-").FullName);

        public string WriteProject(string relativePath, string content) => WriteFile(relativePath, content);

        public string WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed record ProbeResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        IReadOnlyList<string> Arguments);

    private sealed record PreprocessResult(IReadOnlyList<string> ImportPaths, string ExpandedProjectPath, IReadOnlyList<string> Arguments);

    private static class MsBuildProbe
    {
        public static async Task<ProbeResult> QueryAsync(
            string projectPath,
            IReadOnlyList<string> properties,
            IReadOnlyList<string> items,
            IReadOnlyList<string>? globalProperties = null,
            CancellationToken cancellationToken = default)
        {
            var arguments = new List<string>
            {
                "msbuild",
                projectPath,
                "-nologo",
                $"-getProperty:{string.Join(',', properties)}",
            };
            if (items.Count > 0)
            {
                arguments.Add($"-getItem:{string.Join(',', items)}");
            }

            arguments.AddRange(globalProperties ?? []);
            return await RunAsync(arguments, Path.GetDirectoryName(projectPath)!, cancellationToken);
        }

        public static async Task<PreprocessResult> FindImportedPathsAsync(
            string projectPath,
            IReadOnlyList<string> candidateImportPaths,
            CancellationToken cancellationToken)
        {
            var expandedProjectPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "expanded.probe.xml");
            var arguments = new List<string> { "msbuild", projectPath, "-nologo", $"-preprocess:{expandedProjectPath}" };
            var result = await RunAsync(arguments, Path.GetDirectoryName(projectPath)!, cancellationToken);
            Assert.Equal(0, result.ExitCode);

            var expandedXml = await File.ReadAllTextAsync(expandedProjectPath, cancellationToken);
            var imports = candidateImportPaths
                .Select(Path.GetFullPath)
                .Where(path => expandedXml.Contains(path, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            File.Delete(expandedProjectPath);
            return new PreprocessResult(imports, expandedProjectPath, result.Arguments);
        }

        private static async Task<ProbeResult> RunAsync(
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new ProbeResult(process.ExitCode, await stdout, await stderr, arguments);
        }
    }
}
