namespace Csharp2Md.Analysis.Storage;

public enum ArtifactRole
{
    Payload,
    Manifest,
}

public sealed record StagedFragment
{
    public ArtifactRole Role { get; }

    public string CanonicalKey { get; }

    public ImmutableArray<byte> Payload { get; }

    public StagedFragment(ArtifactRole role, string canonicalKey, ImmutableArray<byte> payload)
    {
        Role = role;
        CanonicalKey = canonicalKey;
        Payload = payload;
    }
}

public sealed record CommittedPublication
{
    public string SolutionKey { get; }

    public ImmutableArray<StagedFragment> ArtifactsInPublicationOrder { get; }

    public CommittedPublication(string solutionKey, ImmutableArray<StagedFragment> artifactsInPublicationOrder)
    {
        SolutionKey = solutionKey;
        ArtifactsInPublicationOrder = artifactsInPublicationOrder;
    }
}
