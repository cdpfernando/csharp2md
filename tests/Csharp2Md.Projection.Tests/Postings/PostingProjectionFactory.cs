using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;
using System.Text.Json.Nodes;

namespace Csharp2Md.Projection.Tests.Postings;

internal static class PostingProjectionFactory
{
    internal static PublishedPackageView ContainsView()
    {
        var solution = CatalogProjectionFactory.CreateSolutionFact();
        var project = CatalogProjectionFactory.CreateProjectFact("src/Acme.Orders/Acme.Orders.csproj");
        var document = CatalogProjectionFactory.CreateDocumentFact(
            "src/Acme.Orders/Acme.Orders.csproj",
            "src/Acme.Orders/Program.cs");
        return CatalogProjectionFactory.ViewOf(
            [solution, project, document],
            [
                CatalogProjectionFactory.Contains(solution.Reference, project.Reference, 1),
                CatalogProjectionFactory.Contains(project.Reference, document.Reference, 2),
            ]);
    }

    internal static (PublishedPackageView View, BoundaryOperation Operation) SelfTargetView()
    {
        var operation = CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge");
        var view = CatalogProjectionFactory.ViewOf(
            [operation],
            [Targets(operation, operation)]);
        return (view, operation);
    }

    internal static ConfirmedRelation Targets(IFact source, IFact target, int occurrenceOrdinal = 1) =>
        ConfirmedRelation.Create(
            RelationKind.Targets,
            source.Reference,
            target.Reference,
            EmptyFacets(),
            Evidence(source.Reference, occurrenceOrdinal),
            ClassifierIdentity.Create("csharp2md.projection.targets", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Configured,
            targetFact: target);

    internal static ConfirmedRelation Invokes(Symbol source, Symbol target, int occurrenceOrdinal = 1) =>
        ConfirmedRelation.Create(
            RelationKind.Invokes,
            source.Reference,
            target.Reference,
            EmptyFacets(),
            Evidence(source.Reference, occurrenceOrdinal),
            ClassifierIdentity.Create("csharp2md.projection.invokes", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Semantic,
            source,
            target);

    internal static FacetBinding EmptyFacets() => FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    internal static FacetBinding PayloadRoleFacets(string payloadRole)
    {
        var axes = TaxonomyTables.Default.FacetAxes.Add(
            new FacetAxisDescriptor("payload-role", TaxonomyTables.Default.PayloadRoles));
        return FacetBinding.Create(axes, ["payload-role"], [new FacetBindingEntry("payload-role", payloadRole)]);
    }

    internal static EvidenceChain Evidence(FactReference owner, int occurrenceOrdinal) =>
        EvidenceChain.Create(
        [
            new ObservationIdentity(owner, ObservationKind.Invocation, NormalizedPayload.Create([]), occurrenceOrdinal),
        ]);

    internal static ImmutableArray<PostingGroupDto> ReadPosting(ImmutableArray<StagedFragment> fragments, string key)
    {
        var fragment = Assert.Single(fragments, candidate => string.Equals(candidate.CanonicalKey, key, StringComparison.Ordinal));
        var payload = fragment.IsDeferred ? fragment.ReadPayload() : fragment.Payload;
        return CanonicalJson.Read<ImmutableArray<PostingGroupDto>>(payload.AsSpan());
    }

    internal static void AssertEntriesAreCitationsOnly(ImmutableArray<PostingGroupDto> groups)
    {
        Assert.NotEmpty(groups);
        foreach (var group in groups)
        {
            Assert.False(string.IsNullOrEmpty(group.FactId));
            Assert.NotEmpty(group.Entries);
            foreach (var entry in group.Entries)
            {
                AssertCitationOnly(entry);
            }
        }
    }

    internal static void AssertCitationOnly(PostingEntryDto entry)
    {
        Assert.False(string.IsNullOrEmpty(entry.ArtifactKey));
        Assert.True(entry.Ordinal >= 0);
        var node = JsonNode.Parse(CanonicalJson.Write(entry).AsSpan()) as JsonObject;
        Assert.NotNull(node);
        Assert.Equal(2, node.Count);
        Assert.True(node.ContainsKey("artifact_key"));
        Assert.True(node.ContainsKey("ordinal"));
        Assert.False(node.ContainsKey("derived_from"));
        Assert.False(node.ContainsKey("facets"));
        Assert.False(node.ContainsKey("kind"));
        Assert.False(node.ContainsKey("source"));
        Assert.False(node.ContainsKey("target"));
        Assert.False(node.ContainsKey("content_sha256"));
        Assert.False(node.ContainsKey("evidence_method"));
        Assert.False(node.ContainsKey("classifier"));
        Assert.False(node.ContainsKey("analysis_variants"));
    }

    internal static ConfirmedRelationDto RelationAt(PublishedPackageView view, PostingEntryDto entry)
    {
        Assert.StartsWith("relations/confirmed/", entry.ArtifactKey, StringComparison.Ordinal);
        Assert.EndsWith(".json", entry.ArtifactKey, StringComparison.Ordinal);
        var kind = entry.ArtifactKey["relations/confirmed/".Length..^".json".Length];
        Assert.True(view.Document.ConfirmedRelations.TryGetValue(kind, out var records));
        Assert.InRange(entry.Ordinal, 0, records.Length - 1);
        Assert.True(view.TryLocateRelation(kind, entry.Ordinal, out var citation));
        Assert.Equal(citation.ArtifactKey, entry.ArtifactKey);
        Assert.Equal(citation.Ordinal, entry.Ordinal);
        return records[entry.Ordinal];
    }
}
