using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Cli.Tests;

internal static class PublishedManifestTestFile
{
    internal const string PartRole = "manifest-part";

    internal static ManifestEnvelope ReadRoot(string packageDirectory) =>
        CanonicalJson.Read<ManifestEnvelope>(File.ReadAllBytes(Path.Combine(packageDirectory, "manifest.json")));

    internal static ImmutableArray<ManifestEntry> ReadEntries(string packageDirectory)
    {
        var entries = ImmutableArray.CreateBuilder<ManifestEntry>();
        Expand(ReadRoot(packageDirectory).Artifacts);
        return entries.ToImmutable();

        void Expand(ImmutableArray<ManifestEntry> level)
        {
            foreach (var entry in level)
            {
                entries.Add(entry);
                if (entry.Role == PartRole)
                {
                    Expand(CanonicalJson.Read<ImmutableArray<ManifestEntry>>(
                        File.ReadAllBytes(Path.Combine(packageDirectory, entry.Path.Replace('/', Path.DirectorySeparatorChar)))));
                }
            }
        }
    }

    internal static bool HasParts(string packageDirectory) =>
        ReadRoot(packageDirectory).Artifacts.Any(static entry => entry.Role == PartRole);

    internal static void RewriteEntry(
        string packageDirectory,
        Func<ManifestEntry, bool> predicate,
        Func<ManifestEntry, ManifestEntry> rewrite)
    {
        var manifestPath = Path.Combine(packageDirectory, "manifest.json");
        var root = ReadRoot(packageDirectory);
        if (TryRewrite(root.Artifacts, out var rootEntries))
        {
            File.WriteAllBytes(manifestPath, CanonicalJson.Write(root with { Artifacts = rootEntries }).ToArray());
            return;
        }

        foreach (var pointer in ReadEntries(packageDirectory).Where(static entry => entry.Role == PartRole))
        {
            var partPath = Path.Combine(packageDirectory, pointer.Path.Replace('/', Path.DirectorySeparatorChar));
            var partBytes = File.ReadAllBytes(partPath);
            var part = CanonicalJson.Read<ImmutableArray<ManifestEntry>>(partBytes);
            if (!TryRewrite(part, out var rewritten))
            {
                continue;
            }

            var rewrittenBytes = CanonicalJson.Write(rewritten);
            Assert.Equal(partBytes.Length, rewrittenBytes.Length);
            File.WriteAllBytes(partPath, rewrittenBytes.ToArray());
            return;
        }

        throw new InvalidOperationException("The requested manifest entry was not found.");

        bool TryRewrite(ImmutableArray<ManifestEntry> entries, out ImmutableArray<ManifestEntry> rewritten)
        {
            var index = -1;
            for (var candidate = 0; candidate < entries.Length; candidate++)
            {
                if (predicate(entries[candidate]))
                {
                    index = candidate;
                    break;
                }
            }

            if (index < 0)
            {
                rewritten = default;
                return false;
            }

            rewritten = entries.SetItem(index, rewrite(entries[index]));
            return true;
        }
    }
}
