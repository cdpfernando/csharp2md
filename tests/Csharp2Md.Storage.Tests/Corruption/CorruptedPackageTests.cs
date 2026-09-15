using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Corruption;

/// <summary>
/// One deliberately corrupted package per defect class `validate` must detect (GCPC-065, GCPC-066), so
/// detection strength is proven by inputs -- not by a second implementation of the check being validated.
/// Each test mutates <see cref="CorruptedPackageFactory"/>'s clean baseline in exactly one way and re-runs
/// the same validators T48's `validate` command runs: <see cref="PackageValidator.ValidatePackageDirectory"/>,
/// <see cref="FactualPackageReader"/> and <see cref="ProjectionValidator"/>.
/// </summary>
public sealed class CorruptedPackageTests : IDisposable
{
    private readonly string _root;
    private readonly string _cleanPackageDirectory;

    public CorruptedPackageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "csharp2md-corruption-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _cleanPackageDirectory = CorruptedPackageFactory.BuildCleanPackage(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-065")]
    [Trait("Requirement", "GCPC-066")]
    public void BadHash_IsRejectedNamingTheFactAndDiffersOnlyInStructuralFacts()
    {
        const string relativeKey = "facts/structural.json";
        var mutant = Mutate(relativeKey, "bad-hash", path =>
        {
            var shard = CanonicalJson.Read<StructuralFactsShard>(File.ReadAllBytes(path));
            var solution = shard.Solutions[0];
            var badHash = new string(solution.ContentSha256[0] == '0' ? '1' : '0', 64);
            var mutated = shard with { Solutions = [solution with { ContentSha256 = badHash }] };
            File.WriteAllBytes(path, CanonicalJson.Write(mutated).ToArray());
            return solution.Identity.Id;
        });

        var exception = RunFullValidate(mutant.PackageDirectory);

        Assert.Equal("content-hash", exception.Gate);
        Assert.Contains(mutant.OffendingValue, exception.Detail, StringComparison.Ordinal);
        AssertDiffersInExactlyOneFile(mutant.PackageDirectory, relativeKey);
    }

    [Fact]
    [Trait("Requirement", "GCPC-065")]
    [Trait("Requirement", "GCPC-066")]
    public void WrongCount_IsRejectedNamingTheArtifactAndDiffersOnlyInTheManifest()
    {
        var mutant = Mutate(CorruptedPackageFactory.ManifestKey, "wrong-count", path =>
        {
            var manifest = CanonicalJson.Read<ManifestEnvelope>(File.ReadAllBytes(path));
            var index = manifest.Artifacts.IndexOf(manifest.Artifacts.Single(
                static entry => entry.CanonicalKey == "facts/structural"));
            var entry = manifest.Artifacts[index];
            var mutated = manifest with { Artifacts = manifest.Artifacts.SetItem(index, entry with { Count = entry.Count + 1 }) };
            File.WriteAllBytes(path, CanonicalJson.Write(mutated).ToArray());
            return entry.Path;
        });

        var exception = RunFullValidate(mutant.PackageDirectory);

        Assert.Equal("manifest-count-mismatch", exception.Gate);
        Assert.Contains(mutant.OffendingValue, exception.Detail, StringComparison.Ordinal);
        AssertDiffersInExactlyOneFile(mutant.PackageDirectory, CorruptedPackageFactory.ManifestKey);
    }

    [Fact]
    [Trait("Requirement", "GCPC-065")]
    [Trait("Requirement", "GCPC-066")]
    public void DanglingReference_IsRejectedNamingTheMissingKeyAndDiffersOnlyInTheCatalog()
    {
        string? missingKey = null;
        var mutant = Mutate(CorruptedPackageFactory.CatalogKey, "dangling-reference", path =>
        {
            var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var original = node["artifact_key"]!.GetValue<string>();
            missingKey = SameLength("facts/does-not-exist.json", original.Length);
            node["artifact_key"] = missingKey;
            File.WriteAllText(path, node.ToJsonString());
            return missingKey;
        });

        var exception = RunFullValidate(mutant.PackageDirectory);

        Assert.Equal("projection-key", exception.Gate);
        Assert.Equal(missingKey, exception.Detail);
        AssertDiffersInExactlyOneFile(mutant.PackageDirectory, CorruptedPackageFactory.CatalogKey);
    }

    [Fact]
    [Trait("Requirement", "GCPC-065")]
    [Trait("Requirement", "GCPC-066")]
    public void OutOfRangeOrdinal_IsRejectedNamingKeyAndOrdinalAndDiffersOnlyInTheCatalog()
    {
        var mutant = Mutate(CorruptedPackageFactory.CatalogKey, "out-of-range-ordinal", path =>
        {
            var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            node["ordinal"] = 5;
            File.WriteAllText(path, node.ToJsonString());
            return CorruptedPackageFactory.SourceKey + " 5";
        });

        var exception = RunFullValidate(mutant.PackageDirectory);

        Assert.Equal("projection-ordinal", exception.Gate);
        Assert.Contains(CorruptedPackageFactory.SourceKey, exception.Detail, StringComparison.Ordinal);
        Assert.Contains("5", exception.Detail, StringComparison.Ordinal);
        AssertDiffersInExactlyOneFile(mutant.PackageDirectory, CorruptedPackageFactory.CatalogKey);
    }

    [Fact]
    [Trait("Requirement", "GCPC-065")]
    [Trait("Requirement", "GCPC-066")]
    public void OutOfBoundsLocator_IsRejectedNamingTheLocatorAndDiffersOnlyInTheCatalog()
    {
        var mutant = Mutate(CorruptedPackageFactory.CatalogKey, "out-of-bounds-locator", path =>
        {
            var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            // "0" is column-out-of-bounds (PositionIsInside requires column >= 1) and the same one-digit
            // byte length as the clean baseline's "5", so only this one value's meaning changes -- the
            // catalog's declared byte size still matches the manifest, and the projection-span check (not
            // the coarser manifest-size-mismatch check that ValidatePackageDirectory runs first) is what
            // actually catches it.
            node["span"]!["end_column"] = 0;
            File.WriteAllText(path, node.ToJsonString());
            return node["locator"]!.GetValue<string>();
        });

        var exception = RunFullValidate(mutant.PackageDirectory);

        Assert.Equal("projection-span", exception.Gate);
        Assert.Equal(mutant.OffendingValue, exception.Detail);
        AssertDiffersInExactlyOneFile(mutant.PackageDirectory, CorruptedPackageFactory.CatalogKey);
    }

    [Fact]
    [Trait("Requirement", "GCPC-065")]
    [Trait("Requirement", "GCPC-066")]
    public void BrokenLink_IsRejectedNamingTheMissingPageAndDiffersOnlyInTheLinkingPage()
    {
        string? missingPage = null;
        var mutant = Mutate(CorruptedPackageFactory.EntryPointsPageKey, "broken-link", path =>
        {
            var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var original = node["artifact_key"]!.GetValue<string>();
            missingPage = SameLength("pages/does-not-exist.md", original.Length);
            node["artifact_key"] = missingPage;
            File.WriteAllText(path, node.ToJsonString());
            return missingPage;
        });

        var exception = RunFullValidate(mutant.PackageDirectory);

        Assert.Equal("projection-key", exception.Gate);
        Assert.Equal(missingPage, exception.Detail);
        AssertDiffersInExactlyOneFile(mutant.PackageDirectory, CorruptedPackageFactory.EntryPointsPageKey);
    }

    [Fact]
    [Trait("Requirement", "GCPC-065")]
    [Trait("Requirement", "GCPC-066")]
    [Trait("Requirement", "GCPC-071")]
    public void MismatchedProvenance_IsRejectedNamingTheNewerVersionAndDiffersOnlyInTheManifest()
    {
        const string futureVersion = "999.0.0.0";
        var mutant = Mutate(CorruptedPackageFactory.ManifestKey, "mismatched-provenance", path =>
        {
            var manifest = CanonicalJson.Read<ManifestEnvelope>(File.ReadAllBytes(path));
            Assert.NotNull(manifest.Provenance);
            var mutated = manifest with { Provenance = manifest.Provenance! with { GeneratorVersion = futureVersion } };
            File.WriteAllBytes(path, CanonicalJson.Write(mutated).ToArray());
            return futureVersion;
        });

        var exception = RunFullValidate(mutant.PackageDirectory);

        Assert.Equal("incompatible-provenance", exception.Gate);
        Assert.Contains(futureVersion, exception.Detail, StringComparison.Ordinal);
        AssertDiffersInExactlyOneFile(mutant.PackageDirectory, CorruptedPackageFactory.ManifestKey);
    }

    [Fact]
    [Trait("Requirement", "GCPC-067")]
    public void CleanPackage_PassesEveryValidator()
    {
        // The negative control: proves the seven corruptions above are each caught for the mutation, not
        // because the baseline itself was already invalid.
        var exception = Record.Exception(() => RunValidate(_cleanPackageDirectory));
        Assert.Null(exception);
    }

    private readonly record struct Mutant(string PackageDirectory, string OffendingValue);

    private Mutant Mutate(string relativeKey, string name, Func<string, string> mutate)
    {
        var mutantDirectory = CorruptedPackageFactory.CopyForMutation(_cleanPackageDirectory, _root, name);
        var target = Path.Combine(mutantDirectory, relativeKey.Replace('/', Path.DirectorySeparatorChar));
        var offendingValue = mutate(target);
        return new Mutant(mutantDirectory, offendingValue);
    }

    /// <summary>Same length as <paramref name="length"/>, truncated or padded, so a value substitution
    /// never changes a projection artifact's byte size -- isolating the specific defect the corruption
    /// targets from the coarser manifest byte-size check <see cref="RunValidate"/> runs first.</summary>
    private static string SameLength(string preferred, int length) =>
        preferred.Length >= length ? preferred[..length] : preferred.PadRight(length, '_');

    private static PublicationRejectedException RunFullValidate(string packageDirectory) =>
        Assert.Throws<PublicationRejectedException>(() => RunValidate(packageDirectory));

    /// <summary>The same validators, in the same order, T48's `validate` command runs (AD-025).</summary>
    private static void RunValidate(string packageDirectory)
    {
        PackageValidator.ValidatePackageDirectory(packageDirectory);
        var result = FactualPackageReader.Read(packageDirectory);
        var manifest = CanonicalJson.Read<ManifestEnvelope>(
            File.ReadAllBytes(Path.Combine(packageDirectory, CorruptedPackageFactory.ManifestKey)));
        var context = new ManifestContext(manifest.SolutionKey, manifest.SolutionFileName);
        var document = DomainMapper.ToWire(result.Snapshot, context);
        var view = PublishedPackageView.From(document);
        ProjectionValidator.Validate(view, result.Projections);
    }

    private void AssertDiffersInExactlyOneFile(string mutantDirectory, string expectedRelativeKey)
    {
        var clean = CorruptedPackageFactory.ReadAllFiles(_cleanPackageDirectory);
        var mutant = CorruptedPackageFactory.ReadAllFiles(mutantDirectory);

        Assert.Equal(clean.Keys.OrderBy(static key => key, StringComparer.Ordinal), mutant.Keys.OrderBy(static key => key, StringComparer.Ordinal));

        var differing = clean.Keys
            .Where(key => !clean[key].AsSpan().SequenceEqual(mutant[key]))
            .ToArray();

        Assert.Equal(new[] { expectedRelativeKey }, differing);
    }
}
