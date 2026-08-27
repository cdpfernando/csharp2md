using Csharp2Md.Analysis.Storage;
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

    public static ImmutableArray<StagedFragment> Project(PublishedPackageView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var fragments = ImmutableArray.CreateBuilder<StagedFragment>();
        Add(fragments, view, EntryPointsKey, view.Document.EntryPoints.Select(static dto => dto.Identity));
        Add(fragments, view, BoundaryOperationsKey, view.Document.BoundaryOperations.Select(static dto => dto.Identity));
        Add(
            fragments,
            view,
            ComponentsAndDeploymentUnitsKey,
            view.Document.Components.Select(static dto => dto.Identity)
                .Concat(view.Document.DeploymentUnits.Select(static dto => dto.Identity)));
        Add(fragments, view, ContractsKey, view.Document.Contracts.Select(static dto => dto.Identity));
        Add(
            fragments,
            view,
            DataStoresObjectsAndFieldsKey,
            view.Document.DataStores.Select(static dto => dto.Identity)
                .Concat(view.Document.DataObjects.Select(static dto => dto.Identity))
                .Concat(view.Document.DataFields.Select(static dto => dto.Identity)));
        AddUnknowns(fragments, view);
        return fragments.ToImmutable();
    }

    private static void Add(
        ImmutableArray<StagedFragment>.Builder fragments,
        PublishedPackageView view,
        string catalogKey,
        IEnumerable<FactReferenceDto> identities)
    {
        var entries = new List<CatalogEntryDto>();
        foreach (var identity in identities.OrderBy(static dto => dto.Id, StringComparer.Ordinal))
        {
            if (!view.TryLocate(identity.Id, out var citation))
            {
                continue;
            }

            entries.Add(new CatalogEntryDto(identity.Id, citation.ArtifactKey, citation.Ordinal, identity.FactType));
        }

        if (entries.Count == 0)
        {
            return;
        }

        fragments.Add(new StagedFragment(
            ArtifactRole.Payload,
            catalogKey,
            CanonicalJson.Write(entries.ToImmutableArray())));
    }

    private static void AddUnknowns(ImmutableArray<StagedFragment>.Builder fragments, PublishedPackageView view)
    {
        var ranked = UnknownRanking.Rank(view);
        if (ranked.IsDefaultOrEmpty)
        {
            return;
        }

        var artifactKey = view.Slots
            .Single(static slot => slot.CanonicalKey.EndsWith("/unresolved.json", StringComparison.Ordinal))
            .CanonicalKey;
        var entries = ranked
            .Select(item => new CatalogEntryDto(
                item.Record.Source.Id,
                artifactKey,
                item.Ordinal,
                item.Record.Source.FactType))
            .ToImmutableArray();
        fragments.Add(new StagedFragment(ArtifactRole.Payload, UnknownsKey, CanonicalJson.Write(entries)));
    }
}
