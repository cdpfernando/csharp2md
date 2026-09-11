using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests;

/// <summary>
/// Reads one compound fact-family artifact from a raw <see cref="StagedFragment"/> sequence (the shape
/// <c>CommittedPublication.ArtifactsInPublicationOrder</c> already carries), merging every shard the
/// family split into (<c>base.&lt;bucket&gt;.json</c>, F1/GCPC-039) back into a single value with the
/// same shape the unsplit family always had -- so a fixture test asserting against
/// <c>ArchitectureFactsShard</c>, <c>StructuralFactsShard</c> and friends keeps working whether or not
/// this run's ceiling actually split the family.
/// </summary>
internal static class ShardedFactsReader
{
    public static T Read<T>(ImmutableArray<StagedFragment> fragments, string baseKey)
    {
        var shards = ResolveShards<T>(fragments, baseKey);
        if (shards.Count == 0)
        {
            throw new InvalidOperationException($"No artifact found for '{baseKey}' or any of its shards.");
        }

        if (shards.Count == 1)
        {
            return shards[0];
        }

        return Merge<T>(shards);
    }

    /// <summary>True when <paramref name="canonicalKey"/> is <paramref name="baseKey"/> itself or one of
    /// its shards (<c>base.&lt;bucket&gt;.json</c>) -- so a caller can tell whether a family was
    /// published at all without committing to a payload type yet.</summary>
    public static bool IsFamilyMember(string canonicalKey, string baseKey)
    {
        var stem = baseKey.EndsWith(".json", StringComparison.Ordinal) ? baseKey[..^".json".Length] : baseKey;
        return canonicalKey == baseKey
            || (canonicalKey.StartsWith(stem + ".", StringComparison.Ordinal)
                && canonicalKey.EndsWith(".json", StringComparison.Ordinal));
    }

    private static List<T> ResolveShards<T>(ImmutableArray<StagedFragment> fragments, string baseKey) =>
        fragments
            .Where(fragment => IsFamilyMember(fragment.CanonicalKey, baseKey))
            .OrderBy(static fragment => fragment.CanonicalKey, StringComparer.Ordinal)
            .Select(static fragment => CanonicalJson.Read<T>(fragment.Payload.AsSpan()))
            .ToList();

    private static T Merge<T>(List<T> shards)
    {
        object merged = shards switch
        {
            List<StructuralFactsShard> structural => new StructuralFactsShard(
                [.. structural.SelectMany(static s => s.Solutions)],
                [.. structural.SelectMany(static s => s.Projects)],
                [.. structural.SelectMany(static s => s.Documents)],
                [.. structural.SelectMany(static s => s.Symbols)]),
            List<ArchitectureFactsShard> architecture => new ArchitectureFactsShard(
                [.. architecture.SelectMany(static s => s.Components)],
                [.. architecture.SelectMany(static s => s.DeploymentUnits)],
                [.. architecture.SelectMany(static s => s.EntryPoints)],
                [.. architecture.SelectMany(static s => s.BoundaryOperations)],
                [.. architecture.SelectMany(static s => s.ExternalSystems)]),
            List<ContractFactsShard> contract => new ContractFactsShard(
                [.. contract.SelectMany(static s => s.Contracts)],
                [.. contract.SelectMany(static s => s.ContractBindings)],
                [.. contract.SelectMany(static s => s.ContractRevisions)]),
            List<PersistenceFactsShard> persistence => new PersistenceFactsShard(
                [.. persistence.SelectMany(static s => s.DataStores)],
                [.. persistence.SelectMany(static s => s.DataObjects)],
                [.. persistence.SelectMany(static s => s.DataFields)],
                [.. persistence.SelectMany(static s => s.DataOperations)]),
            List<ConfigurationFactsShard> configuration => new ConfigurationFactsShard(
                [.. configuration.SelectMany(static s => s.ConfigurationBindings)]),
            List<QuarantineEnvelope> quarantine => new QuarantineEnvelope(
                [.. quarantine.SelectMany(static s => s.Records)]),
            _ => throw new NotSupportedException(
                $"ShardedFactsReader has no merge defined for '{typeof(T)}' -- it never splits, or this "
                + "helper needs a new case."),
        };

        return (T)merged;
    }
}
