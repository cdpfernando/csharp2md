namespace Csharp2Md.Analysis.Storage;

public enum ArtifactRole
{
    Payload,
    Manifest,
}

public sealed record StagedFragment
{
    private readonly Func<ImmutableArray<byte>>? _materialize;
    private int _read;

    public ArtifactRole Role { get; }

    public string CanonicalKey { get; }

    public ImmutableArray<byte> Payload { get; }

    public bool IsDeferred { get; }

    public StagedFragment(ArtifactRole role, string canonicalKey, ImmutableArray<byte> payload)
    {
        Role = role;
        CanonicalKey = canonicalKey;
        Payload = payload;
        IsDeferred = false;
    }

    private StagedFragment(ArtifactRole role, string canonicalKey, Func<ImmutableArray<byte>> materialize)
    {
        Role = role;
        CanonicalKey = canonicalKey;
        Payload = [];
        IsDeferred = true;
        _materialize = materialize;
    }

    public static StagedFragment Deferred(ArtifactRole role, string key, Func<ImmutableArray<byte>> materialize)
    {
        ArgumentNullException.ThrowIfNull(materialize);
        return new StagedFragment(role, key, materialize);
    }

    public ImmutableArray<byte> ReadPayload()
    {
        if (!IsDeferred)
        {
            return Payload;
        }

        if (Interlocked.Exchange(ref _read, 1) != 0)
        {
            throw new InvalidOperationException("A deferred fragment's payload can be materialized only once.");
        }

        return _materialize!();
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
