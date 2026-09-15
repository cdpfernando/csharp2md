using System.Text;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Projection;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Tests.Validation;

/// <summary>
/// T45 (GCPC-097): a published catalog's compact labels carry the same "artifact_key" / "ordinal" /
/// "value" shape every other projected citation already does, so a corrupted label is caught by the same
/// <c>EnsureValueMatches</c> path the pre-existing <c>ProjectionValidatorTests</c> and
/// <c>ProjectionValidationAbortTests</c> already exercise for hand-written citation stubs -- extended
/// here to a real, proven label produced by <c>LabelProjector</c> and <c>CatalogProjector</c>, and proven
/// against a real prior successful publication rather than an empty one.
/// </summary>
public sealed class ProjectionValidatorLabelTests
{
    private const string SolutionKey = @"C:\src\Acme Orders.sln";
    private const string EntryPointsCatalogKey = "catalogs/entry-points.json";
    private const string ComponentLabelKind = "component";

    [Fact]
    [Trait("Requirement", "GCPC-097")]
    public void Commit_LabelValueAlteredByOneCharacter_AbortsNamingTheOffenderAndLeavesThePriorPackageByteIdentical()
    {
        var projector = new SwitchableProjector();
        var store = new InMemoryTransactionalStore(projector);
        var prior = CommitSuccessfully(store);

        projector.Corruption = label => label["value"] = (string)label["value"]! + "X";
        var exception = Assert.Throws<PublicationRejectedException>(() => CommitSuccessfully(store));

        Assert.Equal("projection-value", exception.Gate);
        Assert.Contains(EntryPointsCatalogKey, exception.Detail, StringComparison.Ordinal);
        AssertUnchanged(store, prior);
    }

    [Fact]
    [Trait("Requirement", "GCPC-097")]
    public void Commit_LabelCitingAMissingArtifactKey_AbortsNamingTheKeyAndLeavesThePriorPackageByteIdentical()
    {
        var projector = new SwitchableProjector();
        var store = new InMemoryTransactionalStore(projector);
        var prior = CommitSuccessfully(store);

        projector.Corruption = label => label["artifact_key"] = "facts/absent.json";
        var exception = Assert.Throws<PublicationRejectedException>(() => CommitSuccessfully(store));

        Assert.Equal("projection-key", exception.Gate);
        Assert.Equal("facts/absent.json", exception.Detail);
        AssertUnchanged(store, prior);
    }

    [Fact]
    [Trait("Requirement", "GCPC-097")]
    public void Commit_LabelCitingAnOutOfRangeOrdinal_AbortsNamingTheKeyAndOrdinalAndLeavesThePriorPackageByteIdentical()
    {
        var projector = new SwitchableProjector();
        var store = new InMemoryTransactionalStore(projector);
        var prior = CommitSuccessfully(store);

        projector.Corruption = label => label["ordinal"] = 999999;
        var exception = Assert.Throws<PublicationRejectedException>(() => CommitSuccessfully(store));

        Assert.Equal("projection-ordinal", exception.Gate);
        Assert.Contains("999999", exception.Detail, StringComparison.Ordinal);
        AssertUnchanged(store, prior);
    }

    private static CommittedPublication CommitSuccessfully(InMemoryTransactionalStore store)
    {
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(BuildSnapshot());
        return session.Commit();
    }

    private static void AssertUnchanged(InMemoryTransactionalStore store, CommittedPublication prior)
    {
        Assert.True(store.TryGetPublication(SolutionKey, out var after));
        Assert.Equal(
            prior.ArtifactsInPublicationOrder.Select(static fragment => fragment.CanonicalKey).Order(StringComparer.Ordinal),
            after.ArtifactsInPublicationOrder.Select(static fragment => fragment.CanonicalKey).Order(StringComparer.Ordinal));
        foreach (var fragment in prior.ArtifactsInPublicationOrder)
        {
            var match = after.ArtifactsInPublicationOrder.Single(candidate => candidate.CanonicalKey == fragment.CanonicalKey);
            Assert.True(fragment.Payload.AsSpan().SequenceEqual(match.Payload.AsSpan()), fragment.CanonicalKey);
        }
    }

    private static FactualSnapshot BuildSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Orders/Acme.Orders.csproj");
        var signature = CanonicalSymbolSignature.Create(
            "method", "global::Acme.Orders.Host", "Run", 0, "global::System.Void");
        var symbol = Symbol.Create(signature, projectId, SymbolFacetSet.Create([SymbolFacet.Callable]));
        var component = Component.Create(solutionId, "Orders.Api", []);
        var entryPoint = EntryPoint.Create(symbol.Reference, component.Reference);
        return new FactualSnapshot([component, symbol, entryPoint], [], [], [], [], []);
    }

    /// <summary>
    /// Delegates to the real <see cref="PackageProjector"/> and, once <see cref="Corruption"/> is set,
    /// mutates the one component label on the entry-points catalog's single entry before returning the
    /// fragments -- so the first (nil <see cref="Corruption"/>) commit publishes a real, valid package and
    /// only the second commit through the same store instance is corrupted.
    /// </summary>
    private sealed class SwitchableProjector : IPackageProjector
    {
        private readonly PackageProjector _real = new();

        public Action<JsonObject>? Corruption { get; set; }

        public ImmutableArray<StagedFragment> Project(PublishedPackageView view, ISourceDocumentReader source)
        {
            var fragments = _real.Project(view, source);
            if (Corruption is not { } corrupt)
            {
                return fragments;
            }

            var builder = fragments.ToBuilder();
            for (var index = 0; index < builder.Count; index++)
            {
                var fragment = builder[index];
                if (fragment.CanonicalKey != EntryPointsCatalogKey)
                {
                    continue;
                }

                var array = JsonNode.Parse(fragment.Payload.AsSpan()) as JsonArray
                    ?? throw new InvalidOperationException("Expected the entry-points catalog to be a JSON array.");
                var entry = (JsonObject)array[0]!;
                var labels = (JsonArray)entry["labels"]!;
                var label = (JsonObject)labels.Single(
                    node => (string?)((JsonObject)node!)["kind"] == ComponentLabelKind)!;
                corrupt(label);
                builder[index] = new StagedFragment(
                    fragment.Role,
                    fragment.CanonicalKey,
                    Encoding.UTF8.GetBytes(array.ToJsonString()).ToImmutableArray());
            }

            return builder.ToImmutable();
        }
    }
}
