using System.Text.Json.Serialization;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Storage;

namespace Csharp2Md.Core.Projection.Aggregates;

/// <summary>
/// Every one of the eight <see cref="ResolutionMethod"/> keys, always present and zero when unused
/// (RELR-33) - a JSON object with a genuinely missing key reads as "we don't know", not "zero".
/// </summary>
internal sealed record ResolutionMethodCountsJson(
    [property: JsonPropertyOrder(0)] int Exact,
    [property: JsonPropertyOrder(1)] int Candidate,
    [property: JsonPropertyOrder(2)] int Syntactic,
    [property: JsonPropertyOrder(3)] int Configured,
    [property: JsonPropertyOrder(4)] int Convention,
    [property: JsonPropertyOrder(5)] int Dynamic,
    [property: JsonPropertyOrder(6)] int Heuristic,
    [property: JsonPropertyOrder(7)] int Unresolved)
{
    public static ResolutionMethodCountsJson Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Deliberately not an instance property: <see cref="ResolutionMethodCountsJson"/> is serialized
    /// directly by <see cref="AggregateJsonContext"/>, and an undecorated computed property risks the
    /// source generator including it as an unintended wire field. Kept as a static helper instead.
    /// </summary>
    public static int Sum(ResolutionMethodCountsJson counts) =>
        counts.Exact + counts.Candidate + counts.Syntactic + counts.Configured
        + counts.Convention + counts.Dynamic + counts.Heuristic + counts.Unresolved;

    public static ResolutionMethodCountsJson Count(IEnumerable<string> methods)
    {
        int exact = 0, candidate = 0, syntactic = 0, configured = 0, convention = 0, dynamic = 0, heuristic = 0, unresolved = 0;
        foreach (var wireMethod in methods)
        {
            switch (ResolutionMethodWire.Parse(wireMethod))
            {
                case ResolutionMethod.Exact: exact++; break;
                case ResolutionMethod.Candidate: candidate++; break;
                case ResolutionMethod.Syntactic: syntactic++; break;
                case ResolutionMethod.Configured: configured++; break;
                case ResolutionMethod.Convention: convention++; break;
                case ResolutionMethod.Dynamic: dynamic++; break;
                case ResolutionMethod.Heuristic: heuristic++; break;
                case ResolutionMethod.Unresolved: unresolved++; break;
                default: throw new ArgumentOutOfRangeException(nameof(methods), wireMethod, "Unsupported resolution method.");
            }
        }

        return new ResolutionMethodCountsJson(exact, candidate, syntactic, configured, convention, dynamic, heuristic, unresolved);
    }
}

internal sealed record ResolutionPartitionMetricsJson(
    [property: JsonPropertyOrder(0)] string Partition,
    [property: JsonPropertyOrder(1)] int Total,
    [property: JsonPropertyOrder(2)] ResolutionMethodCountsJson ByMethod);

/// <summary>
/// <c>raw/facts/relations/resolution.json</c>'s shape (design.md, envelope version 1 - distinct from the
/// other aggregate envelopes' version 2). <see cref="Total"/> and every count below it are derived
/// exclusively from the projected relation partitions, never from a second, independent tally, so
/// RELR-36 ("counts match the partition files exactly") holds by construction.
/// </summary>
internal sealed record ResolutionMetricsAggregate(
    [property: JsonPropertyOrder(0)] int SchemaVersion,
    [property: JsonPropertyOrder(1)] string Kind,
    [property: JsonPropertyOrder(2)] int Total,
    [property: JsonPropertyOrder(3)] ResolutionMethodCountsJson ByMethod,
    [property: JsonPropertyOrder(4)] ImmutableArray<ResolutionPartitionMetricsJson> ByPartition)
{
    public static ResolutionMetricsAggregate Empty { get; } = new(
        1,
        "resolution",
        0,
        ResolutionMethodCountsJson.Zero,
        [.. Enum.GetValues<RelationPartition>().Select(static partition =>
            new ResolutionPartitionMetricsJson(FactualJsonMapper.WireRelationPartition(partition), 0, ResolutionMethodCountsJson.Zero))]);
}

/// <summary>
/// Counts resolution outcomes from the already-projected relation partitions (RELR-33..RELR-36) - the
/// same source <c>raw/facts/relations/*.json</c> is written from, so a count here can never drift from
/// what a reader of those files would tally themselves.
/// </summary>
internal static class ResolutionMetricsProjector
{
    public static ResolutionMetricsAggregate Project(RelationProjectionResult relations)
    {
        ArgumentNullException.ThrowIfNull(relations);

        var byPartition = relations.Partitions
            .Select(partition =>
            {
                var methods = ResolutionMethodCountsJson.Count(
                    partition.Relations.Select(static relation => relation.ResolutionMethod));
                return new ResolutionPartitionMetricsJson(
                    FactualJsonMapper.WireRelationPartition(partition.Partition), ResolutionMethodCountsJson.Sum(methods), methods);
            })
            .OrderBy(static entry => entry.Partition, StringComparer.Ordinal)
            .ToImmutableArray();

        var byMethod = ResolutionMethodCountsJson.Count(
            relations.Partitions.SelectMany(static partition => partition.Relations)
                .Select(static relation => relation.ResolutionMethod));

        return new ResolutionMetricsAggregate(1, "resolution", ResolutionMethodCountsJson.Sum(byMethod), byMethod, byPartition);
    }
}
