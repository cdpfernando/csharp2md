using System.Text;
using System.Text.RegularExpressions;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Projection.Guides;
using Csharp2Md.Projection.Markdown;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Projection.Tests.Markdown;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Guides;

public sealed class RetrievalGuideProjectorTests
{
    private static readonly string[] RelationKinds =
    [
        "executes",
        "implements-operation",
        "invokes",
        "uses-contract",
        "accesses-data",
        "operates-on",
        "targets",
    ];

    [Fact]
    [Trait("Requirement", "GCPC-046")]
    public void Project_LocateSection_NamesACatalogAndNeverACanonicalPayloadArtifact()
    {
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
            CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced"));

        var section = Section(GuideText(RetrievalGuideProjector.Project(view)), "## 1. Locate an identity");
        var named = ArtifactKeys(section);

        Assert.Contains("catalogs/entry-points.json", named);
        Assert.Contains("catalogs/contracts.json", named);
        Assert.DoesNotContain(named, key => key.StartsWith("facts/", StringComparison.Ordinal));
        Assert.DoesNotContain(named, key => key.StartsWith("relations/", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-046")]
    public void Project_LocateSection_WhenNoCatalogFamilyHasFacts_NamesNoArtifactAtAll()
    {
        var view = CatalogProjectionFactory.ViewOf(CatalogProjectionFactory.Callable("Orphan"));

        var section = Section(GuideText(RetrievalGuideProjector.Project(view)), "## 1. Locate an identity");

        Assert.Empty(ArtifactKeys(section));
    }

    [Fact]
    [Trait("Requirement", "GCPC-047")]
    public void Project_PostingsSection_TeachesBucketSelectionAndNamesEachPresentBucket()
    {
        var symbol = CatalogProjectionFactory.Callable("Run");
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var entry = EntryPoint.Create(symbol.Reference, component.Reference);
        var view = CatalogProjectionFactory.ViewOf(
            [symbol, component, entry],
            [MarkdownProjectionFactory.Executes(entry, symbol)]);

        var section = Section(GuideText(RetrievalGuideProjector.Project(view)), "## 2. Select a postings bucket");
        var named = ArtifactKeys(section);

        Assert.Contains("resolve by ordinal", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never the whole canonical payload", section, StringComparison.Ordinal);
        Assert.Contains("postings/outgoing.json", named);
        Assert.Contains("postings/incoming.json", named);
    }

    [Fact]
    [Trait("Requirement", "GCPC-048")]
    public void Project_RelationsSection_DocumentsAllSevenKinds()
    {
        var view = CatalogProjectionFactory.ViewOf();

        var section = Section(GuideText(RetrievalGuideProjector.Project(view)), "## 3. Follow a confirmed relation");

        foreach (var kind in RelationKinds)
        {
            Assert.Contains("`" + kind + "`", section, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-048")]
    public void Project_RelationsSection_NamesTheHoldingArtifactOnlyForKindsPresentInThisPublication()
    {
        var view = ViewWithConfirmedRelationKinds("executes", "uses-contract");

        var section = Section(GuideText(RetrievalGuideProjector.Project(view)), "## 3. Follow a confirmed relation");
        var named = ArtifactKeys(section);

        Assert.Contains("relations/confirmed/executes.json", named);
        Assert.Contains("relations/confirmed/uses-contract.json", named);
        Assert.DoesNotContain("relations/confirmed/invokes.json", named);
        Assert.DoesNotContain("relations/confirmed/targets.json", named);
        Assert.Contains("no such relation is recognized in this package", section, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-048")]
    public void Project_RelationsSection_NamesTheHoldingArtifactForEveryOneOfTheSevenKindsWhenAllArePresent()
    {
        var view = ViewWithConfirmedRelationKinds(RelationKinds);

        var section = Section(GuideText(RetrievalGuideProjector.Project(view)), "## 3. Follow a confirmed relation");
        var named = ArtifactKeys(section);

        foreach (var kind in RelationKinds)
        {
            Assert.Contains("relations/confirmed/" + kind + ".json", named);
        }

        Assert.DoesNotContain("no such relation is recognized", section, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-048")]
    [Trait("Requirement", "APR-27")]
    public void Project_RelationsSection_ShardedInvokesFamily_IsRecognizedNotReportedAbsent()
    {
        // A real LayoutPlanner-driven split (not the hand-built ViewWithConfirmedRelationKinds fixture,
        // which never shards) -- eight Invokes relations under an 8-byte ceiling so each lands in its own
        // over-ceiling shard, matching LayoutPlannerShardingTests' established pattern.
        var view = ShardedInvokesView(count: 8, ceilingBytes: 8);

        var section = Section(GuideText(RetrievalGuideProjector.Project(view)), "## 3. Follow a confirmed relation");

        Assert.DoesNotContain("`invokes` -- no such relation is recognized in this package", section, StringComparison.Ordinal);
        Assert.Contains(
            "`invokes` -- select its posting bucket in step 2, then read the matching relations/confirmed/invokes.json shard at the cited ordinal.",
            section,
            StringComparison.Ordinal);

        // The sharded family's guide text never names an exact key this publication does not hold --
        // ValidateNoAbsentKeys would already have thrown inside RetrievalGuideProjector.Project above, but
        // assert the class of names explicitly too.
        Assert.DoesNotContain("`relations/confirmed/invokes.json`", section, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-049")]
    [Trait("Requirement", "APR-24")]
    [Trait("Requirement", "APR-27")]
    public void Project_DispositionsSection_ShardedUnresolvedFamily_IsRecognizedNotReportedAbsent()
    {
        var view = ShardedUnresolvedView(count: 8, ceilingBytes: 24);

        var section = Section(GuideText(RetrievalGuideProjector.Project(view)), "## 4. Follow an unproven disposition");

        Assert.DoesNotContain("unresolved record: none is recognized in this package", section, StringComparison.Ordinal);
        Assert.Contains(
            "unresolved record: select its bucket in `postings/unknowns.json`, then read the matching relations/unresolved.json shard at the cited ordinal",
            section,
            StringComparison.Ordinal);
        Assert.DoesNotContain("`relations/unresolved.json`", section, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "APR-25")]
    [Trait("Requirement", "APR-28")]
    [Trait("Requirement", "APR-29")]
    public void Project_DispositionsSection_ShardedUnknownPostingFamily_DescribesMatchingShardWithoutQuotingAbsentKey()
    {
        const int ceilingBytes = 24;
        var view = ShardedUnresolvedView(count: 8, ceilingBytes: ceilingBytes);

        var section = Section(
            GuideText(RetrievalGuideProjector.Project(view, ceilingBytes)),
            "## 4. Follow an unproven disposition");

        Assert.Contains(
            "unresolved record: select its bucket in the matching postings/unknowns.json shard, then read the matching relations/unresolved.json shard at the cited ordinal",
            section,
            StringComparison.Ordinal);
        Assert.DoesNotContain("`postings/unknowns.json`", section, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "APR-26")]
    public void Project_DispositionsSection_ExactFrontierPostingFamily_BackticksTheExactKey()
    {
        var owner = CatalogProjectionFactory.CreateComponent("Orders.Api").Reference;
        var frontier = Csharp2Md.Domain.Relations.OpenFrontier.Create(
            new Csharp2Md.Domain.Observations.ObservationIdentity(
                owner,
                Csharp2Md.Domain.Observations.ObservationKind.Invocation,
                Csharp2Md.Domain.Observations.NormalizedPayload.Create([]),
                1),
            Csharp2Md.Domain.Relations.FrontierCause.FurtherContinuationObserved);
        var view = CatalogProjectionFactory.ViewOf([], frontiers: [frontier]);

        var section = Section(GuideText(RetrievalGuideProjector.Project(view)), "## 4. Follow an unproven disposition");

        Assert.Contains(
            "open frontier: select its bucket in `postings/frontiers.json`, then read `relations/frontiers.json` at the cited ordinal",
            section,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "APR-26")]
    [Trait("Requirement", "APR-28")]
    [Trait("Requirement", "APR-29")]
    public void Project_DispositionsSection_ShardedFrontierPostingFamily_DescribesMatchingShardWithoutQuotingAbsentKey()
    {
        const int ceilingBytes = 24;
        var view = ShardedFrontiersView(count: 8, ceilingBytes: ceilingBytes);

        var section = Section(
            GuideText(RetrievalGuideProjector.Project(view, ceilingBytes)),
            "## 4. Follow an unproven disposition");

        Assert.Contains(
            "open frontier: select its bucket in the matching postings/frontiers.json shard, then read the matching relations/frontiers.json shard at the cited ordinal",
            section,
            StringComparison.Ordinal);
        Assert.DoesNotContain("`postings/frontiers.json`", section, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "APR-25")]
    [Trait("Requirement", "APR-26")]
    public void Project_DispositionsSection_ShardedUnknownPostingAndExactFrontierPosting_MixesWording()
    {
        // One frontier group is ~282 bytes; eight unknown groups unsplit exceed 2 KiB. A ceiling in
        // between shards only the unknown posting family, matching APR-25/APR-26 mixed wording.
        const int ceilingBytes = 400;
        var view = MixedUnknownsAndFrontiersView(unresolvedCount: 8, frontierCount: 1, ceilingBytes: ceilingBytes);

        var section = Section(
            GuideText(RetrievalGuideProjector.Project(view, ceilingBytes)),
            "## 4. Follow an unproven disposition");

        Assert.Contains(
            "unresolved record: select its bucket in the matching postings/unknowns.json shard",
            section,
            StringComparison.Ordinal);
        Assert.DoesNotContain("`postings/unknowns.json`", section, StringComparison.Ordinal);
        Assert.Contains(
            "open frontier: select its bucket in `postings/frontiers.json`",
            section,
            StringComparison.Ordinal);
        Assert.Contains("`postings/frontiers.json`", section, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "APR-31")]
    public void Project_SameViewAndCeiling_ProducesByteIdenticalRetrievalGuide()
    {
        const int ceilingBytes = 24;
        var view = ShardedUnresolvedView(count: 8, ceilingBytes: ceilingBytes);

        var first = RetrievalGuideProjector.Project(view, ceilingBytes);
        var second = RetrievalGuideProjector.Project(view, ceilingBytes);

        Assert.True(first[0].Payload.AsSpan().SequenceEqual(second[0].Payload.AsSpan()));
    }

    [Fact]
    [Trait("Requirement", "GCPC-047")]
    public void Project_PostingsSection_ShardedOutgoingFamily_DescribesBucketingOnceNotOncePerShard()
    {
        // The view's own LayoutPlan ceiling only governs RelationsSection/DispositionsSection's `slots`;
        // PostingProjector shards independently under the ceilingBytes passed to Project itself (T63's
        // ShardWriter, a separate fixed-depth bucketer) -- both need to be tiny to reproduce the guide's
        // own "one line per shard" size bug.
        var view = ShardedInvokesView(count: 40, ceilingBytes: 8);

        var section = Section(GuideText(RetrievalGuideProjector.Project(view, ceilingBytes: 8)), "## 2. Select a postings bucket");

        Assert.Single(Regex.Matches(section, "postings/outgoing").Cast<Match>());
        Assert.Contains("postings/outgoing.json (bucketed by fact id across shards)", section, StringComparison.Ordinal);
        Assert.DoesNotContain("`postings/outgoing.json`", section, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-049")]
    [Trait("Requirement", "APR-24")]
    [Trait("Requirement", "APR-26")]
    [Trait("Requirement", "APR-27")]
    public void Project_DispositionsSection_DocumentsCandidatesUnresolvedAndFrontiersEachWithTheirOwnArtifact()
    {
        var owner = CatalogProjectionFactory.CreateComponent("Orders.Api").Reference;
        var candidate = Csharp2Md.Domain.Relations.CandidateLink.Create(
            Csharp2Md.Domain.Relations.RelationKind.Contains,
            owner,
            owner,
            Evidence(owner));
        var unresolved = CatalogProjectionFactory.CreateUnresolved(Csharp2Md.Domain.Relations.RelationKind.Invokes, owner);
        var frontier = Csharp2Md.Domain.Relations.OpenFrontier.Create(
            new Csharp2Md.Domain.Observations.ObservationIdentity(
                owner,
                Csharp2Md.Domain.Observations.ObservationKind.Invocation,
                Csharp2Md.Domain.Observations.NormalizedPayload.Create([]),
                1),
            Csharp2Md.Domain.Relations.FrontierCause.FurtherContinuationObserved);
        var view = CatalogProjectionFactory.ViewOf(
            [],
            unresolved: [unresolved],
            candidates: [candidate],
            frontiers: [frontier]);

        var section = Section(GuideText(RetrievalGuideProjector.Project(view)), "## 4. Follow an unproven disposition");
        var named = ArtifactKeys(section);

        Assert.Contains("relations/candidates.json", named);
        Assert.Contains("relations/unresolved.json", named);
        Assert.Contains("relations/frontiers.json", named);
        Assert.Contains("postings/unknowns.json", named);
        Assert.Contains("postings/frontiers.json", named);
        Assert.Contains("candidate link", section, StringComparison.Ordinal);
        Assert.Contains("unresolved record", section, StringComparison.Ordinal);
        Assert.Contains("open frontier", section, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-049")]
    [Trait("Requirement", "APR-30")]
    public void Project_DispositionsSection_WhenNoneArePresent_NamesNoDispositionArtifact()
    {
        var view = CatalogProjectionFactory.ViewOf();

        var section = Section(GuideText(RetrievalGuideProjector.Project(view)), "## 4. Follow an unproven disposition");

        Assert.Empty(ArtifactKeys(section));
        Assert.Equal(3, Regex.Matches(section, "none is recognized in this package").Count);
    }

    [Fact]
    [Trait("Requirement", "GCPC-051")]
    public void Project_StoppingRules_DocumentsAllFive()
    {
        var section = Section(
            GuideText(RetrievalGuideProjector.Project(CatalogProjectionFactory.ViewOf())),
            "## 6. Stop");

        Assert.Contains("terminal effect", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cycle", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("open frontier", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("capability is unsupported", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reading budget is exhausted", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("100,000", section, StringComparison.Ordinal);
        Assert.Contains("25 file reads", section, StringComparison.Ordinal);
        Assert.Equal(5, Regex.Matches(section, @"^\d+\. ", RegexOptions.Multiline).Count);
    }

    [Fact]
    [Trait("Requirement", "GCPC-055")]
    [Trait("Requirement", "APR-28")]
    public void ValidateNoAbsentKeys_TextNamingAKeyNotInThePublication_AbortsNamingTheOffender()
    {
        const string offender = "catalogs/entry-points.json";
        var known = new HashSet<string>(StringComparer.Ordinal) { "manifest.json" };

        var exception = Assert.Throws<PublicationRejectedException>(
            () => RetrievalGuideProjector.ValidateNoAbsentKeys("Read `" + offender + "`.", known));

        Assert.Equal("projection-key", exception.Gate);
        Assert.Equal(offender, exception.Detail);
    }

    [Fact]
    [Trait("Requirement", "GCPC-055")]
    public void ValidateNoAbsentKeys_TextNamingOnlyKnownKeys_DoesNotThrow()
    {
        var known = new HashSet<string>(StringComparer.Ordinal) { "catalogs/entry-points.json" };

        RetrievalGuideProjector.ValidateNoAbsentKeys("Read `catalogs/entry-points.json`.", known);
    }

    [Fact]
    [Trait("Requirement", "GCPC-055")]
    public void ValidateNoAbsentKeys_TextNamingAWordThatIsNotArtifactShaped_DoesNotThrow()
    {
        RetrievalGuideProjector.ValidateNoAbsentKeys(
            "Follow `invokes`.",
            new HashSet<string>(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-40")]
    public void Project_RetrievalGuide_IsListedInManifest()
    {
        var store = new InMemoryTransactionalStore(new PackageProjector());
        var session = store.Open("s-test", new EmptySourceReader());
        session.Stage(new FactualSnapshot(
            [CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api")],
            [],
            [],
            [],
            [],
            []));
        var publication = session.Commit();

        var manifest = CanonicalJson.Read<ManifestEnvelope>(
            publication.ArtifactsInPublicationOrder
                .Single(static fragment => fragment.CanonicalKey == "manifest.json")
                .Payload
                .AsSpan());
        Assert.Contains(manifest.Artifacts, entry => entry.Path == RetrievalGuideProjector.Key);
        Assert.Contains(
            publication.ArtifactsInPublicationOrder,
            fragment => fragment.CanonicalKey == RetrievalGuideProjector.Key);
    }

    [Fact]
    [Trait("Requirement", "RP-40")]
    public void Project_RetrievalGuide_ContainsNoAbsolutePath()
    {
        var view = CatalogProjectionFactory.ViewOf(CatalogProjectionFactory.CreateComponent("Orders.Api"));

        var text = GuideText(RetrievalGuideProjector.Project(view));

        Assert.DoesNotContain(":\\", text, StringComparison.Ordinal);
        Assert.DoesNotContain(":/", text, StringComparison.Ordinal);
        foreach (var key in ArtifactKeys(text))
        {
            Assert.False(Path.IsPathRooted(key), key);
            Assert.False(key.StartsWith("/", StringComparison.Ordinal), key);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-055")]
    public void Project_NeverNamesAnArtifactKeyAbsentFromTheSamePublication()
    {
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
            CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced"));
        var slots = view.Slots.Select(static slot => slot.CanonicalKey).ToHashSet(StringComparer.Ordinal);
        var catalogKeys = Csharp2Md.Projection.Catalogs.CatalogProjector.Project(view)
            .Select(static fragment => fragment.CanonicalKey);
        var postingKeys = Csharp2Md.Projection.Postings.PostingProjector.Project(view)
            .Select(static fragment => fragment.CanonicalKey);
        var known = new HashSet<string>(slots, StringComparer.Ordinal);
        known.UnionWith(catalogKeys);
        known.UnionWith(postingKeys);

        var named = ArtifactKeys(GuideText(RetrievalGuideProjector.Project(view)));

        Assert.NotEmpty(named);
        Assert.All(named, key => Assert.True(known.Contains(key), key));
    }

    [Fact]
    [Trait("Requirement", "RP-37")]
    public void Project_EmptyArchitecture_StillPublishesRetrievalGuide()
    {
        var view = CatalogProjectionFactory.ViewOf();

        var fragments = RetrievalGuideProjector.Project(view);

        Assert.Equal(RetrievalGuideProjector.Key, Assert.Single(fragments).CanonicalKey);
        Assert.Empty(MarkdownProjector.Project(view));
    }

    [Fact]
    [Trait("Requirement", "RP-37")]
    [Trait("Requirement", "RP-38")]
    public void PackageProjector_NoArchitectureFacts_PublishesGuidesWithoutMarkdownPages()
    {
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateSolutionFact(),
            CatalogProjectionFactory.CreateProjectFact("src/Acme.Orders/Acme.Orders.csproj"),
            CatalogProjectionFactory.CreateDocumentFact(
                "src/Acme.Orders/Acme.Orders.csproj",
                "src/Acme.Orders/Program.cs"));
        var reader = new EmptySourceReader();

        var fragments = new PackageProjector().Project(view, reader);

        Assert.DoesNotContain(
            fragments,
            fragment => fragment.CanonicalKey.StartsWith("markdown/", StringComparison.Ordinal));
        Assert.Contains(fragments, fragment => fragment.CanonicalKey == RetrievalGuideProjector.Key);
        Assert.Contains(fragments, fragment => fragment.CanonicalKey == AgentsGuideProjector.Key);
    }

    [Fact]
    [Trait("Requirement", "RP-37")]
    public void PackageProjector_ComposesRetrievalGuideAfterMarkdown()
    {
        var view = CatalogProjectionFactory.ViewOf(CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"));
        var reader = new EmptySourceReader();

        var composed = new PackageProjector().Project(view, reader);
        var pages = MarkdownProjector.Project(view);
        var guide = RetrievalGuideProjector.Project(view);

        Assert.Equal(RetrievalGuideProjector.Key, composed[^2].CanonicalKey);
        Assert.Equal(pages.Length + 1, composed.Count(static fragment =>
            fragment.CanonicalKey.StartsWith("markdown/", StringComparison.Ordinal)
            || fragment.CanonicalKey == RetrievalGuideProjector.Key));
        Assert.Equal(guide[0].CanonicalKey, composed[^2].CanonicalKey);
        Assert.Equal(
            Encoding.UTF8.GetString(guide[0].Payload.AsSpan()),
            Encoding.UTF8.GetString(composed[^2].Payload.AsSpan()));
    }

    private static Csharp2Md.Domain.Proof.EvidenceChain Evidence(Csharp2Md.Domain.Identity.FactReference owner) =>
        Csharp2Md.Domain.Proof.EvidenceChain.Create(
        [
            new Csharp2Md.Domain.Observations.ObservationIdentity(
                owner,
                Csharp2Md.Domain.Observations.ObservationKind.Invocation,
                Csharp2Md.Domain.Observations.NormalizedPayload.Create([]),
                1),
        ]);

    /// <summary>
    /// A real, LayoutPlanner-sharded <c>relations/confirmed/invokes.json</c> family: <paramref name="count"/>
    /// distinct callee <see cref="Symbol"/>s each invoked once by a shared caller, planned under
    /// <paramref name="ceilingBytes"/> so the family splits into per-record shards (mirrors
    /// <c>LayoutPlannerShardingTests</c>' established pattern), unlike <see cref="ViewWithConfirmedRelationKinds"/>
    /// which never shards.
    /// </summary>
    private static PublishedPackageView ShardedInvokesView(int count, int ceilingBytes)
    {
        var caller = CatalogProjectionFactory.Callable("Caller");
        var facts = new List<IFact> { caller };
        var relations = new List<Csharp2Md.Domain.Relations.ConfirmedRelation>();
        for (var i = 0; i < count; i++)
        {
            var callee = Symbol.Create(
                Csharp2Md.Domain.Identity.CanonicalSymbolSignature.Create(
                    "method", $"global::Acme.Orders.Handler{i:D3}", "HandleAsync", 0, "global::System.Void"),
                CatalogProjectionFactory.Project,
                SymbolFacetSet.Create([SymbolFacet.Callable]));
            facts.Add(callee);
            relations.Add(Invokes(caller, callee.Reference));
        }

        var document = DomainMapper.ToWire(
            new FactualSnapshot([.. facts], [], [.. relations], [], [], []),
            CatalogProjectionFactory.Context);
        return PublishedPackageView.From(document, LayoutPlanner.Plan(document, ceilingBytes));
    }

    private static Csharp2Md.Domain.Relations.ConfirmedRelation Invokes(
        Symbol source, Csharp2Md.Domain.Identity.FactReference target) =>
        Csharp2Md.Domain.Relations.ConfirmedRelation.Create(
            Csharp2Md.Domain.Relations.RelationKind.Invokes,
            source.Reference,
            target,
            Csharp2Md.Domain.Relations.FacetBinding.Create(Csharp2Md.Domain.Registry.TaxonomyTables.Default.FacetAxes, [], []),
            Evidence(source.Reference),
            Csharp2Md.Domain.Proof.ClassifierIdentity.Create("csharp2md.structural.invokes", 1),
            [Csharp2Md.Domain.Identity.AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            Csharp2Md.Domain.Proof.EvidenceMethod.Semantic,
            sourceFact: source);

    /// <summary>A real, LayoutPlanner-sharded <c>relations/unresolved.json</c> family: <paramref name="count"/>
    /// unresolved records, each owned by a distinct component so they hash into distinct shards, planned
    /// under <paramref name="ceilingBytes"/>.</summary>
    private static PublishedPackageView ShardedUnresolvedView(int count, int ceilingBytes)
    {
        var records = new List<Csharp2Md.Domain.Relations.UnresolvedRecord>();
        for (var i = 0; i < count; i++)
        {
            var owner = CatalogProjectionFactory.CreateComponent($"Orders.Shard{i:D3}").Reference;
            records.Add(Csharp2Md.Domain.Relations.UnresolvedRecord.Create(
                Csharp2Md.Domain.Relations.RelationKind.Invokes,
                owner,
                Csharp2Md.Domain.Relations.UnresolvedCause.NoCandidateFound,
                Evidence(owner)));
        }

        var document = DomainMapper.ToWire(
            new FactualSnapshot([], [], [], [], [.. records], []),
            CatalogProjectionFactory.Context);
        return PublishedPackageView.From(document, LayoutPlanner.Plan(document, ceilingBytes));
    }

    private static PublishedPackageView ShardedFrontiersView(int count, int ceilingBytes)
    {
        var frontiers = new List<Csharp2Md.Domain.Relations.OpenFrontier>();
        for (var i = 0; i < count; i++)
        {
            var owner = CatalogProjectionFactory.CreateComponent($"Orders.Frontier{i:D3}").Reference;
            frontiers.Add(Csharp2Md.Domain.Relations.OpenFrontier.Create(
                new Csharp2Md.Domain.Observations.ObservationIdentity(
                    owner,
                    Csharp2Md.Domain.Observations.ObservationKind.Invocation,
                    Csharp2Md.Domain.Observations.NormalizedPayload.Create([]),
                    1),
                Csharp2Md.Domain.Relations.FrontierCause.FurtherContinuationObserved));
        }

        var document = DomainMapper.ToWire(
            new FactualSnapshot([], [], [], [], [], [.. frontiers]),
            CatalogProjectionFactory.Context);
        return PublishedPackageView.From(document, LayoutPlanner.Plan(document, ceilingBytes));
    }

    private static PublishedPackageView MixedUnknownsAndFrontiersView(
        int unresolvedCount,
        int frontierCount,
        int ceilingBytes)
    {
        var records = new List<Csharp2Md.Domain.Relations.UnresolvedRecord>();
        for (var i = 0; i < unresolvedCount; i++)
        {
            var owner = CatalogProjectionFactory.CreateComponent($"Orders.Shard{i:D3}").Reference;
            records.Add(Csharp2Md.Domain.Relations.UnresolvedRecord.Create(
                Csharp2Md.Domain.Relations.RelationKind.Invokes,
                owner,
                Csharp2Md.Domain.Relations.UnresolvedCause.NoCandidateFound,
                Evidence(owner)));
        }

        var frontiers = new List<Csharp2Md.Domain.Relations.OpenFrontier>();
        for (var i = 0; i < frontierCount; i++)
        {
            var owner = CatalogProjectionFactory.CreateComponent($"Orders.Frontier{i:D3}").Reference;
            frontiers.Add(Csharp2Md.Domain.Relations.OpenFrontier.Create(
                new Csharp2Md.Domain.Observations.ObservationIdentity(
                    owner,
                    Csharp2Md.Domain.Observations.ObservationKind.Invocation,
                    Csharp2Md.Domain.Observations.NormalizedPayload.Create([]),
                    1),
                Csharp2Md.Domain.Relations.FrontierCause.FurtherContinuationObserved));
        }

        var document = DomainMapper.ToWire(
            new FactualSnapshot([], [], [], [], [.. records], [.. frontiers]),
            CatalogProjectionFactory.Context);
        return PublishedPackageView.From(document, LayoutPlanner.Plan(document, ceilingBytes));
    }

    /// <summary>
    /// Builds a view whose <c>ConfirmedRelations</c> carries one wire-level record per named kind --
    /// bypassing domain-level <c>ConfirmedRelation.Create</c>'s per-kind shape guards (a real callable
    /// symbol for `invokes`, a registered payload-role facet for `uses-contract`, and so on), since this
    /// test only needs <see cref="PublishedPackageView.Slots"/> to carry each kind's
    /// <c>relations/confirmed/&lt;kind&gt;.json</c> artifact, which <c>LayoutPlanner</c> derives purely
    /// from a kind having at least one record -- content shape included.
    /// </summary>
    private static PublishedPackageView ViewWithConfirmedRelationKinds(params string[] kinds)
    {
        var document = CatalogProjectionFactory.ViewOf().Document;
        var confirmed = kinds.ToImmutableDictionary(
            static kind => kind,
            static kind => (ImmutableArray<ConfirmedRelationDto>)[FakeRelation(kind)],
            StringComparer.Ordinal);
        return PublishedPackageView.From(document with { ConfirmedRelations = confirmed });
    }

    private static ConfirmedRelationDto FakeRelation(string kind) =>
        new(
            kind,
            new FactReferenceDto("id1:symbol;n=src-" + kind, "Symbol"),
            new FactReferenceDto("id1:symbol;n=dst-" + kind, "Symbol"),
            [],
            [],
            new ProofAgentIdentityDto("csharp2md.test", 1),
            ["net10.0|Release||ci"],
            "semantic",
            new string('a', 64));

    private static string GuideText(ImmutableArray<StagedFragment> fragments) =>
        Encoding.UTF8.GetString(Assert.Single(fragments).Payload.AsSpan());

    private static string Section(string text, string heading)
    {
        var start = text.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(start >= 0, "Heading '" + heading + "' not found.");
        var next = text.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);
        return next < 0 ? text[start..] : text[start..(next + 1)];
    }

    private static ImmutableArray<string> ArtifactKeys(string text) =>
        [.. Regex.Matches(text, "`([^`]+)`")
            .Select(static match => match.Groups[1].Value)
            .Where(static key => key.Contains('/', StringComparison.Ordinal)
                || key.EndsWith(".json", StringComparison.Ordinal)
                || key.EndsWith(".md", StringComparison.Ordinal))];
}
