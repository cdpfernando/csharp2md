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

    public static ImmutableArray<StagedFragment> Project(PublishedPackageView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var fragments = ImmutableArray.CreateBuilder<StagedFragment>();
        Add(fragments, view, EntryPointsKey, view.Document.EntryPoints.Select(static dto => dto.Identity.Id));
        Add(fragments, view, BoundaryOperationsKey, view.Document.BoundaryOperations.Select(static dto => dto.Identity.Id));
        Add(
            fragments,
            view,
            ComponentsAndDeploymentUnitsKey,
            view.Document.Components.Select(static dto => dto.Identity.Id)
                .Concat(view.Document.DeploymentUnits.Select(static dto => dto.Identity.Id)));
        Add(fragments, view, ContractsKey, view.Document.Contracts.Select(static dto => dto.Identity.Id));
        return fragments.ToImmutable();
    }

    private static void Add(
        ImmutableArray<StagedFragment>.Builder fragments,
        PublishedPackageView view,
        string catalogKey,
        IEnumerable<string> factIds)
    {
        var entries = new List<CatalogEntryDto>();
        foreach (var factId in factIds)
        {
            if (!view.TryLocate(factId, out var citation))
            {
                continue;
            }

            entries.Add(new CatalogEntryDto(factId, citation.ArtifactKey, citation.Ordinal));
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
}
