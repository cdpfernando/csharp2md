using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;

namespace Csharp2Md.Core.Analysis.Semantics.MSBuild;

internal sealed partial class DotnetMsBuildEvaluator(
    IEvaluationProcessRunner? processRunner = null) : IProjectEvaluationAdapter
{
    internal static readonly ImmutableArray<string> EvaluatedPropertyNames =
    [
        "TargetFramework",
        "TargetFrameworks",
        "OutputType",
        "AssemblyName",
        "RootNamespace",
        "DefineConstants",
        "LangVersion",
        "Nullable",
    ];

    internal static readonly ImmutableArray<string> EvaluatedItemNames =
    [
        "Compile",
        "ProjectReference",
        "PackageReference",
        "Reference",
        "Analyzer",
    ];

    private readonly IEvaluationProcessRunner _processRunner = processRunner ?? new EvaluationProcessRunner();

    public async Task<ProjectEvaluationResult> EvaluateAsync(
        TrustedProjectEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using var serviceTimeout = new CancellationTokenSource(request.ServiceTimeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            serviceTimeout.Token);

        var declared = ReadDeclaredProject(request.ProjectPath);
        try
        {
            var outer = await QueryAsync(request.ProjectPath, targetFramework: null, linkedCancellation.Token)
                .ConfigureAwait(false);
            if (outer.ExitCode != 0 || !TryParseEvaluation(outer.StandardOutput, out var outerData))
            {
                return Failed(
                    request,
                    declared,
                    "C2M-EVAL-001",
                    "MSBuild project evaluation failed.",
                    outer.StandardError,
                    timedOut: false);
            }

            var targetFrameworks = FindTargetFrameworks(outerData.Properties);
            var targets = ImmutableArray.CreateBuilder<EvaluatedTarget>(targetFrameworks.Length);
            var diagnostics = ImmutableArray.CreateBuilder<AnalysisDiagnostic>();

            foreach (var targetFramework in targetFrameworks)
            {
                var targetResult = await QueryAsync(request.ProjectPath, targetFramework, linkedCancellation.Token)
                    .ConfigureAwait(false);
                if (targetResult.ExitCode == 0
                    && TryParseEvaluation(targetResult.StandardOutput, out var targetData))
                {
                    targets.Add(new EvaluatedTarget(
                        TargetFactId.Create(request.ProjectId, targetFramework),
                        targetFramework,
                        Succeeded: true,
                        targetData.Properties,
                        targetData.Items,
                        []));
                    continue;
                }

                var targetDiagnostic = Diagnostic(
                    "C2M-EVAL-002",
                    TargetFactId.Create(request.ProjectId, targetFramework).ToFactId(),
                    "MSBuild target-framework evaluation failed.",
                    targetResult.StandardError);
                diagnostics.Add(targetDiagnostic);
                targets.Add(new EvaluatedTarget(
                    TargetFactId.Create(request.ProjectId, targetFramework),
                    targetFramework,
                    Succeeded: false,
                    ImmutableDictionary<string, string>.Empty,
                    ImmutableDictionary<string, ImmutableArray<EvaluatedItem>>.Empty,
                    [targetDiagnostic]));
            }

            var imports = await FindEvaluatedImportsAsync(request.ProjectPath, linkedCancellation.Token)
                .ConfigureAwait(false);
            var evaluatedExtensions = targets
                .Where(static target => target.Succeeded)
                .SelectMany(static target => target.Items.GetValueOrDefault("Analyzer", []))
                .Select(static item => item.Identity)
                .Concat(declared.AnalyzerPaths)
                .Select(path => NormalizeEvaluatedPath(request.ProjectPath, path))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            var generatorPaths = declared.GeneratorPaths
                .Select(path => NormalizeEvaluatedPath(request.ProjectPath, path))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();

            return new ProjectEvaluationResult(
                request.ProjectId,
                declared.Sdk,
                targetFrameworks,
                imports,
                evaluatedExtensions,
                generatorPaths,
                targets.ToImmutable(),
                diagnostics.Order().ToImmutableArray(),
                TimedOut: false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(
                request,
                declared,
                "C2M-EVAL-003",
                "MSBuild project evaluation timed out.",
                request.ServiceTimeout.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
                timedOut: true);
        }
    }

    private async Task<EvaluationProcessResult> QueryAsync(
        string projectPath,
        string? targetFramework,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(projectPath);
        startInfo.ArgumentList.Add($"-getProperty:{string.Join(',', EvaluatedPropertyNames)}");
        startInfo.ArgumentList.Add($"-getItem:{string.Join(',', EvaluatedItemNames)}");
        if (targetFramework is not null)
        {
            startInfo.ArgumentList.Add($"-property:TargetFramework={targetFramework}");
        }

        return await _processRunner.RunAsync(startInfo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ImmutableArray<string>> FindEvaluatedImportsAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        var expandedProjectPath = Path.Combine(
            Path.GetTempPath(),
            $"csharp2md-{Guid.NewGuid():N}.preprocessed.xml");
        try
        {
            var startInfo = CreateStartInfo(projectPath);
            startInfo.ArgumentList.Add($"-preprocess:{expandedProjectPath}");
            var result = await _processRunner.RunAsync(startInfo, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0 || !File.Exists(expandedProjectPath))
            {
                return [];
            }

            var expandedXml = await File.ReadAllTextAsync(expandedProjectPath, cancellationToken)
                .ConfigureAwait(false);
            return ImportedPathPattern()
                .Matches(expandedXml)
                .Select(static match => match.Groups["path"].Value.Trim())
                .Where(Path.IsPathFullyQualified)
                .Select(Path.GetFullPath)
                .Where(path => !string.Equals(path, Path.GetFullPath(projectPath), StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();
        }
        finally
        {
            File.Delete(expandedProjectPath);
        }
    }

    private static ProcessStartInfo CreateStartInfo(string projectPath)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(projectPath)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-nologo");
        return startInfo;
    }

    private static bool TryParseEvaluation(string json, out EvaluationData data)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var properties = document.RootElement.GetProperty("Properties")
                .EnumerateObject()
                .ToImmutableDictionary(
                    static property => property.Name,
                    static property => property.Value.GetString() ?? string.Empty,
                    StringComparer.Ordinal);
            var items = document.RootElement.TryGetProperty("Items", out var itemRoot)
                ? itemRoot.EnumerateObject().ToImmutableDictionary(
                    static item => item.Name,
                    static item => item.Value.EnumerateArray()
                        .Select(MapItem)
                        .OrderBy(static evaluated => evaluated.Identity, StringComparer.Ordinal)
                        .ToImmutableArray(),
                    StringComparer.Ordinal)
                : ImmutableDictionary<string, ImmutableArray<EvaluatedItem>>.Empty;
            data = new EvaluationData(properties, items);
            return true;
        }
        catch (JsonException)
        {
            data = default!;
            return false;
        }
    }

    private static EvaluatedItem MapItem(JsonElement item)
    {
        var metadata = item.EnumerateObject()
            .Where(static property => property.Name != "Identity")
            .ToImmutableDictionary(
                static property => property.Name,
                static property => property.Value.GetString() ?? string.Empty,
                StringComparer.Ordinal);
        return new EvaluatedItem(item.GetProperty("Identity").GetString() ?? string.Empty, metadata);
    }

    private static ImmutableArray<string> FindTargetFrameworks(ImmutableDictionary<string, string> properties)
    {
        var plural = properties.GetValueOrDefault("TargetFrameworks", string.Empty)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var values = plural.Length > 0
            ? plural
            : [properties.GetValueOrDefault("TargetFramework", string.Empty)];
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static DeclaredProject ReadDeclaredProject(string projectPath)
    {
        var document = XDocument.Load(projectPath, LoadOptions.None);
        var sdk = document.Root?.Attribute("Sdk")?.Value ?? string.Empty;
        var analyzerPaths = document.Descendants("Analyzer")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path!)
            .ToImmutableArray();
        var generatorPaths = document.Descendants("Generator")
            .Select(static element => element.Attribute("Include")?.Value)
            .Concat(document.Descendants("Analyzer")
                .Where(static element => string.Equals(
                    element.Attribute("ExtensionKind")?.Value,
                    "Generator",
                    StringComparison.OrdinalIgnoreCase))
                .Select(static element => element.Attribute("Include")?.Value))
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path!)
            .ToImmutableArray();
        return new DeclaredProject(sdk, analyzerPaths, generatorPaths);
    }

    private static string NormalizeEvaluatedPath(string projectPath, string path) =>
        Path.GetFullPath(path, Path.GetDirectoryName(projectPath)!);

    private static ProjectEvaluationResult Failed(
        TrustedProjectEvaluationRequest request,
        DeclaredProject declared,
        string code,
        string message,
        string detail,
        bool timedOut)
    {
        var diagnostic = Diagnostic(code, request.ProjectId.ToFactId(), message, detail);
        return new ProjectEvaluationResult(
            request.ProjectId,
            declared.Sdk,
            [],
            [],
            declared.AnalyzerPaths
                .Select(path => NormalizeEvaluatedPath(request.ProjectPath, path))
                .Order(StringComparer.Ordinal)
                .ToImmutableArray(),
            declared.GeneratorPaths
                .Select(path => NormalizeEvaluatedPath(request.ProjectPath, path))
                .Order(StringComparer.Ordinal)
                .ToImmutableArray(),
            [],
            [diagnostic],
            timedOut);
    }

    private static AnalysisDiagnostic Diagnostic(
        string code,
        FactId scope,
        string message,
        string detail) =>
        AnalysisDiagnostic.Create(
            code,
            DiagnosticSeverity.Warning,
            DiagnosticStage.Evaluation,
            scope,
            message,
            string.IsNullOrWhiteSpace(detail) ? [] : [new DiagnosticData("detail", Sanitize(detail))]);

    private static string Sanitize(string value) =>
        string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    [GeneratedRegex("""(?im)^\s*(?:<!--\s*)?(?<path>[A-Z]:\\[^\r\n<>"]+\.(?:props|targets))\s*(?:-->)?\s*$""", RegexOptions.CultureInvariant)]
    private static partial Regex ImportedPathPattern();

    private sealed record EvaluationData(
        ImmutableDictionary<string, string> Properties,
        ImmutableDictionary<string, ImmutableArray<EvaluatedItem>> Items);

    private sealed record DeclaredProject(
        string Sdk,
        ImmutableArray<string> AnalyzerPaths,
        ImmutableArray<string> GeneratorPaths);
}
