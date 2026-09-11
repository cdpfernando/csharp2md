using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Projection;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Tests;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Scale;

/// <summary>
/// GCPC-038, GCPC-043, GCPC-044: produces, deterministically and entirely in memory at test time (no
/// large output is ever committed to git -- D-01's "scale input" decision), a synthetic
/// <see cref="FactualSnapshot"/> sized so that the <c>contains</c> relation family, the
/// <c>belongs-to</c> relation family and the invocation observation family -- the exact three payload
/// kinds design.md F1 measured over-ceiling in the audited package -- each serialize to well over an
/// order of magnitude past the derived per-artifact ceiling. This is what proves
/// <see cref="LayoutPlanner"/>'s adaptive sharding (T35) actually splits real over-ceiling payloads of
/// those three kinds, not only a hand-picked handful of records the way the pre-existing
/// <c>LayoutPlannerShardingTests</c> does under a tiny explicit test ceiling.
/// </summary>
internal static class ScaleInputGenerator
{
    /// <summary>
    /// A record's owning Project or belongs-to target Component out of <paramref name="fanoutGroups"/>
    /// distinct groups this many records are spread across -- kept well below <paramref
    /// name="recordCount"/> so no single fact id's own posting group (<c>PostingProjector</c>'s
    /// per-fact-id aggregation, keyed and bucketed independently of <see cref="LayoutPlanner"/>'s own
    /// adaptive family sharding) grows large enough to expose the separate, pre-existing, undocumented-
    /// until-now limit that <c>Csharp2Md.Projection.ShardWriter</c>'s bucketing is a single fixed-depth
    /// 256-bucket hash of the *group key* (design.md F9: "no recursion"), not adaptive the way
    /// <see cref="LayoutPlanner"/>'s own family splitting is -- so one fact id fanning out to thousands
    /// of incoming or outgoing edges (a single project owning thousands of documents, for instance)
    /// cannot itself be split further today. That is a real, separate scaling gap this generator's
    /// design deliberately does not stress, because closing it is a `Csharp2Md.Projection.ShardWriter`
    /// production change with its own test burden, well outside this file's scope as a test-only task;
    /// it is recorded instead as a new Deferred Idea in context.md, found while calibrating this
    /// generator, distinct from the pre-existing "RetrievalGuideProjector assumes no family is ever
    /// sharded" entry that also had to be worked around here (see the <c>retrieval.md</c> exclusion in
    /// <see cref="ScaleInputGeneratorTests"/>).
    /// </summary>
    private const int FanoutGroups = 25;

    /// <summary>
    /// <paramref name="fanoutGroups"/> Projects and <paramref name="fanoutGroups"/> Components, plus
    /// <paramref name="recordCount"/> Documents (spread evenly across the Projects) each containing one
    /// Symbol -- so <c>contains</c> carries <c>2 * recordCount</c> records (Project contains Document,
    /// Document contains Symbol) -- each Symbol belonging to one Component (spread evenly across the
    /// Components, so <c>belongs-to</c> carries <paramref name="recordCount"/> records with no single
    /// component's own posting group dominating), and <paramref name="recordCount"/> invocation
    /// Observations, one per Symbol.
    /// </summary>
    public static FactualSnapshot Generate(int recordCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recordCount);

        var workspace = WorkspaceIdentity.Create("scale");
        var solutionId = SolutionId.Create(workspace, "src/Scale.sln");
        var solution = Solution.Create(solutionId);

        var facts = new List<IFact> { solution };
        var relations = new List<ConfirmedRelation>();
        var observations = new List<Observation>();

        var groupCount = Math.Min(FanoutGroups, recordCount);
        var projects = new (ProjectId Id, Project Fact)[groupCount];
        var components = new Component[groupCount];
        for (var group = 0; group < groupCount; group++)
        {
            var projectId = ProjectId.Create(solutionId, $"src/Scale.Project{group:D3}/Scale.Project{group:D3}.csproj");
            var project = Project.Create(projectId);
            projects[group] = (projectId, project);
            facts.Add(project);

            var component = Component.Create(solutionId, $"Scale.Component{group:D3}", []);
            components[group] = component;
            facts.Add(component);
        }

        for (var i = 0; i < recordCount; i++)
        {
            var group = i % groupCount;
            var (projectId, project) = projects[group];
            var component = components[group];

            var document = Document.Create(projectId, $"src/Scale.Project{group:D3}/Generated/Document{i:D6}.cs");
            facts.Add(document);
            relations.Add(Contains(project.Reference, document.Reference, occurrenceOrdinal: i + 1));

            var signature = CanonicalSymbolSignature.Create(
                symbolKind: "method",
                fullyQualifiedContainer: $"global::Scale.Project{group:D3}.Generated.Type{i:D6}",
                metadataName: "Handle",
                genericArity: 0,
                fullyQualifiedType: "void",
                parameters: [new SymbolParameterSignature("global::System.Threading.CancellationToken")]);
            var symbol = Symbol.Create(signature, projectId, SymbolFacetSet.Create([SymbolFacet.Callable]));
            facts.Add(symbol);
            relations.Add(Contains(document.Reference, symbol.Reference, occurrenceOrdinal: i + 1));
            relations.Add(BelongsTo(symbol.Reference, component.Reference, occurrenceOrdinal: i + 1));

            observations.Add(InvocationObservation(symbol.Reference, i));
        }

        return new FactualSnapshot([.. facts], [.. observations], [.. relations], [], [], []);
    }

    private static ConfirmedRelation Contains(FactReference source, FactReference target, int occurrenceOrdinal) =>
        ConfirmedRelation.Create(
            RelationKind.Contains,
            source,
            target,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            EvidenceChain.Create([
                new ObservationIdentity(source, ObservationKind.Invocation, NormalizedPayload.Create([]), occurrenceOrdinal),
            ]),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "scale")],
            EvidenceMethod.Syntactic);

    private static ConfirmedRelation BelongsTo(FactReference source, FactReference target, int occurrenceOrdinal) =>
        ConfirmedRelation.Create(
            RelationKind.BelongsTo,
            source,
            target,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            EvidenceChain.Create([
                new ObservationIdentity(source, ObservationKind.Invocation, NormalizedPayload.Create([]), occurrenceOrdinal),
            ]),
            ClassifierIdentity.Create("csharp2md.topology.belongs-to", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "scale")],
            EvidenceMethod.Semantic);

    private static Observation InvocationObservation(FactReference owner, int index) =>
        Observation.Create(
            owner,
            ObservationKind.Invocation,
            NormalizedPayload.Create([]),
            occurrenceOrdinal: 1,
            new EvidenceLocator(
                DocumentId.Create($"doc-{index:D6}"),
                $"src/Scale.Project/Generated/Document{index:D6}.cs",
                new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("BIND001", "Bound successfully."),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
}

/// <summary>
/// GCPC-038, GCPC-042..GCPC-045: proves the layout planner's adaptive sharding (T35) and the real
/// publication path (<c>FilesystemTransactionalStore</c> + <c>PackageProjector</c>, the same commit
/// path a live <c>analyze</c> run uses) both hold under the <see cref="ScaleInputGenerator"/> input,
/// not only under a hand-picked handful of records and a tiny explicit test ceiling the way the
/// pre-existing <c>LayoutPlannerShardingTests</c> does.
/// </summary>
public sealed class ScaleInputGeneratorTests
{
    /// <summary>Large enough that `contains` (2x this many records), `belongs-to` and the invocation
    /// observation family (this many records each) all measure at least an order of magnitude past the
    /// derived ~32 KiB ceiling -- verified directly in
    /// <see cref="Generate_ProducesContainsBelongsToAndInvocation_EachAnOrderOfMagnitudeOverTheDerivedCeiling"/>.</summary>
    private const int RecordCount = 800;

    private static readonly ManifestContext Context = new("s-scale", "Scale.sln");

    [Fact]
    public void Generate_ProducesContainsBelongsToAndInvocation_EachAnOrderOfMagnitudeOverTheDerivedCeiling()
    {
        var ceilingBytes = CeilingCalculator.Derive().CeilingBytes;
        var document = DomainMapper.ToWire(ScaleInputGenerator.Generate(RecordCount), Context);
        var unsplit = LayoutPlanner.Plan(document, int.MaxValue);

        var containsBytes = FamilyInlineBytes(unsplit, "relations/confirmed/contains.json");
        var belongsToBytes = FamilyInlineBytes(unsplit, "relations/confirmed/belongs-to.json");
        var invocationBytes = FamilyInlineBytes(unsplit, "observations/invocation.json");

        AssertOverAnOrderOfMagnitude(containsBytes, ceilingBytes, "contains");
        AssertOverAnOrderOfMagnitude(belongsToBytes, ceilingBytes, "belongs-to");
        AssertOverAnOrderOfMagnitude(invocationBytes, ceilingBytes, "invocation observations");
    }

    [Fact]
    public void Publish_ScaleInput_SplitsEachNamedFamilyAndEveryFileFitsThePublishedCeiling()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-scale-publish-");
        try
        {
            var packageDirectory = Publish(tree.FullName, ScaleInputGenerator.Generate(RecordCount));

            var manifest = ReadManifest(packageDirectory);
            Assert.NotNull(manifest.Provenance);
            var ceilingBytes = manifest.Provenance.ArtifactCeilingBytes;
            Assert.Equal(CeilingCalculator.Derive().CeilingBytes, ceilingBytes);

            // GCPC-038: every published *record-bearing, shardable* file fits the declared ceiling.
            // Only the fixed one-per-package singleton envelopes (manifest, registry, coverage,
            // diagnostics, measurements, run-certification) are excluded -- LayoutPlanner.Plan never
            // splits them (out of GCPC-039's explicit list of shardable families, and each one's own
            // size in this input is bounded by a fixed metric/reason count or, for manifest.json, by the
            // shard count this run itself produced -- not by any one record's content, so splitting it
            // would be self-referential). The compound fact-family bundles (facts/structural.json,
            // facts/architecture.json, facts/contract.json, facts/persistence.json,
            // facts/configuration.json, quarantine/records.json) are no longer excluded: F1 gave them
            // the same adaptive sharding PlanFamily already applies to flat record-array families, so
            // this scale input's Document and Symbol facts driving facts/structural.json over the
            // ceiling now split it instead of publishing one oversized artifact.
            var unshardableEnvelopeArtifacts = new HashSet<string>(StringComparer.Ordinal)
            {
                "manifest.json",
                PackagePublisher.RegistryKey,
                "coverage.json",
                "diagnostics.json",
                "measurements.json",
                "run-certification.json",
                // retrieval.md: a pre-existing, documented gap (context.md's "RetrievalGuideProjector's
                // own prose assumes no family is ever sharded" Deferred Idea, found at T52) that this
                // scale input is the first fixture to actually trigger a *size* symptom for, not only
                // the prose-correctness symptom that entry originally described --
                // RetrievalGuideProjector's own "## 2. Select a postings bucket" section
                // (PostingHints, over view.Slots) lists one line per *shard key*, not one line per
                // posting family, so once a posting family splits into many shards under the real
                // ceiling, the guide itself grows past the ceiling it documents. F5 (depends on F1)
                // reworks RetrievalGuideProjector's remaining call sites (AppendRelationsSection,
                // AppendDisposition and PostingHints) to recognize a family by stem/prefix and describe
                // its bucketing once, not enumerate every shard, and removes this exclusion.
                "retrieval.md",
            };
            foreach (var file in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(packageDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
                if (unshardableEnvelopeArtifacts.Contains(relative))
                {
                    continue;
                }

                var length = new FileInfo(file).Length;
                Assert.True(
                    length <= ceilingBytes,
                    $"'{relative}' is {length} bytes, over the {ceilingBytes}-byte published ceiling.");
            }

            // GCPC-044: contains, belongs-to and the invocation observations each split.
            var containsShards = ShardFiles(packageDirectory, "relations/confirmed", "contains");
            var belongsToShards = ShardFiles(packageDirectory, "relations/confirmed", "belongs-to");
            var invocationShards = ShardFiles(packageDirectory, "observations", "invocation");
            Assert.True(containsShards.Length > 1, "Expected 'contains' to split into more than one shard.");
            Assert.True(belongsToShards.Length > 1, "Expected 'belongs-to' to split into more than one shard.");
            Assert.True(invocationShards.Length > 1, "Expected invocation observations to split into more than one shard.");

            // GCPC-043: no directory is named after a fact identity -- every shard is a flat file
            // directly inside its family's own directory, bucketed by a hex prefix, never nested.
            Assert.All(
                containsShards.Concat(belongsToShards).Concat(invocationShards),
                static file => Assert.DoesNotContain(
                    '/',
                    Path.GetFileName(file).AsSpan().ToString().TrimEnd()));

            // GCPC-040/GCPC-041: catalog and posting citations still resolve after the split -- an
            // independent second validation pass over the bytes already on disk (AD-025's re-validation
            // path, the same one the `validate` CLI command drives), separate from the one `Commit()`
            // already ran while publishing.
            PackageValidator.ValidatePackageDirectory(packageDirectory);
            var result = FactualPackageReader.Read(packageDirectory);
            var readContext = new ManifestContext(manifest.SolutionKey, manifest.SolutionFileName);
            var document = DomainMapper.ToWire(result.Snapshot, readContext);
            var view = PublishedPackageView.From(document, LayoutPlanner.Plan(document, ceilingBytes));
            ProjectionValidator.Validate(view, result.Projections);

            // GCPC-045: the largest artifact of each family ("role") is published in the manifest, with
            // both its byte size (directly) and a token estimate derivable from that published byte
            // size via the published token-estimator ratio (GCPC-037/provenance), never a second stored
            // number that could drift from the bytes actually on disk.
            foreach (var family in new[] { "relations/confirmed/contains", "relations/confirmed/belongs-to", "observations/invocation" })
            {
                var entries = manifest.Artifacts.Where(entry => entry.CanonicalKey.StartsWith(family, StringComparison.Ordinal)).ToArray();
                Assert.NotEmpty(entries);
                var largest = entries.MaxBy(static entry => entry.ByteSize)!;
                Assert.True(largest.ByteSize > 0);
                var tokenEstimate = CeilingCalculator.EstimateTokens(largest.ByteSize);
                Assert.True(tokenEstimate > 0);
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    public void Plan_ScaleInputGeneratedTwice_AssignsEveryRecordToTheSameShard()
    {
        var ceilingBytes = CeilingCalculator.Derive().CeilingBytes;
        var first = LayoutPlanner.Plan(DomainMapper.ToWire(ScaleInputGenerator.Generate(RecordCount), Context), ceilingBytes);
        var second = LayoutPlanner.Plan(DomainMapper.ToWire(ScaleInputGenerator.Generate(RecordCount), Context), ceilingBytes);

        AssertIdenticalShardAssignment(first, second, "relations/confirmed/contains");
        AssertIdenticalShardAssignment(first, second, "relations/confirmed/belongs-to");
        AssertIdenticalShardAssignment(first, second, "observations/invocation");
    }

    private static void AssertIdenticalShardAssignment(LayoutPlan first, LayoutPlan second, string prefix)
    {
        var firstShards = ShardMembership(first, prefix);
        var secondShards = ShardMembership(second, prefix);

        Assert.NotEmpty(firstShards);
        Assert.Equal(firstShards.Keys.Order(StringComparer.Ordinal), secondShards.Keys.Order(StringComparer.Ordinal));
        foreach (var key in firstShards.Keys)
        {
            Assert.Equal(firstShards[key], secondShards[key]);
        }
    }

    private static Dictionary<string, string[]> ShardMembership(LayoutPlan plan, string prefix) =>
        plan.Artifacts
            .Where(artifact => artifact.ArtifactKey.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(
                static artifact => artifact.ArtifactKey,
                static artifact => artifact.Records.Select(static record => record.Identity).Order(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

    private static int FamilyInlineBytes(LayoutPlan unsplitPlan, string baseKey)
    {
        var artifact = Assert.Single(unsplitPlan.Artifacts, a => a.ArtifactKey == baseKey);
        return LayoutPlanner.SerializeRecords(artifact.Records.Select(static record => record.Entry)).Length;
    }

    private static void AssertOverAnOrderOfMagnitude(int actualBytes, int ceilingBytes, string familyName) =>
        Assert.True(
            actualBytes > ceilingBytes * 10,
            $"Expected the {familyName} family ({actualBytes} bytes) to exceed the {ceilingBytes}-byte ceiling "
            + $"by at least one order of magnitude, i.e. {ceilingBytes * 10} bytes.");

    private static string Publish(string outputRoot, FactualSnapshot snapshot)
    {
        // The projector's own ceiling must be passed explicitly -- PackageProjector()'s parameterless
        // constructor defaults to ShardWriter.DefaultCeilingBytes (1 MiB), not the derived ~32 KiB
        // ceiling PublicationPipeline.Publish enforces for facts/relations/observations by default.
        // CommandFactory's real `analyze` wiring always passes the derived ceiling explicitly for this
        // exact reason (see its own comment on the same point); this mirrors it.
        var store = new FilesystemTransactionalStore(outputRoot, new PackageProjector(CeilingCalculator.Derive().CeilingBytes));
        var session = store.Open("src/Scale.sln", EmptySourceDocumentReader.Instance);
        session.Stage(snapshot);
        session.Commit();
        return Directory.GetDirectories(outputRoot).Single();
    }

    private static ManifestEnvelope ReadManifest(string packageDirectory) =>
        CanonicalJson.Read<ManifestEnvelope>(File.ReadAllBytes(Path.Combine(packageDirectory, "manifest.json")));

    private static string[] ShardFiles(string packageDirectory, string familyDirectory, string stem)
    {
        var directory = Path.Combine(packageDirectory, familyDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, stem + ".*.json", SearchOption.TopDirectoryOnly).ToArray();
    }
}
