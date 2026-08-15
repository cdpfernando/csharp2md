using Csharp2Md.Core.Manifests;

namespace Csharp2Md.Core.Discovery;

public sealed record DiscoveryResult(ServiceCatalog Catalog, IReadOnlyList<string> Warnings);

public static class ServiceDiscoverer
{
    public static DiscoveryResult Discover(Manifest manifest)
    {
        var services = new List<ServiceDescriptor>();
        var warnings = new List<string>();
        var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in manifest.Services)
        {
            var roots = ExpandGlob(entry.Path);
            if (roots.Count == 0)
            {
                warnings.Add($"Manifest pattern '{entry.Path}' matched zero directories."); // P1-17
                continue;
            }

            foreach (var root in roots)
            {
                if (!seenRoots.Add(root))
                {
                    warnings.Add($"Duplicate service root '{root}' — processed once."); // P1-18
                    continue;
                }

                var descriptor = ResolveBoundary(root, entry, warnings);
                if (descriptor is not null)
                {
                    services.Add(descriptor);
                }
            }
        }

        return new DiscoveryResult(new ServiceCatalog(services), warnings);
    }

    private static ServiceDescriptor? ResolveBoundary(string root, ManifestEntry entry, List<string> warnings)
    {
        // P1-04: manifest override wins over the automatic .sln/.csproj heuristic.
        if (entry.Projects is { Count: > 0 })
        {
            return new ServiceDescriptor(
                NameFor(root, entry), root, ServiceBoundaryKind.ManifestOverride, null, entry.Projects, []);
        }

        var solutionFiles = Directory.GetFiles(root, "*.sln", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(root, "*.slnx", SearchOption.TopDirectoryOnly))
            .ToList();

        if (solutionFiles.Count == 1)
        {
            // P1-02: exactly one .sln (or .slnx) file. ProjectPaths is populated here too (by
            // parsing the solution file) so downstream consumers (e.g. ProjectIdentityReader) can
            // read service.ProjectPaths uniformly, regardless of boundary kind.
            var projectPaths = SolutionProjectPaths.Read(solutionFiles[0]);
            return new ServiceDescriptor(
                NameFor(root, entry), root, ServiceBoundaryKind.Solution, solutionFiles[0], projectPaths, []);
        }

        if (solutionFiles.Count > 1)
        {
            // Not covered by P1-02 (which requires "exactly one") or P1-03 (which requires "no
            // .sln file"). Ambiguous — skip rather than guess which solution the service means.
            warnings.Add(
                $"Service root '{root}' has {solutionFiles.Count} solution files — expected exactly one; skipping.");
            return null;
        }

        var projectFiles = Directory.GetFiles(root, "*.csproj", SearchOption.TopDirectoryOnly).ToList();
        if (projectFiles.Count > 0)
        {
            // P1-03: no .sln file, but one or more .csproj files.
            return new ServiceDescriptor(
                NameFor(root, entry), root, ServiceBoundaryKind.LooseProjects, null, projectFiles, []);
        }

        warnings.Add($"Service root '{root}' has no .sln and no .csproj files — skipping.");
        return null;
    }

    private static ServiceName NameFor(string root, ManifestEntry entry) =>
        new(entry.Name ?? Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

    /// <summary>
    /// Expands wildcards in the last path segment only (e.g. "services/*", "fixtures/Acme.*") via
    /// the native <see cref="Directory.GetDirectories(string, string)"/> pattern-matching overload.
    /// Not a general multi-segment glob engine — sufficient for P1-01's stated shape ("root paths
    /// with optional wildcards"), and a pattern with a wildcard in a non-last segment degrades
    /// safely to "matched zero directories" (P1-17) rather than throwing.
    /// </summary>
    private static IReadOnlyList<string> ExpandGlob(string pattern)
    {
        if (pattern.IndexOfAny(['*', '?']) < 0)
        {
            return Directory.Exists(pattern) ? [Path.GetFullPath(pattern)] : [];
        }

        var parent = Path.GetDirectoryName(pattern);
        var lastSegment = Path.GetFileName(pattern);

        if (string.IsNullOrEmpty(parent))
        {
            parent = ".";
        }

        if (!Directory.Exists(parent))
        {
            return [];
        }

        return Directory.GetDirectories(parent, lastSegment)
            .Select(Path.GetFullPath)
            .ToList();
    }
}
