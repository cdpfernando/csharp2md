using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;
using System.Text.Json.Nodes;

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
        ImmutableArray<UnresolvedRecord> unresolved = default,
        ImmutableArray<CandidateLink> candidates = default,
        ImmutableArray<OpenFrontier> frontiers = default) =>
        PublishedPackageView.From(
            DomainMapper.ToWire(
                new FactualSnapshot(
                    facts,
                    [],
                    relations.IsDefault ? [] : relations,
                    candidates.IsDefault ? [] : candidates,
                    unresolved.IsDefault ? [] : unresolved,
                    frontiers.IsDefault ? [] : frontiers),
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

    internal static DeploymentUnit CreateDeploymentUnit(string name) => DeploymentUnit.Create(Solution, name);

    internal static Contract CreateContract(string schemaKey) =>
        Contract.Create(StructuralLiteral.Create(LiteralRole.SchemaName, schemaKey, "proof"));

    internal static DataStore CreateStore(string name) =>
        DataStore.Create(
            DataStoreTechnology.Relational,
            StructuralLiteral.Create(LiteralRole.SchemaName, name, "name"));

    internal static DataObject CreateObject(DataStore store, string table) =>
        DataObject.Create(
            store.Reference,
            DataObjectForm.Table,
            StructuralLiteral.Create(LiteralRole.SchemaName, "dbo", "schemaName"),
            StructuralLiteral.Create(LiteralRole.TableName, table, "tableName"),
            MappingStateKind.ExplicitConfirmation);

    internal static DataField CreateField(DataObject dataObject, string name) =>
        DataField.Create(
            dataObject.Reference,
            StructuralLiteral.Create(LiteralRole.FieldName, name, "fieldName"),
            MappingStateKind.ExplicitConfirmation);

    internal static DataOperation CreateOperation(DataObject dataObject) =>
        DataOperation.Create(dataObject.Reference, DataOperationKind.Read, MappingStateKind.ExplicitConfirmation);

    internal static Domain.Facts.Solution CreateSolutionFact() => Domain.Facts.Solution.Create(Solution);

    internal static Domain.Facts.Project CreateProjectFact(string relativeProjectPath) =>
        Domain.Facts.Project.Create(ProjectId.Create(Solution, relativeProjectPath));

    internal static Document CreateDocumentFact(string relativeProjectPath, string relativePath) =>
        Document.Create(ProjectId.Create(Solution, relativeProjectPath), relativePath);

    internal static ConfirmedRelation Contains(FactReference source, FactReference target, int occurrenceOrdinal = 1) =>
        ConfirmedRelation.Create(
            RelationKind.Contains,
            source,
            target,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            Evidence(source, occurrenceOrdinal),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);

    internal static UnresolvedRecord CreateUnresolved(RelationKind kind, FactReference owner) =>
        UnresolvedRecord.Create(kind, owner, UnresolvedCause.NoCandidateFound, Evidence(owner, 1));

    internal static void AssertUnknownsResolve(PublishedPackageView view, ImmutableArray<CatalogEntryDto> entries)
    {
        Assert.NotEmpty(entries);
        foreach (var entry in entries)
        {
            Assert.False(string.IsNullOrEmpty(entry.FactId));
            Assert.False(string.IsNullOrEmpty(entry.ArtifactKey));
            Assert.True(entry.Ordinal >= 0, entry.FactId);
            Assert.Equal(entry.FactId, FactIdAt(view, new ArtifactCitation(entry.ArtifactKey, entry.Ordinal)));
            Assert.Equal(view.Document.Unresolved[entry.Ordinal].Source.Id, entry.FactId);
        }
    }

    private static EvidenceChain Evidence(FactReference owner, int occurrenceOrdinal) =>
        EvidenceChain.Create(
        [
            new ObservationIdentity(owner, ObservationKind.Invocation, NormalizedPayload.Create([]), occurrenceOrdinal),
        ]);

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

    internal static void AssertEntryValuesPresentInCitedArtifact(PublishedPackageView view, CatalogEntryDto entry)
    {
        var cited = CitedElement(view, new ArtifactCitation(entry.ArtifactKey, entry.Ordinal)).ToJsonString();
        var node = JsonNode.Parse(CanonicalJson.Write(entry).AsSpan()) as JsonObject;
        Assert.NotNull(node);
        foreach (var property in node)
        {
            if (property.Key is "artifact_key" or "ordinal")
            {
                continue;
            }

            if (property.Value is JsonValue jsonValue
                && jsonValue.TryGetValue<string>(out var text)
                && text.Length > 0)
            {
                Assert.Contains(text, cited, StringComparison.Ordinal);
            }
        }
    }

    internal static JsonNode CitedElement(PublishedPackageView view, ArtifactCitation citation)
    {
        var bytes = ArtifactBytes(view, citation.ArtifactKey);
        var node = JsonNode.Parse(bytes.AsSpan());
        Assert.NotNull(node);
        switch (node)
        {
            case JsonArray array:
                Assert.InRange(citation.Ordinal, 0, array.Count - 1);
                return array[citation.Ordinal]!;
            case JsonObject obj:
                var items = new List<JsonNode?>();
                foreach (var property in obj)
                {
                    if (property.Value is JsonArray family)
                    {
                        items.AddRange(family);
                    }
                }

                Assert.InRange(citation.Ordinal, 0, items.Count - 1);
                return items[citation.Ordinal]!;
            default:
                return node;
        }
    }

    private static ImmutableArray<byte> ArtifactBytes(PublishedPackageView view, string artifactKey)
    {
        var document = view.Document;
        return artifactKey switch
        {
            "facts/architecture.json" => CanonicalJson.Write(
                new ArchitectureFactsShard(
                    document.Components,
                    document.DeploymentUnits,
                    document.EntryPoints,
                    document.BoundaryOperations,
                    document.ExternalSystems)),
            "facts/contract.json" => CanonicalJson.Write(
                new ContractFactsShard(document.Contracts, document.ContractBindings, document.ContractRevisions)),
            "facts/persistence.json" => CanonicalJson.Write(
                new PersistenceFactsShard(
                    document.DataStores,
                    document.DataObjects,
                    document.DataFields,
                    document.DataOperations)),
            "relations/unresolved.json" => CanonicalJson.Write(document.Unresolved),
            _ => throw new InvalidOperationException($"Unexpected catalog citation '{artifactKey}'."),
        };
    }
}
