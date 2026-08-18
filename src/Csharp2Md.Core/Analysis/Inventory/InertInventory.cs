using System.Xml;
using System.Xml.Linq;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Discovery;
using Csharp2Md.Core.Manifests;
using Csharp2Md.Core.Output;

namespace Csharp2Md.Core.Analysis.Inventory;

internal sealed class InertInventory(IInventoryExecutionObserver? observer = null)
{
    private static readonly string[] ConfigurationPatterns =
        ["appsettings*.json", "docker-compose*.yml", "docker-compose*.yaml"];

    // Kept as an injected test seam for later semantic cuts. Syntax inventory deliberately never
    // invokes it: project discovery and XML/source reads are inert filesystem operations.
    private readonly IInventoryExecutionObserver? _observer = observer;

    public InventoryResult Inventory(AnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = _observer;

        var diagnostics = new List<InventoryDiagnostic>();
        if (!TryLoadManifest(request.Input, diagnostics, out var manifest, out var inputRoot))
        {
            return new InventoryResult([], CanonicalDiagnostics(diagnostics));
        }

        var discovery = ServiceDiscoverer.Discover(NormalizeManifest(manifest!, inputRoot));
        diagnostics.AddRange(discovery.Warnings.Select(static warning =>
            new InventoryDiagnostic("inventory.discovery", InventoryDiagnosticSeverity.Warning, warning)));

        var services = discovery.Catalog.Services
            .Select(service => InventoryService(inputRoot, service, diagnostics))
            .OrderBy(static service => service.RootPath, StringComparer.Ordinal)
            .ThenBy(static service => service.Name, StringComparer.Ordinal)
            .ToImmutableArray();

        return new InventoryResult(services, CanonicalDiagnostics(diagnostics));
    }

    private static bool TryLoadManifest(
        string input,
        List<InventoryDiagnostic> diagnostics,
        out Manifest? manifest,
        out string inputRoot)
    {
        var fullInput = Path.GetFullPath(input);
        if (Directory.Exists(fullInput))
        {
            inputRoot = fullInput;
            manifest = CreateDirectoryManifest(fullInput);
            return true;
        }

        inputRoot = Path.GetDirectoryName(fullInput) ?? fullInput;
        var load = ManifestLoader.Load(fullInput);
        if (load.IsSuccess)
        {
            manifest = load.Manifest;
            return true;
        }

        manifest = null;
        diagnostics.Add(new InventoryDiagnostic(
            "inventory.input",
            InventoryDiagnosticSeverity.Error,
            load.Error?.Message ?? $"Analysis input does not exist: {fullInput}"));
        return false;
    }

    private static Manifest CreateDirectoryManifest(string inputRoot)
    {
        var hasTopLevelBoundary = Directory.EnumerateFiles(inputRoot, "*.sln", SearchOption.TopDirectoryOnly).Any()
            || Directory.EnumerateFiles(inputRoot, "*.slnx", SearchOption.TopDirectoryOnly).Any()
            || Directory.EnumerateFiles(inputRoot, "*.csproj", SearchOption.TopDirectoryOnly).Any();
        if (hasTopLevelBoundary)
        {
            return new Manifest([new ManifestEntry(inputRoot)]);
        }

        var projectDirectories = Directory.EnumerateFiles(inputRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !OutputWriter.IsExcluded(Path.GetRelativePath(inputRoot, path)))
            .Select(static path => Path.GetDirectoryName(path)!)
            .Distinct(PathComparer)
            .Order(StringComparer.Ordinal)
            .Select(static directory => new ManifestEntry(directory))
            .ToArray();
        return new Manifest(projectDirectories.Length == 0 ? [new ManifestEntry(inputRoot)] : projectDirectories);
    }

    private static Manifest NormalizeManifest(Manifest manifest, string manifestRoot) =>
        new(manifest.Services.Select(entry =>
        {
            var root = ResolvePath(manifestRoot, entry.Path);
            var projects = entry.Projects?.Select(project => ResolvePath(root, project)).ToArray();
            return entry with { Path = root, Projects = projects };
        }).ToArray());

    private static InventoryService InventoryService(
        string inputRoot,
        ServiceDescriptor service,
        List<InventoryDiagnostic> diagnostics)
    {
        var projects = service.ProjectPaths
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Select(path => InventoryProject(inputRoot, path, diagnostics))
            .ToImmutableArray();

        return new InventoryService(
            service.Name.Value,
            RelativePath(inputRoot, service.RootPath),
            service.SolutionPath is null ? null : RelativePath(inputRoot, service.SolutionPath),
            projects);
    }

    private static InventoryProject InventoryProject(
        string inputRoot,
        string projectPath,
        List<InventoryDiagnostic> diagnostics)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? inputRoot;
        var sources = Directory.Exists(projectDirectory)
            ? Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !OutputWriter.IsExcluded(Path.GetRelativePath(projectDirectory, path)))
                .Select(path => RelativePath(inputRoot, path))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray()
            : [];

        var configuration = Directory.Exists(projectDirectory)
            ? ConfigurationPatterns
                .SelectMany(pattern => Directory.EnumerateFiles(projectDirectory, pattern, SearchOption.AllDirectories))
                .Where(path => !OutputWriter.IsExcluded(Path.GetRelativePath(projectDirectory, path)))
                .Select(path => RelativePath(inputRoot, path))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray()
            : [];

        var imports = ImmutableArray<string>.Empty;
        var analyzers = ImmutableArray<string>.Empty;
        var generators = ImmutableArray<string>.Empty;

        if (!File.Exists(projectPath))
        {
            diagnostics.Add(new InventoryDiagnostic(
                "inventory.project-missing",
                InventoryDiagnosticSeverity.Warning,
                $"Project file not found: {RelativePath(inputRoot, projectPath)}"));
        }
        else
        {
            try
            {
                var document = XDocument.Load(projectPath, LoadOptions.None);
                imports = ReadPaths(document.Descendants("Import").Attributes("Project"), projectDirectory, inputRoot);
                analyzers = ReadPaths(document.Descendants("Analyzer").Attributes("Include"), projectDirectory, inputRoot);
                generators = ReadPaths(
                    document.Descendants("Generator").Attributes("Include")
                        .Concat(document.Descendants("ProjectReference")
                            .Where(IsAnalyzerProjectReference)
                            .Attributes("Include")),
                    projectDirectory,
                    inputRoot);
            }
            catch (Exception exception) when (exception is XmlException or IOException)
            {
                diagnostics.Add(new InventoryDiagnostic(
                    "inventory.project-xml",
                    InventoryDiagnosticSeverity.Warning,
                    $"Could not parse {RelativePath(inputRoot, projectPath)}: {exception.Message}"));
            }
        }

        return new InventoryProject(
            Path.GetFileNameWithoutExtension(projectPath),
            RelativePath(inputRoot, projectPath),
            sources,
            configuration,
            imports,
            analyzers,
            generators);
    }

    private static bool IsAnalyzerProjectReference(XElement element) =>
        string.Equals(element.Element("OutputItemType")?.Value.Trim(), "Analyzer", StringComparison.OrdinalIgnoreCase)
        || string.Equals(element.Attribute("OutputItemType")?.Value.Trim(), "Analyzer", StringComparison.OrdinalIgnoreCase);

    private static ImmutableArray<string> ReadPaths(
        IEnumerable<XAttribute> attributes,
        string projectDirectory,
        string inputRoot) =>
        attributes
            .Select(static attribute => attribute.Value.Trim())
            .Where(static path => path.Length > 0)
            .Select(path => ContainsMsBuildExpression(path)
                ? Normalize(path)
                : RelativePath(inputRoot, ResolvePath(projectDirectory, path)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

    private static bool ContainsMsBuildExpression(string path) =>
        path.Contains("$(", StringComparison.Ordinal) || path.Contains("@(", StringComparison.Ordinal);

    private static string ResolvePath(string root, string path) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));

    private static string RelativePath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, Path.GetFullPath(path));
        return Normalize(relative);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static ImmutableArray<InventoryDiagnostic> CanonicalDiagnostics(
        IEnumerable<InventoryDiagnostic> diagnostics) =>
        diagnostics
            .Distinct()
            .OrderBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToImmutableArray();

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
