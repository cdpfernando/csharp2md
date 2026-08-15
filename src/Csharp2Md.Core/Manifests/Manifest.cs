namespace Csharp2Md.Core.Manifests;

public sealed record Manifest(IReadOnlyList<ManifestEntry> Services);

public sealed record ManifestEntry(
    string Path,
    string? Name = null,
    IReadOnlyList<string>? Projects = null);
