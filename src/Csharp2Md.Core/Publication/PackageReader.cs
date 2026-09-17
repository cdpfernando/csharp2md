using Csharp2Md.Core.Analysis.Inventory;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;

namespace Csharp2Md.Core.Publication;

internal sealed class PackageReader : IDisposable
{
    private readonly string _packageDirectory;
    private readonly string _artifactDirectory;
    private readonly FileStream _manifestLock;
    private bool _disposed;

    private PackageReader(string packageDirectory, string artifactDirectory, FileStream manifestLock, PackageManifest manifest)
    {
        _packageDirectory = packageDirectory;
        _artifactDirectory = artifactDirectory;
        _manifestLock = manifestLock;
        Manifest = manifest;
    }

    internal PackageManifest Manifest { get; }

    internal string PackageDirectory => _packageDirectory;

    internal static PackageReader Open(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        var root = PathGuard.Normalize(packageDirectory);
        var manifestPath = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("The package root manifest is missing.", manifestPath);
        }
        PathGuard.RejectEscapes(root, manifestPath);
        var manifestLock = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var bytes = ReadAll(manifestLock);
            var artifactDirectory = root;
            PackageManifest manifest;
            if (TryReadPointer(bytes, out var pointer))
            {
                artifactDirectory = Path.Combine(root, "generations", pointer.Generation);
                PathGuard.RejectEscapes(root, artifactDirectory);
                manifest = CanonicalJson.Read<PackageManifest>(File.ReadAllBytes(Path.Combine(artifactDirectory, "manifest.json")));
            }
            else
            {
                manifest = CanonicalJson.Read<PackageManifest>(bytes.AsSpan());
            }
            return new PackageReader(root, artifactDirectory, manifestLock, manifest);
        }
        catch
        {
            manifestLock.Dispose();
            throw;
        }
    }

    internal ImmutableArray<byte> ReadArtifact(string relativePath)
    {
        ThrowIfDisposed();
        var path = Resolve(relativePath);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadAll(stream);
    }

    internal IReadOnlyDictionary<string, ImmutableArray<byte>> ReadDeclaredArtifacts()
    {
        ThrowIfDisposed();
        var paths = Manifest.Solutions
            .SelectMany(solution => solution.Indexes.Select(index => index.EntryPath)
                .Concat(solution.Journeys.Select(journey => solution.Indexes.Single(index => index.Kind == journey.EntryIndex).EntryPath)))
            .Append("manifest.json")
            .Append("markdown/index.md")
            .Append("certification.json")
            .Append("measurements.json")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal);
        var artifacts = paths.ToDictionary(path => path, ReadArtifact, StringComparer.Ordinal);
        var tablePaths = Manifest.Solutions
            .SelectMany(solution => new[] { "entities", "variants", "cycles" }
                .Select(name => $"solutions/{solution.Id.Value}/tables/{name}.000000.json"));
        foreach (var tablePath in tablePaths)
        {
            artifacts.TryAdd(tablePath, ReadArtifact(tablePath));
        }

        var rootsIndexes = Manifest.Solutions
            .Select(solution => CanonicalJson.Read<RootsIndexData>(artifacts[solution.Roots.EntryPath].AsSpan()))
            .ToArray();
        var markdownPaths = rootsIndexes
            .SelectMany(index => index.Roots.Select(root => index.MarkdownPath(root.Handle)))
            .Distinct(StringComparer.Ordinal);
        foreach (var markdownPath in markdownPaths)
        {
            artifacts.TryAdd(markdownPath, ReadArtifact(markdownPath));
        }

        foreach (var rootsIndex in rootsIndexes)
        {
            artifacts.TryAdd(rootsIndex.DocumentsIndexPath, ReadArtifact(rootsIndex.DocumentsIndexPath));
            var documents = CanonicalJson.Read<DocumentsIndexData>(artifacts[rootsIndex.DocumentsIndexPath].AsSpan());
            foreach (var document in documents.Documents)
            {
                var documentPath = documents.MarkdownPath(document.Handle);
                artifacts.TryAdd(documentPath, ReadArtifact(documentPath));
            }
        }

        var pointerPaths = Manifest.Solutions
            .SelectMany(solution => solution.Indexes)
            .Where(index => index.Kind is NavigationIndexKind.Outgoing or NavigationIndexKind.Incoming
                or NavigationIndexKind.Contracts or NavigationIndexKind.Persistence or NavigationIndexKind.Measures)
            .Select(index => CanonicalJson.Read<NavigationIndexData>(artifacts[index.EntryPath].AsSpan()).ArtifactPath)
            .Distinct(StringComparer.Ordinal);
        foreach (var pointerPath in pointerPaths)
        {
            artifacts.TryAdd(pointerPath, ReadArtifact(pointerPath));
        }
        var shardPaths = Manifest.Solutions
            .SelectMany(solution => solution.Indexes)
            .Where(index => index.Kind is NavigationIndexKind.Evidence)
            .SelectMany(index => CanonicalJson.Read<EvidenceIndexData>(artifacts[index.EntryPath].AsSpan()).Shards)
            .Select(shard => shard.ArtifactPath)
            .Distinct(StringComparer.Ordinal);
        foreach (var shardPath in shardPaths)
        {
            artifacts.TryAdd(shardPath, ReadArtifact(shardPath));
        }
        return artifacts;
    }

    private static bool TryReadPointer(ImmutableArray<byte> bytes, out PackageGenerationPointer pointer)
    {
        try
        {
            pointer = CanonicalJson.Read<PackageGenerationPointer>(bytes.AsSpan());
            return true;
        }
        catch (ArgumentException) { pointer = null!; return false; }
        catch (System.Text.Json.JsonException) { pointer = null!; return false; }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _manifestLock.Dispose();
    }

    private string Resolve(string relativePath)
    {
        _ = new RelativeArtifactPath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(_artifactDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The declared package artifact is missing.", fullPath);
        }
        PathGuard.RejectEscapes(_artifactDirectory, fullPath);
        return fullPath;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static ImmutableArray<byte> ReadAll(Stream stream)
    {
        stream.Position = 0;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray().ToImmutableArray();
    }
}
