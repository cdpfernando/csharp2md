using Csharp2Md.Core.Analysis.Inventory;
using Csharp2Md.Core.PackageBuilding;

namespace Csharp2Md.Core.Publication;

internal sealed class PackageReader : IDisposable
{
    private readonly string _packageDirectory;
    private readonly FileStream _manifestLock;
    private bool _disposed;

    private PackageReader(string packageDirectory, FileStream manifestLock, PackageManifest manifest)
    {
        _packageDirectory = packageDirectory;
        _manifestLock = manifestLock;
        Manifest = manifest;
    }

    internal PackageManifest Manifest { get; }

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
            var manifest = CanonicalJson.Read<PackageManifest>(bytes.AsSpan());
            return new PackageReader(root, manifestLock, manifest);
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
        var paths = Manifest.Indexes.Select(index => index.Path)
            .Append("manifest.json")
            .Concat(Manifest.Roots.Select(root => root.MarkdownPath))
            .Concat(Manifest.Journeys.Select(journey => journey.EntryPath))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal);
        return paths.ToDictionary(path => path, ReadArtifact, StringComparer.Ordinal);
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
        var fullPath = Path.GetFullPath(Path.Combine(_packageDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The declared package artifact is missing.", fullPath);
        }
        PathGuard.RejectEscapes(_packageDirectory, fullPath);
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
