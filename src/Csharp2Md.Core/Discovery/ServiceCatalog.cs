namespace Csharp2Md.Core.Discovery;

public enum ServiceBoundaryKind
{
    Solution,
    LooseProjects,
    ManifestOverride,
}

public sealed record ServiceDescriptor(
    ServiceName Name,
    string RootPath,
    ServiceBoundaryKind BoundaryKind,
    string? SolutionPath,
    IReadOnlyList<string> ProjectPaths,
    IReadOnlyList<PackageId> PackageIds);

public sealed record ServiceCatalog(IReadOnlyList<ServiceDescriptor> Services);
