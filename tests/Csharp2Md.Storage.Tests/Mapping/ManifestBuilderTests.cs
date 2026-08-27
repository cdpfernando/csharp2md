using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

public sealed class ManifestBuilderTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    public void From_EveryManifestEntry_NamesAFragmentInTheSamePublication()
    {
        var snapshot = StructuralSnapshot();
        var (payloads, manifest) = Build(snapshot);
        var fragmentKeys = payloads
            .Where(fragment => fragment.Role == ArtifactRole.Payload)
            .Select(fragment => fragment.CanonicalKey)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(manifest.Artifacts);
        Assert.All(manifest.Artifacts, entry => Assert.Contains(entry.Path, fragmentKeys));
    }

    [Fact]
    public void From_DocumentWithEmptyFamilies_DoesNotNameAbsentShards()
    {
        var (payloads, manifest) = Build(StructuralSnapshot());
        var fragmentKeys = payloads
            .Where(fragment => fragment.Role == ArtifactRole.Payload)
            .Select(fragment => fragment.CanonicalKey)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey == "facts/architecture");
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey == "facts/contract");
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey == "facts/persistence");
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey == "facts/configuration");
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey.StartsWith("observations/", StringComparison.Ordinal));
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey.StartsWith("relations/", StringComparison.Ordinal));
        Assert.All(manifest.Artifacts, entry => Assert.Contains(entry.Path, fragmentKeys));
        Assert.DoesNotContain("facts/architecture.json", fragmentKeys);
    }

    [Fact]
    public void From_UsesStemFormKeysAndFragmentFormPaths()
    {
        var (_, manifest) = Build(StructuralSnapshot());
        var structural = Assert.Single(manifest.Artifacts, entry => entry.Path == "facts/structural.json");

        Assert.Equal("facts/structural", structural.CanonicalKey);
        Assert.Equal("payload", structural.Role);
        Assert.Equal(2, structural.Count);
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey.EndsWith(".json", StringComparison.Ordinal));
    }

    [Fact]
    public void From_EmptySnapshot_ListsNoOmittedFamilyWithCountZero()
    {
        var (payloads, manifest) = Build(FactualSnapshot.Empty);

        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey.StartsWith("facts/", StringComparison.Ordinal));
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.Count == 0 && IsOmittedFamilyPath(entry.Path));
        Assert.All(
            manifest.Artifacts,
            entry => Assert.Contains(
                payloads,
                fragment => fragment.CanonicalKey == entry.Path && fragment.Role == ArtifactRole.Payload));
    }

    [Fact]
    public void From_CopiesSolutionIdentityFromContext()
    {
        var (_, manifest) = Build(FactualSnapshot.Empty);

        Assert.Equal(Context.SolutionKey, manifest.SolutionKey);
        Assert.Equal(Context.SolutionFileName, manifest.SolutionFileName);
        Assert.Equal(TaxonomyVersions.Initial.SchemaVersion, manifest.SchemaVersion);
    }

    [Fact]
    public void From_IgnoresTheManifestFragment()
    {
        var (payloads, manifest) = Build(StructuralSnapshot());

        Assert.Contains(payloads, fragment => fragment.Role == ArtifactRole.Manifest);
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.Path == PackagePublisher.ManifestKey);
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey == "manifest");
    }

    private static (ImmutableArray<StagedFragment> Payloads, ManifestEnvelope Manifest) Build(
        FactualSnapshot snapshot)
    {
        var document = DomainMapper.ToWire(snapshot, Context);
        var view = PublishedPackageView.From(document);
        var payloads = PackagePublisher.ToPublicationOrder(document);
        return (payloads, ManifestBuilder.From(Context, payloads, view));
    }

    private static bool IsOmittedFamilyPath(string path) =>
        path.StartsWith("facts/", StringComparison.Ordinal)
        || path.StartsWith("observations/", StringComparison.Ordinal)
        || path.StartsWith("relations/", StringComparison.Ordinal);

    private static FactualSnapshot StructuralSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Payments/Acme.Payments.csproj");
        return new FactualSnapshot(
            [Solution.Create(solutionId), Project.Create(projectId)],
            [],
            [],
            [],
            [],
            []);
    }
}
