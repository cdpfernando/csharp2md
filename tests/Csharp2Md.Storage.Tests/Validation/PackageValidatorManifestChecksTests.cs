using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Tests.Filesystem;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Validation;

/// <summary>GCPC-061/GCPC-062/GCPC-065 (partial)/GCPC-071 (partial): manifest cardinality and provenance
/// compatibility checks, usable identically at publication and at re-validation (AD-025).</summary>
[Collection(FilesystemStoreCollection.Name)]
public sealed class PackageValidatorManifestChecksTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    public void ValidatePublishedManifest_CountDisagreesWithArtifact_RejectsNamingArtifactAndBothValues()
    {
        var bytes = TwoElementArrayBytes();
        var manifest = ManifestWith(new ManifestEntry("relations/candidates", "payload", 5, bytes.Length, "relations/candidates.json"));
        var artifactsByKey = new Dictionary<string, ImmutableArray<byte>>(StringComparer.Ordinal)
        {
            ["relations/candidates.json"] = bytes,
        };

        var exception = Assert.Throws<PublicationRejectedException>(
            () => PackageValidator.ValidatePublishedManifest(manifest, artifactsByKey));

        Assert.Equal("manifest-count-mismatch", exception.Gate);
        Assert.Contains("relations/candidates.json", exception.Detail, StringComparison.Ordinal);
        Assert.Contains("5", exception.Detail, StringComparison.Ordinal);
        Assert.Contains("2", exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatePublishedManifest_ByteSizeDisagreesWithArtifact_Rejects()
    {
        var bytes = TwoElementArrayBytes();
        var manifest = ManifestWith(new ManifestEntry("relations/candidates", "payload", 2, bytes.Length + 100, "relations/candidates.json"));
        var artifactsByKey = new Dictionary<string, ImmutableArray<byte>>(StringComparer.Ordinal)
        {
            ["relations/candidates.json"] = bytes,
        };

        var exception = Assert.Throws<PublicationRejectedException>(
            () => PackageValidator.ValidatePublishedManifest(manifest, artifactsByKey));

        Assert.Equal("manifest-size-mismatch", exception.Gate);
        Assert.Contains("relations/candidates.json", exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatePublishedManifest_FileDeclaredButAbsent_Rejects()
    {
        var manifest = ManifestWith(new ManifestEntry("relations/candidates", "payload", 0, 2, "relations/candidates.json"));
        var artifactsByKey = new Dictionary<string, ImmutableArray<byte>>(StringComparer.Ordinal);

        var exception = Assert.Throws<PublicationRejectedException>(
            () => PackageValidator.ValidatePublishedManifest(manifest, artifactsByKey));

        Assert.Equal("manifest-file-missing", exception.Gate);
        Assert.Equal("relations/candidates.json", exception.Detail);
    }

    [Fact]
    public void ValidatePublishedManifest_FilePresentButUndeclared_Rejects()
    {
        var manifest = ManifestWith();
        var artifactsByKey = new Dictionary<string, ImmutableArray<byte>>(StringComparer.Ordinal)
        {
            ["relations/candidates.json"] = TwoElementArrayBytes(),
        };

        var exception = Assert.Throws<PublicationRejectedException>(
            () => PackageValidator.ValidatePublishedManifest(manifest, artifactsByKey));

        Assert.Equal("undeclared-file", exception.Gate);
        Assert.Equal("relations/candidates.json", exception.Detail);
    }

    [Fact]
    public void ValidatePublishedManifest_DeferredArtifact_IsSkippedRatherThanCompared()
    {
        var manifest = ManifestWith(new ManifestEntry("source/acme/invoice", "payload", 0, 0, "source/acme/Invoice.cs"));
        var artifactsByKey = new Dictionary<string, ImmutableArray<byte>>(StringComparer.Ordinal);
        var deferredKeys = new HashSet<string>(StringComparer.Ordinal) { "source/acme/Invoice.cs" };

        PackageValidator.ValidatePublishedManifest(manifest, artifactsByKey, deferredKeys);
    }

    [Fact]
    public void EnsureProvenanceCompatible_NewerGeneratorVersion_RejectsWithItsOwnReasonCode()
    {
        var running = ProvenanceDto.Current();
        var newer = running with { GeneratorVersion = IncrementMajor(running.GeneratorVersion) };

        var exception = Assert.Throws<PublicationRejectedException>(
            () => PackageValidator.EnsureProvenanceCompatible(newer));

        Assert.Equal("incompatible-provenance", exception.Gate);
    }

    [Fact]
    public void EnsureProvenanceCompatible_SameOrOlderGeneratorVersion_DoesNotThrow()
    {
        var running = ProvenanceDto.Current();

        PackageValidator.EnsureProvenanceCompatible(running);
    }

    [Fact]
    public void ValidatePackageDirectory_ManifestCountCorruptedAfterCommit_RejectsAndTheValidatorWritesNothing()
    {
        using var output = TempOutputRoot.Create();
        var package = Commit(output.DirectoryPath);
        var before = FilesystemTestPaths.SnapshotFiles(package);

        var manifestPath = Path.Combine(package, "manifest.json");
        var manifest = CanonicalJson.Read<ManifestEnvelope>(File.ReadAllBytes(manifestPath));
        var target = Assert.Single(manifest.Artifacts, entry => entry.Path == "facts/structural.json");
        var corrupted = manifest with
        {
            Artifacts = [.. manifest.Artifacts.Select(entry => entry == target ? entry with { Count = entry.Count + 5 } : entry)],
        };
        File.WriteAllBytes(manifestPath, [.. CanonicalJson.Write(corrupted)]);

        var exception = Assert.Throws<PublicationRejectedException>(() => PackageValidator.ValidatePackageDirectory(package));
        Assert.Equal("manifest-count-mismatch", exception.Gate);
        Assert.Contains("facts/structural.json", exception.Detail, StringComparison.Ordinal);

        // The validator itself never writes: every file this test did not deliberately corrupt is
        // unchanged, proving a failed validation attempt leaves the package it read exactly as found.
        var after = FilesystemTestPaths.SnapshotFiles(package);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var key in before.Keys.Where(static key => key != "manifest.json"))
        {
            Assert.True(before[key].AsSpan().SequenceEqual(after[key]), $"'{key}' changed after a failed validation attempt.");
        }
    }

    private static ManifestEnvelope ManifestWith(params ManifestEntry[] entries) =>
        new(1, 1, 1, "s-test", "Acme.sln", [.. entries], ProvenanceDto.Current());

    private static ImmutableArray<byte> TwoElementArrayBytes() =>
        CanonicalJson.Write(ImmutableArray.Create("alpha", "beta"));

    private static string IncrementMajor(string version)
    {
        var parsed = Version.Parse(version);
        return new Version(parsed.Major + 1, parsed.Minor, parsed.Build < 0 ? 0 : parsed.Build, parsed.Revision < 0 ? 0 : parsed.Revision).ToString();
    }

    private static string Commit(string outputRoot)
    {
        var store = new FilesystemTransactionalStore(outputRoot);
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(new FactualSnapshot([Solution.Create(SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln"))], [], [], [], [], []));
        session.Commit();
        return FilesystemTestPaths.ChildDirectory(outputRoot, SolutionKey);
    }
}
