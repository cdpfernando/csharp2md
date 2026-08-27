using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

public sealed class PackagePublisherViewTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    public void ToPublicationOrder_PayloadKeysMatchPublishedPackageViewSlots()
    {
        var document = DomainMapper.ToWire(StructuralSnapshot(), Context);
        var view = PublishedPackageView.From(document);
        var artifacts = PackagePublisher.ToPublicationOrder(document);
        var payloadKeys = artifacts
            .Where(fragment => fragment.Role == ArtifactRole.Payload)
            .Select(fragment => fragment.CanonicalKey)
            .ToArray();

        Assert.Equal(view.Slots.Select(slot => slot.CanonicalKey), payloadKeys);
        Assert.Equal(ArtifactRole.Manifest, artifacts[^1].Role);
        Assert.Equal(PackagePublisher.ManifestKey, artifacts[^1].CanonicalKey);
    }

    [Fact]
    public void ToPublicationOrder_PublishedManifest_ListsNoCountZeroForOmittedShard()
    {
        var artifacts = PackagePublisher.ToPublicationOrder(
            DomainMapper.ToWire(StructuralSnapshot(), Context));
        var manifest = CanonicalJson.Read<ManifestEnvelope>(artifacts[^1].Payload.AsSpan());
        var payloadKeys = artifacts
            .Where(fragment => fragment.Role == ArtifactRole.Payload)
            .Select(fragment => fragment.CanonicalKey)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(manifest.Artifacts, entry => entry.CanonicalKey == "facts/structural" && entry.Count == 2);
        Assert.DoesNotContain(
            manifest.Artifacts,
            entry => entry.Count == 0 && IsFamilyStem(entry.CanonicalKey));
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey == "facts/architecture");
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey == "facts/contract");
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey == "facts/persistence");
        Assert.DoesNotContain(manifest.Artifacts, entry => entry.CanonicalKey == "facts/configuration");
        Assert.All(manifest.Artifacts, entry => Assert.Contains(entry.Path, payloadKeys));
    }

    [Fact]
    public void ToWire_DoesNotProduceThePublishedManifest()
    {
        var snapshot = StructuralSnapshot();
        var document = DomainMapper.ToWire(snapshot, Context);
        var artifacts = PackagePublisher.ToPublicationOrder(document);
        var published = CanonicalJson.Read<ManifestEnvelope>(artifacts[^1].Payload.AsSpan());

        Assert.True(document.Manifest.Artifacts.IsDefaultOrEmpty);
        Assert.Contains(published.Artifacts, entry => entry.CanonicalKey == "facts/structural");
        Assert.Equal(Context.SolutionKey, published.SolutionKey);
    }

    private static bool IsFamilyStem(string canonicalKey) =>
        canonicalKey.StartsWith("facts/", StringComparison.Ordinal)
        || canonicalKey.StartsWith("observations/", StringComparison.Ordinal)
        || canonicalKey.StartsWith("relations/", StringComparison.Ordinal);

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
