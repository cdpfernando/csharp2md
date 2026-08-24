using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed record RelationPartitionProjection(
    RelationPartition Partition,
    ImmutableArray<RelationFactJson> Relations);

internal sealed record RelationProjectionResult(ImmutableArray<RelationPartitionProjection> Partitions)
{
    public RelationPartitionProjection Partition(RelationPartition partition) =>
        Partitions.Single(entry => entry.Partition == partition);
}

/// <summary>
/// Produces bounded aggregate views from already validated fragments. It retains relation summaries
/// only, and never interprets a detail such as a URL or logical service name as an identity. Component
/// synthesis and the component graph/index rendering that used to live here moved to
/// <c>ComponentFragmentBuilder</c> and <c>ComponentGraphProjector</c>, which resolve relation endpoints
/// through <c>GraphNodeIndex</c> instead of a project-keyed lookup that only ever matched a
/// project-shaped <c>FactId</c>.
/// </summary>
internal static class RelationProjector
{
    // Projected over the enum, not a hand-kept list: a new RelationPartition member cannot be silently
    // left without a projection, which is the same duplication the aggregate writer already dropped.
    private static readonly ImmutableArray<RelationPartition> Partitions =
        [.. Enum.GetValues<RelationPartition>()];

    public static RelationProjectionResult Project(IEnumerable<ValidatedFactFragment> fragments)
    {
        ArgumentNullException.ThrowIfNull(fragments);

        var relationBuilders = Partitions.ToDictionary(
            static partition => partition,
            static _ => ImmutableArray.CreateBuilder<RelationFactJson>());
        var relationIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fragment in fragments)
        {
            ArgumentNullException.ThrowIfNull(fragment);
            foreach (var fact in fragment.Facts)
            {
                if (fact is not RelationFact relation)
                {
                    continue;
                }

                if (!relationBuilders.TryGetValue(relation.Partition, out var relations))
                {
                    throw new InvalidOperationException($"Unsupported relation partition '{relation.Partition}'.");
                }

                if (!relationIds.Add(relation.RelationId.Value))
                {
                    throw new InvalidOperationException($"Duplicate relation identity '{relation.RelationId.Value}' in aggregate input.");
                }

                relations.Add(FactualJsonMapper.MapRelation(relation));
            }
        }

        var canonicalPartitions = Partitions
            .Select(partition => new RelationPartitionProjection(
                partition,
                relationBuilders[partition]
                    .OrderBy(static relation => relation.RelationId, StringComparer.Ordinal)
                    .ToImmutableArray()))
            .ToImmutableArray();

        return new RelationProjectionResult(canonicalPartitions);
    }
}
