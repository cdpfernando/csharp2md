using Csharp2Md.Storage;

namespace Csharp2Md.Storage.Tests.Schema;

public sealed class EmbeddedRegistryDriftTests
{
    [Fact]
    [Trait("Requirement", "STOR-05")]
    [Trait("Requirement", "STOR-09")]
    public void EmbeddedRegistry_MatchesCommittedFile_ByteForByte()
    {
        var resourceName = DiscoverEmbeddedRegistryName();
        var committedPath = CommittedRegistryPath();
        var embedded = ReadEmbeddedBytes(resourceName);
        var committed = File.ReadAllBytes(committedPath);

        Assert.True(
            committed.AsSpan().SequenceEqual(embedded),
            DescribeMismatch(resourceName, committedPath, committed, embedded));
    }

    [Fact]
    [Trait("Requirement", "STOR-05")]
    public void EmbeddedRegistryDrift_NamesBothPaths()
    {
        var resourceName = DiscoverEmbeddedRegistryName();
        var committedPath = CommittedRegistryPath();
        var committed = File.ReadAllBytes(committedPath);
        var tampered = committed.ToArray();
        tampered[^2] ^= 0x01;

        var description = DescribeMismatch(resourceName, committedPath, committed, tampered);

        Assert.Contains(resourceName, description, StringComparison.Ordinal);
        Assert.Contains(committedPath, description, StringComparison.Ordinal);
        Assert.False(committed.AsSpan().SequenceEqual(tampered));
    }

    private static string DiscoverEmbeddedRegistryName()
    {
        var names = typeof(AssemblyMarker).Assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith("taxonomy-registry.json", StringComparison.Ordinal))
            .ToArray();

        return Assert.Single(names);
    }

    private static string CommittedRegistryPath() =>
        Path.Combine(StorageTestPaths.RepoRoot, "contracts", "taxonomy-registry.json");

    private static byte[] ReadEmbeddedBytes(string resourceName)
    {
        using var stream = typeof(AssemblyMarker).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static string DescribeMismatch(
        string resourceName,
        string committedPath,
        byte[] expected,
        byte[] actual)
    {
        return $"Embedded registry '{resourceName}' differs from '{committedPath}' ({expected.Length} vs {actual.Length} bytes).";
    }
}
