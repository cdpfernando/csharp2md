using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Tests.Storage;

public sealed class StagedFragmentTests
{
    [Fact]
    [Trait("Requirement", "ENG-20")]
    public void ArtifactRole_HasExactlyPayloadAndManifest()
    {
        var names = Enum.GetNames<ArtifactRole>();
        var values = Enum.GetValues<ArtifactRole>();

        Assert.Equal(["Payload", "Manifest"], names);
        Assert.Equal([ArtifactRole.Payload, ArtifactRole.Manifest], values);
        Assert.Equal(0, (int)ArtifactRole.Payload);
        Assert.Equal(1, (int)ArtifactRole.Manifest);
    }

    [Fact]
    [Trait("Requirement", "ENG-20")]
    public void StagedFragment_CarriesRoleKeyAndOpaquePayload()
    {
        ImmutableArray<byte> payload = [0x7B, 0x7D];

        var fragment = new StagedFragment(ArtifactRole.Payload, "facts/alpha.json", payload);

        Assert.Equal(ArtifactRole.Payload, fragment.Role);
        Assert.Equal("facts/alpha.json", fragment.CanonicalKey);
        Assert.Equal(payload, fragment.Payload);
    }

    [Fact]
    [Trait("Requirement", "ENG-20")]
    public void CommittedPublication_ExposesArtifactsInPublicationOrder()
    {
        var ordered = ImmutableArray.Create(
            new StagedFragment(ArtifactRole.Payload, "a", [1]),
            new StagedFragment(ArtifactRole.Manifest, "manifest", [2]));

        var publication = new CommittedPublication("solution-key", ordered);

        var property = typeof(CommittedPublication).GetProperty("ArtifactsInPublicationOrder");

        Assert.Equal("solution-key", publication.SolutionKey);
        Assert.NotNull(property);
        Assert.Equal(typeof(ImmutableArray<StagedFragment>), property.PropertyType);
        Assert.Equal(ordered, publication.ArtifactsInPublicationOrder);
    }

    [Fact]
    [Trait("Requirement", "RP-57")]
    public void ReadPayload_EagerFragment_ReturnsTheStoredArray()
    {
        ImmutableArray<byte> payload = [1, 2, 3];
        var fragment = new StagedFragment(ArtifactRole.Payload, "facts/a.json", payload);

        Assert.False(fragment.IsDeferred);
        Assert.True(fragment.ReadPayload().AsSpan().SequenceEqual(payload.AsSpan()));
        Assert.True(fragment.ReadPayload().AsSpan().SequenceEqual(payload.AsSpan()));
    }

    [Fact]
    [Trait("Requirement", "RP-57")]
    public void ReadPayload_DeferredFragment_InvokesTheProviderOnceThenRejectsASecondCall()
    {
        var invokes = 0;
        ImmutableArray<byte> bytes = [9, 8];
        var fragment = StagedFragment.Deferred(
            ArtifactRole.Payload,
            "source/a.cs",
            () =>
            {
                invokes++;
                return bytes;
            });

        Assert.True(fragment.IsDeferred);
        Assert.True(fragment.Payload.IsDefaultOrEmpty);
        Assert.True(fragment.ReadPayload().AsSpan().SequenceEqual(bytes.AsSpan()));
        Assert.Equal(1, invokes);

        Assert.Throws<InvalidOperationException>(() => fragment.ReadPayload());
        Assert.Equal(1, invokes);
    }

    [Fact]
    [Trait("Requirement", "RP-57")]
    public void CommittedPublication_DeferredFragment_IsDeferredWithEmptyPayload()
    {
        var fragment = StagedFragment.Deferred(ArtifactRole.Payload, "source/a.cs", static () => [1]);
        var publication = new CommittedPublication("sol", [fragment]);

        var returned = Assert.Single(publication.ArtifactsInPublicationOrder);
        Assert.True(returned.IsDeferred);
        Assert.True(returned.Payload.IsDefaultOrEmpty);
        Assert.Equal("source/a.cs", returned.CanonicalKey);
        Assert.Equal(ArtifactRole.Payload, returned.Role);
    }

    [Fact]
    [Trait("Requirement", "RP-57")]
    public void ReadPayload_DeferredFragment_DoesNotCopyBytesIntoPayload()
    {
        ImmutableArray<byte> bytes = [4, 5, 6];
        var fragment = StagedFragment.Deferred(ArtifactRole.Payload, "source/a.cs", () => bytes);

        Assert.True(fragment.ReadPayload().AsSpan().SequenceEqual(bytes.AsSpan()));
        Assert.True(fragment.IsDeferred);
        Assert.True(fragment.Payload.IsDefaultOrEmpty);
    }
}
