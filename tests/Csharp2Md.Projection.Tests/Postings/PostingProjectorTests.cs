using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Projection.Postings;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;
using System.Text.Json.Nodes;

namespace Csharp2Md.Projection.Tests.Postings;

public sealed class PostingProjectorTests
{
    [Fact]
    [Trait("Requirement", "RP-25")]
    [Trait("Requirement", "RP-26")]
    public void Project_Outgoing_EntriesCarryArtifactKeyAndOrdinalOnly()
    {
        var view = PostingProjectionFactory.ContainsView();

        var groups = PostingProjectionFactory.ReadPosting(
            PostingProjector.Project(view),
            PostingProjector.OutgoingKey);

        PostingProjectionFactory.AssertEntriesAreCitationsOnly(groups);
        Assert.All(groups.SelectMany(static group => group.Entries), static entry =>
            Assert.StartsWith("relations/confirmed/", entry.ArtifactKey, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-25")]
    [Trait("Requirement", "RP-26")]
    public void Project_Incoming_EntriesCarryArtifactKeyAndOrdinalOnly()
    {
        var view = PostingProjectionFactory.ContainsView();

        var groups = PostingProjectionFactory.ReadPosting(
            PostingProjector.Project(view),
            PostingProjector.IncomingKey);

        PostingProjectionFactory.AssertEntriesAreCitationsOnly(groups);
        foreach (var entry in groups.SelectMany(static group => group.Entries))
        {
            var node = JsonNode.Parse(CanonicalJson.Write(entry).AsSpan()) as JsonObject;
            Assert.NotNull(node);
            Assert.Null(node["derived_from"]);
            Assert.Null(node["facets"]);
            Assert.Null(node["source"]);
            Assert.Null(node["target"]);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-25")]
    public void Project_SelfRelation_AppearsInBothOutgoingAndIncomingForThatFact()
    {
        var (view, operation) = PostingProjectionFactory.SelfTargetView();

        var fragments = PostingProjector.Project(view);
        var outgoing = Assert.Single(PostingProjectionFactory.ReadPosting(fragments, PostingProjector.OutgoingKey));
        var incoming = Assert.Single(PostingProjectionFactory.ReadPosting(fragments, PostingProjector.IncomingKey));

        Assert.Equal(operation.Reference.Id.Value, outgoing.FactId);
        Assert.Equal(operation.Reference.Id.Value, incoming.FactId);
        var outgoingEntry = Assert.Single(outgoing.Entries);
        var incomingEntry = Assert.Single(incoming.Entries);
        Assert.Equal(outgoingEntry, incomingEntry);
        var cited = PostingProjectionFactory.RelationAt(view, outgoingEntry);
        Assert.Equal(operation.Reference.Id.Value, cited.Source.Id);
        Assert.Equal(operation.Reference.Id.Value, cited.Target.Id);
    }

    [Fact]
    [Trait("Requirement", "RP-26")]
    [Trait("Requirement", "RP-27")]
    public void Project_Outgoing_CitedOrdinalYieldsRelationWhoseSourceIsTheGroupFact()
    {
        var view = PostingProjectionFactory.ContainsView();

        var groups = PostingProjectionFactory.ReadPosting(
            PostingProjector.Project(view),
            PostingProjector.OutgoingKey);

        Assert.Equal(2, groups.Length);
        foreach (var group in groups)
        {
            foreach (var entry in group.Entries)
            {
                var relation = PostingProjectionFactory.RelationAt(view, entry);
                Assert.Equal(group.FactId, relation.Source.Id);
            }
        }
    }

    [Fact]
    [Trait("Requirement", "RP-26")]
    [Trait("Requirement", "RP-27")]
    public void Project_Incoming_CitedOrdinalYieldsRelationWhoseTargetIsTheGroupFact()
    {
        var view = PostingProjectionFactory.ContainsView();

        var groups = PostingProjectionFactory.ReadPosting(
            PostingProjector.Project(view),
            PostingProjector.IncomingKey);

        Assert.Equal(2, groups.Length);
        foreach (var group in groups)
        {
            foreach (var entry in group.Entries)
            {
                var relation = PostingProjectionFactory.RelationAt(view, entry);
                Assert.Equal(group.FactId, relation.Target.Id);
            }
        }
    }

    [Fact]
    [Trait("Requirement", "RP-25")]
    public void Project_EmptyConfirmedRelations_OmitsOutgoingAndIncoming()
    {
        var view = CatalogProjectionFactory.ViewOf(CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"));

        var fragments = PostingProjector.Project(view);

        Assert.Empty(fragments);
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.Equals(PostingProjector.OutgoingKey, StringComparison.Ordinal));
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.Equals(PostingProjector.IncomingKey, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-25")]
    public void PackageProjector_ComposesPostingProjectorAfterCatalogs()
    {
        var view = PostingProjectionFactory.ContainsView();
        var reader = new EmptySourceReader();

        var composed = new PackageProjector().Project(view, reader);
        var source = Csharp2Md.Projection.Source.SourceProjector.Project(view, reader);
        var catalogs = CatalogProjector.Project(view);
        var postings = PostingProjector.Project(view);

        Assert.Equal(
            source.Select(static fragment => fragment.CanonicalKey)
                .Concat(catalogs.Select(static fragment => fragment.CanonicalKey))
                .Concat(postings.Select(static fragment => fragment.CanonicalKey)),
            composed.Select(static fragment => fragment.CanonicalKey).Take(source.Length + catalogs.Length + postings.Length));
        Assert.Equal(PostingProjector.OutgoingKey, composed[source.Length + catalogs.Length].CanonicalKey);
        Assert.Equal(PostingProjector.IncomingKey, composed[source.Length + catalogs.Length + 1].CanonicalKey);
    }

    [Fact]
    [Trait("Requirement", "RP-25")]
    public void Project_OutgoingAndIncoming_CoverEveryConfirmedRelationEndpoint()
    {
        var view = PostingProjectionFactory.ContainsView();
        var contains = view.Document.ConfirmedRelations["contains"];

        var fragments = PostingProjector.Project(view);
        var outgoing = PostingProjectionFactory.ReadPosting(fragments, PostingProjector.OutgoingKey)
            .SelectMany(static group => group.Entries)
            .ToArray();
        var incoming = PostingProjectionFactory.ReadPosting(fragments, PostingProjector.IncomingKey)
            .SelectMany(static group => group.Entries)
            .ToArray();

        Assert.Equal(contains.Length, outgoing.Length);
        Assert.Equal(contains.Length, incoming.Length);
        for (var index = 0; index < contains.Length; index++)
        {
            Assert.Contains(outgoing, entry => entry.ArtifactKey == "relations/confirmed/contains.json" && entry.Ordinal == index);
            Assert.Contains(incoming, entry => entry.ArtifactKey == "relations/confirmed/contains.json" && entry.Ordinal == index);
        }
    }
}
