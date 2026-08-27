using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Catalogs;

internal static class CatalogProjectionFactory
{
    internal static readonly ManifestContext Context = new("s-test", "Acme.sln");

    internal static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    internal static SolutionId Solution => SolutionId.Create(Workspace, "src/Acme.sln");

    internal static ProjectId Project => ProjectId.Create(Solution, "src/Acme.Orders/Acme.Orders.csproj");

    internal static PublishedPackageView ViewOf(
        ImmutableArray<IFact> facts,
        ImmutableArray<ConfirmedRelation> relations = default,
        ImmutableArray<UnresolvedRecord> unresolved = default) =>
        PublishedPackageView.From(
            DomainMapper.ToWire(
                new FactualSnapshot(
                    facts,
                    [],
                    relations.IsDefault ? [] : relations,
                    [],
                    unresolved.IsDefault ? [] : unresolved,
                    []),
                Context));

    internal static PublishedPackageView ViewOf(params IFact[] facts) =>
        ViewOf(facts.ToImmutableArray());

    internal static Symbol Callable(string name) =>
        Symbol.Create(
            CanonicalSymbolSignature.Create("method", "global::Acme.Orders.Host", name, 0, "global::System.Void"),
            Project,
            SymbolFacetSet.Create([SymbolFacet.Callable]));

    internal static Component CreateComponent(string name) => Component.Create(Solution, name, []);

    internal static EntryPoint CreateEntryPoint(string symbolName, string componentName) =>
        EntryPoint.Create(Callable(symbolName).Reference, CreateComponent(componentName).Reference);

    internal static BoundaryOperation CreateInboundOperation(string symbolName, string componentName, string key) =>
        BoundaryOperation.Create(
            Callable(symbolName).Reference,
            CreateComponent(componentName).Reference,
            BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, key, "protocolOperationKey"));

    internal static ImmutableArray<CatalogEntryDto> ReadCatalog(ImmutableArray<StagedFragment> fragments, string key)
    {
        var fragment = Assert.Single(fragments, candidate => string.Equals(candidate.CanonicalKey, key, StringComparison.Ordinal));
        var payload = fragment.IsDeferred ? fragment.ReadPayload() : fragment.Payload;
        return CanonicalJson.Read<ImmutableArray<CatalogEntryDto>>(payload.AsSpan());
    }

    internal static string FactIdAt(PublishedPackageView view, ArtifactCitation citation)
    {
        var ids = IdsIn(view, citation.ArtifactKey);
        Assert.InRange(citation.Ordinal, 0, ids.Length - 1);
        return ids[citation.Ordinal];
    }

    internal static ImmutableArray<string> IdsIn(PublishedPackageView view, string artifactKey)
    {
        var document = view.Document;
        return artifactKey switch
        {
            "facts/architecture.json" => Concat(
                document.Components.Select(static dto => dto.Identity.Id),
                document.DeploymentUnits.Select(static dto => dto.Identity.Id),
                document.EntryPoints.Select(static dto => dto.Identity.Id),
                document.BoundaryOperations.Select(static dto => dto.Identity.Id),
                document.ExternalSystems.Select(static dto => dto.Identity.Id)),
            "facts/contract.json" => Concat(
                document.Contracts.Select(static dto => dto.Identity.Id),
                document.ContractBindings.Select(static dto => dto.Identity.Id),
                document.ContractRevisions.Select(static dto => dto.Identity.Id)),
            "facts/persistence.json" => Concat(
                document.DataStores.Select(static dto => dto.Identity.Id),
                document.DataObjects.Select(static dto => dto.Identity.Id),
                document.DataFields.Select(static dto => dto.Identity.Id),
                document.DataOperations.Select(static dto => dto.Identity.Id)),
            "relations/unresolved.json" => [.. document.Unresolved.Select(static dto => dto.Source.Id)],
            _ => throw new InvalidOperationException($"Unexpected catalog citation '{artifactKey}'."),
        };
    }

    internal static void AssertEveryEntryResolves(PublishedPackageView view, ImmutableArray<CatalogEntryDto> entries)
    {
        Assert.NotEmpty(entries);
        foreach (var entry in entries)
        {
            Assert.False(string.IsNullOrEmpty(entry.FactId));
            Assert.True(entry.Ordinal >= 0, entry.FactId);
            Assert.Equal(entry.FactId, FactIdAt(view, new ArtifactCitation(entry.ArtifactKey, entry.Ordinal)));
            Assert.True(view.TryLocate(entry.FactId, out var citation), entry.FactId);
            Assert.Equal(citation.ArtifactKey, entry.ArtifactKey);
            Assert.Equal(citation.Ordinal, entry.Ordinal);
        }
    }

    private static ImmutableArray<string> Concat(params IEnumerable<string>[] sequences) =>
        [.. sequences.SelectMany(static sequence => sequence)];
}
