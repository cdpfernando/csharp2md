using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection.Labels;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Catalogs;

internal static class CatalogProjector
{
    internal const string EntryPointsKey = "catalogs/entry-points.json";
    internal const string BoundaryOperationsKey = "catalogs/boundary-operations.json";
    internal const string ComponentsAndDeploymentUnitsKey = "catalogs/components-and-deployment-units.json";
    internal const string ContractsKey = "catalogs/contracts.json";
    internal const string DataStoresObjectsAndFieldsKey = "catalogs/data-stores-objects-and-fields.json";
    internal const string UnknownsKey = "catalogs/unknowns.json";

    public static ImmutableArray<StagedFragment> Project(
        PublishedPackageView view,
        int ceilingBytes = ShardWriter.DefaultCeilingBytes)
    {
        ArgumentNullException.ThrowIfNull(view);

        var fragments = ImmutableArray.CreateBuilder<StagedFragment>();
        Add(fragments, view, EntryPointsKey, view.Document.EntryPoints.Select(static dto => dto.Identity), ceilingBytes);
        Add(
            fragments,
            view,
            BoundaryOperationsKey,
            view.Document.BoundaryOperations.Select(static dto => dto.Identity),
            ceilingBytes);
        Add(
            fragments,
            view,
            ComponentsAndDeploymentUnitsKey,
            view.Document.Components.Select(static dto => dto.Identity)
                .Concat(view.Document.DeploymentUnits.Select(static dto => dto.Identity)),
            ceilingBytes);
        Add(fragments, view, ContractsKey, view.Document.Contracts.Select(static dto => dto.Identity), ceilingBytes);
        Add(
            fragments,
            view,
            DataStoresObjectsAndFieldsKey,
            view.Document.DataStores.Select(static dto => dto.Identity)
                .Concat(view.Document.DataObjects.Select(static dto => dto.Identity))
                .Concat(view.Document.DataFields.Select(static dto => dto.Identity)),
            ceilingBytes);
        AddUnknowns(fragments, view, ceilingBytes);
        return fragments.ToImmutable();
    }

    private static void Add(
        ImmutableArray<StagedFragment>.Builder fragments,
        PublishedPackageView view,
        string catalogKey,
        IEnumerable<FactReferenceDto> identities,
        int ceilingBytes)
    {
        var entries = new List<CatalogEntryDto>();
        foreach (var identity in identities.OrderBy(static dto => dto.Id, StringComparer.Ordinal))
        {
            if (!view.TryLocate(identity.Id, out var citation))
            {
                continue;
            }

            entries.Add(new CatalogEntryDto(
                identity.Id,
                citation.ArtifactKey,
                citation.Ordinal,
                identity.FactType,
                LabelProjector.For(identity, view)));
        }

        if (entries.Count == 0)
        {
            return;
        }

        fragments.AddRange(ShardWriter.Write(catalogKey, Nodes(entries), ceilingBytes));
    }

    private static void AddUnknowns(
        ImmutableArray<StagedFragment>.Builder fragments,
        PublishedPackageView view,
        int ceilingBytes)
    {
        var ranked = UnknownRanking.Rank(view);
        if (ranked.IsDefaultOrEmpty)
        {
            return;
        }

        // GCPC-040/GCPC-041: `item.Ordinal` is the record's position in the document's own (unsplit)
        // `Unresolved` array, not necessarily its ordinal inside whatever artifact the family split into
        // -- resolve the real, shard-aware citation instead of assuming the family stayed in a single
        // "relations/unresolved.json" (the single .Single() lookup this replaced threw once that family
        // was actually sharded, since no shard key ends with the literal unsplit base key).
        var entries = new List<CatalogEntryDto>();
        foreach (var item in ranked)
        {
            if (!view.TryLocateUnresolved(item.Ordinal, out var citation))
            {
                continue;
            }

            entries.Add(new CatalogEntryDto(
                item.Record.Source.Id,
                citation.ArtifactKey,
                citation.Ordinal,
                item.Record.Source.FactType,
                LabelProjector.For(item.Record.Source, view)));
        }

        if (entries.Count == 0)
        {
            return;
        }

        fragments.AddRange(ShardWriter.Write(UnknownsKey, Nodes(entries), ceilingBytes));
    }

    private static List<(string FactId, JsonNode Entry)> Nodes(IReadOnlyList<CatalogEntryDto> entries)
    {
        var nodes = new List<(string FactId, JsonNode Entry)>(entries.Count);
        foreach (var entry in entries)
        {
            nodes.Add((entry.FactId, Parse(CanonicalJson.Write(entry))));
        }

        return nodes;
    }

    private static JsonNode Parse(ImmutableArray<byte> utf8) =>
        JsonNode.Parse(utf8.AsSpan())
        ?? throw new InvalidOperationException("Canonical catalog entry parsed to null.");
}
