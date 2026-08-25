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
}
