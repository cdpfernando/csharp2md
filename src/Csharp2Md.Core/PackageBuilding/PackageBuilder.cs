using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding.Measures;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.PackageBuilding.Retention;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.PackageBuilding;

internal sealed record PackageBudget(int MaximumArtifacts, long MaximumBytes)
{
    // The fallback for a corpus the spec pins no ceiling on. 96 MiB, not 64: rendering one Markdown page per
    // cited document took eShop from 49.21 to 78.60 MiB, NAV-03 owns those pages, and the spec sets no size
    // ceiling for eShop. A corpus the spec does pin is never measured against this - see ForCorpus.
    internal static PackageBudget Default { get; } = new(1_500, 96L * 1024 * 1024);

    // The ceilings CRT-04 and CRT-05 pin, keyed by the solution file the corpus is analysed through. These are
    // the spec's own numbers; nothing here may be relaxed without amending the criterion that states it.
    private static readonly ImmutableDictionary<string, PackageBudget> Pinned =
        ImmutableDictionary.CreateRange(StringComparer.OrdinalIgnoreCase,
        [
            KeyValuePair.Create("eShopOnContainers-ServicesAndWebApps.sln", new PackageBudget(1_500, 64L * 1024 * 1024)),
            KeyValuePair.Create("pitstop.sln", new PackageBudget(750, 25L * 1024 * 1024)),
        ]);

    // EDG-03 refuses the package that exceeds "o limite estrutural aplicavel ao corpus", so the limit is chosen
    // from the corpus rather than fixed. Every pinned ceiling a package touches applies to the whole committed
    // package, so the applied limit is the componentwise minimum over the matches - which also makes a pinned
    // corpus strictly tighter than Default rather than merely different from it.
    internal static PackageBudget ForCorpus(RetrievalModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var applied = Default;
        foreach (var solution in model.Solutions)
        {
            var file = Path.GetFileName(solution.Solution.LogicalRelativePath);
            if (Pinned.TryGetValue(file, out var pinned))
            {
                applied = new PackageBudget(
                    Math.Min(applied.MaximumArtifacts, pinned.MaximumArtifacts),
                    Math.Min(applied.MaximumBytes, pinned.MaximumBytes));
            }
        }

        return applied;
    }

    // The corpus whose ceiling was applied, for CRT-03's measurement and the EDG-03 diagnostic `design.md:575`
    // requires to name it. A package touching no pinned corpus reports "unpinned", which is what the 96 MiB
    // default means; one touching several reports them in canonical order, since every ceiling bound it.
    internal const string UnpinnedCorpus = "unpinned";

    internal static string DescribeCorpus(RetrievalModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var matched = model.Solutions
            .Select(solution => Path.GetFileName(solution.Solution.LogicalRelativePath))
            .Where(Pinned.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return matched.Length == 0 ? UnpinnedCorpus : string.Join(", ", matched);
    }
}

internal sealed class PackageBudgetExceededException : InvalidOperationException
{
    // `design.md:575` requires the rejection to name the family AND the corpus alongside the exceeded measure.
    internal PackageBudgetExceededException(string limit, string corpus, ImmutableArray<FamilyMeasurement> byFamily)
        : base($"package-budget: '{limit}'. corpus: '{corpus}'. by-family: {Describe(byFamily)}.") { }

    private static string Describe(ImmutableArray<FamilyMeasurement> byFamily) =>
        byFamily.IsDefaultOrEmpty
            ? "none"
            : string.Join(", ", byFamily.Select(family => $"{family.Family}={family.ArtifactCount}/{family.Bytes}"));
}

internal static class PackageBuilder
{
    internal static PackagePlan Build(
        IEnumerable<FactualGraph> graphs,
        bool includeTests = false,
        PackageBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(graphs);
        var solutions = graphs
            .OrderBy(static graph => graph.Solution.CanonicalKey, StringComparer.Ordinal)
            .Select(graph => BuildSolution(graph, includeTests))
            .ToImmutableArray();
        return Build(new RetrievalModel(solutions), includeTests, budget);
    }

    internal static PackagePlan Build(RetrievalModel model, bool includeTests = false, PackageBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        budget ??= PackageBudget.ForCorpus(model);
        if (budget.MaximumArtifacts < 1 || budget.MaximumBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(budget));
        }

        var machine = MachineArtifactWriter.Write(model, includeTests);
        var payloads = machine.Artifacts.Where(artifact => artifact.Path.Value != "manifest.json")
            .ToImmutableArray().AddRange(MarkdownRenderer.Render(model, machine.Manifest));
        var retained = model.Solutions.Select(solution => solution.RetainedGraph).Where(graph => graph is not null).Cast<RetainedGraph>().ToArray();
        var extraction = retained.Length == 0
            ? new ExtractionMeasurements(0, 0)
            : new ExtractionMeasurements(retained.Sum(graph => graph.Measurements.RetainedCount), retained.Sum(graph => graph.Measurements.FilteredCount));
        var measurement = new PublicationMeasurements(
            extraction,
            payloads.Length + 3,
            [new FilteredCount("retention", extraction.FilteredCount)],
            ByFamily(payloads),
            BySolution(payloads, machine.Manifest),
            new CorpusMeasurement(
                PackageBudget.DescribeCorpus(model),
                payloads.Length,
                payloads.Sum(static artifact => (long)artifact.Payload.Length),
                budget.MaximumArtifacts,
                budget.MaximumBytes));
        payloads = payloads.Add(Artifact("measurements.json", ArtifactFamily.Measurement, measurement, 1));
        payloads = payloads.Add(Artifact("certification.json", ArtifactFamily.Certification, new PackageCertification([]), 0));
        payloads = payloads.Add(CompactArtifact("manifest.json", ArtifactFamily.Manifest, machine.Manifest, 1));
        var ordered = payloads.OrderBy(artifact => artifact.Path.Value, StringComparer.Ordinal).ToImmutableArray();
        var digest = Digest(ordered);

        // The ceiling binds the committed package, which `design.md:530` defines as "o manifest e artefatos
        // alcancaveis da geracao committed". Publication writes one more file than the plan carries: the root
        // manifest.json pointer, outside the generation directory. Measuring the plan alone let a package of
        // exactly the ceiling commit as ceiling + 1, which is what LocalCorpusAnalyzeTests counts on disk.
        //
        // This remains a cheap early gate, not the authoritative one: publication also replaces the reserved
        // certification.json with the real certification, which is larger, so the committed byte total is not
        // knowable here. PackagePublication.EnsureWithinBudget re-checks both before the atomic swap.
        var pointerBytes = CanonicalJson.Write(new PackageGenerationPointer(digest)).Length;
        var committedArtifacts = ordered.Length + 1;
        var bytes = ordered.Sum(artifact => (long)artifact.Payload.Length) + pointerBytes;
        if (committedArtifacts > budget.MaximumArtifacts)
        {
            throw new PackageBudgetExceededException("artifacts", measurement.Corpus!.Corpus, measurement.ByFamily);
        }

        if (bytes > budget.MaximumBytes)
        {
            throw new PackageBudgetExceededException("bytes", measurement.Corpus!.Corpus, measurement.ByFamily);
        }

        return new PackagePlan(machine.Manifest, ordered, digest, measurement);
    }

    // Attribution is the "solutions/{id}/" path prefix the writers already lay out. Artifacts belonging to no
    // single solution - the Markdown summary and the trailers - are deliberately unattributed, so these counts
    // are a breakdown of the package rather than a partition of it.
    private static ImmutableArray<SolutionMeasurement> BySolution(
        ImmutableArray<PlannedArtifact> artifacts,
        PackageManifest manifest) =>
        manifest.Solutions
            .Select(solution =>
            {
                var prefix = $"solutions/{solution.Id.Value}/";
                var owned = artifacts.Where(artifact => artifact.Path.Value.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
                return new SolutionMeasurement(
                    solution.Id.Value,
                    owned.Length,
                    owned.Sum(static artifact => (long)artifact.Payload.Length));
            })
            .OrderBy(static solution => solution.SolutionId, StringComparer.Ordinal)
            .ToImmutableArray();

    // The breakdown covers the artifacts built from the model, not the three publication trailers.
    // measurements.json carries the breakdown and cannot report its own size, so manifest.json and
    // certification.json are left out with it and counts and bytes keep describing the same set.
    private static ImmutableArray<FamilyMeasurement> ByFamily(ImmutableArray<PlannedArtifact> artifacts) =>
        artifacts.GroupBy(static artifact => artifact.Family)
            .OrderBy(static group => group.Key)
            .Select(static group => new FamilyMeasurement(group.Key, group.Count(), group.Sum(static artifact => (long)artifact.Payload.Length)))
            .ToImmutableArray();

    private static SolutionRetrievalModel BuildSolution(FactualGraph graph, bool includeTests)
    {
        var retained = RetentionPolicy.Apply(graph, RetainedGraphBuilder.Build(graph, includeTests), includeTests);
        var evidence = retained.Evidence.ToDictionary(static item => item.CanonicalKey, StringComparer.Ordinal);
        var memberships = BuildMemberships(graph, retained);
        var projectsByDocument = BuildProjectsByDocument(graph);
        var contributions = retained.Relations.SelectMany(relation =>
            Contributions(relation, evidence, memberships, projectsByDocument));
        var dependencies = DependencyAggregator.Aggregate(contributions);
        var cycles = CycleCalculator.Calculate(dependencies);
        var directMeasures = DirectMeasureCalculator.Calculate(dependencies)
            .Select(measure => new ScopeMeasures(
                measure.Scope,
                measure.Entity,
                measure.FanIn,
                measure.FanOut,
                measure.CrossComponentEdges,
                cycles.Where(cycle => cycle.Scope == measure.Scope && cycle.Members.Contains(measure.Entity))
                    .Select(static cycle => cycle.Handle)
                    .OrderBy(static handle => handle.Value, StringComparer.Ordinal)
                    .ToImmutableArray(),
                measure.ReverseImpact,
                measure.Gaps))
            .ToImmutableArray();
        var impacts = ImpactCalculator.Calculate(dependencies, retained.Gaps);
        return SingleSolution(RetrievalModelBuilder.Build(
            graph,
            retained,
            dependencies,
            directMeasures,
            impacts));
    }

    private static IEnumerable<DependencyContribution> Contributions(
        FactualRelation relation,
        IReadOnlyDictionary<string, EvidenceRecord> evidence,
        IReadOnlyDictionary<string, EntityMembership> memberships,
        IReadOnlyDictionary<string, ImmutableArray<EntityHandle>> projectsByDocument)
    {
        if (!TryCategoryOf(relation.Category, out var category)
            || !memberships.TryGetValue(relation.SourceCanonicalKey, out var source)
            || !memberships.TryGetValue(relation.TargetCanonicalKey, out var target))
        {
            yield break;
        }

        foreach (var evidenceKey in relation.EvidenceCanonicalKeys.Where(evidence.ContainsKey))
        {
            var record = evidence[evidenceKey];
            var variant = new VariantHandle(CanonicalIdentity.VariantKey(record.Variant));
            var origin = Origin(record, projectsByDocument, source);
            foreach (var (scope, from, to) in Pairs(origin, source, target))
            {
                yield return new DependencyContribution(
                    scope,
                    from,
                    to,
                    category,
                    variant,
                    new RelationHandle(relation.CanonicalKey),
                    new EvidenceHandle(evidenceKey),
                    IsConfirmed: true);
            }
        }
    }

    /// <summary>
    /// Document scope starts at the evidence document. Project scope uses its proven owner.
    /// Component and deployment-unit scope keep the entity membership derivation.
    /// </summary>
    private static IEnumerable<(AggregationScope Scope, EntityHandle Source, EntityHandle Target)> Pairs(
        EntityMembership origin,
        EntityMembership source,
        EntityMembership target)
    {
        foreach (var pair in Cross(origin.Documents, target.Documents))
            yield return (AggregationScope.Document, pair.Source, pair.Target);
        foreach (var pair in Cross(origin.Projects, target.Projects))
            yield return (AggregationScope.Project, pair.Source, pair.Target);
        foreach (var pair in Cross(source.Components, target.Components))
            yield return (AggregationScope.Component, pair.Source, pair.Target);
        foreach (var pair in Cross(source.DeploymentUnits, target.DeploymentUnits))
            yield return (AggregationScope.DeploymentUnit, pair.Source, pair.Target);
    }

    private static IEnumerable<(EntityHandle Source, EntityHandle Target)> Cross(
        ImmutableArray<EntityHandle> sources,
        ImmutableArray<EntityHandle> targets) =>
        from source in sources
        from target in targets
        select (source, target);

    private static EntityMembership Origin(
        EvidenceRecord record,
        IReadOnlyDictionary<string, ImmutableArray<EntityHandle>> projectsByDocument,
        EntityMembership source)
    {
        var document = ImmutableArray.Create(new EntityHandle(record.DocumentCanonicalKey));
        var projects = projectsByDocument.TryGetValue(record.DocumentCanonicalKey, out var owning)
            ? owning
            : ImmutableArray<EntityHandle>.Empty;
        return source with { Documents = document, Projects = projects };
    }

    private static IReadOnlyDictionary<string, ImmutableArray<EntityHandle>> BuildProjectsByDocument(
        FactualGraph graph) =>
        graph.Occurrences
            .GroupBy(
                occurrence => CanonicalIdentity.CreateDocumentKey(graph.Solution, occurrence.Locator.RelativePath),
                StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => Handles(group.Select(static occurrence => occurrence.Project.CanonicalKey)),
                StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, EntityMembership> BuildMemberships(
        FactualGraph graph,
        RetainedGraph retained)
    {
        var retainedKeys = retained.Entities.Select(static entity => entity.CanonicalKey).ToHashSet(StringComparer.Ordinal);
        var occurrences = graph.Occurrences
            .Where(occurrence => retainedKeys.Contains(occurrence.EntityCanonicalKey))
            .GroupBy(static occurrence => occurrence.EntityCanonicalKey, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var kinds = retained.Entities.ToDictionary(static entity => entity.CanonicalKey, static entity => entity.Kind, StringComparer.Ordinal);
        var componentsByProject = RootsByProject(EntityKind.Component);
        var deploymentsByProject = RootsByProject(EntityKind.DeploymentUnit);

        return retained.Entities.ToDictionary(
            static entity => entity.CanonicalKey,
            entity =>
            {
                occurrences.TryGetValue(entity.CanonicalKey, out var found);
                found ??= [];
                var documents = found
                    .Select(occurrence => CanonicalIdentity.CreateDocumentKey(graph.Solution, occurrence.Locator.RelativePath));
                var projects = found.Select(static occurrence => occurrence.Project.CanonicalKey);
                var projectKeys = found.Select(static occurrence => occurrence.Project.CanonicalKey).Distinct(StringComparer.Ordinal);
                return new EntityMembership(
                    Handles(documents),
                    Handles(projects),
                    Handles(projectKeys.SelectMany(project => componentsByProject.GetValueOrDefault(project, []))),
                    Handles(projectKeys.SelectMany(project => deploymentsByProject.GetValueOrDefault(project, []))));
            },
            StringComparer.Ordinal);

        Dictionary<string, ImmutableArray<string>> RootsByProject(EntityKind kind) =>
            graph.Occurrences
                .Where(occurrence => retainedKeys.Contains(occurrence.EntityCanonicalKey)
                    && kinds.GetValueOrDefault(occurrence.EntityCanonicalKey) == kind)
                .GroupBy(static occurrence => occurrence.Project.CanonicalKey, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.Select(static occurrence => occurrence.EntityCanonicalKey)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToImmutableArray(),
                    StringComparer.Ordinal);
    }

    private static ImmutableArray<EntityHandle> Handles(IEnumerable<string> values) =>
        values.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(static value => new EntityHandle(value))
            .ToImmutableArray();

    private static bool TryCategoryOf(string category, out DependencyCategory result)
    {
        result = category switch
        {
            "project-reference" => DependencyCategory.ProjectReference,
            "internal-invocation" => DependencyCategory.InternalInvocation,
            "structural-type-use" => DependencyCategory.StructuralTypeUse,
            "http" => DependencyCategory.Http,
            "grpc" => DependencyCategory.Grpc,
            "messaging" => DependencyCategory.Messaging,
            "contract" => DependencyCategory.Contract,
            "persistence" or "persistence-store" or "persistence-operation" or "persistence-object" => DependencyCategory.Persistence,
            _ => default,
        };
        return category is "project-reference" or "internal-invocation" or "structural-type-use"
            or "http" or "grpc" or "messaging" or "contract" or "persistence"
            or "persistence-store" or "persistence-operation" or "persistence-object";
    }

    private static SolutionRetrievalModel SingleSolution(RetrievalModel model) =>
        model.Solutions.Length == 1
            ? model.Solutions[0]
            : throw new InvalidOperationException("A solution graph must build exactly one retrieval model.");

    private sealed record EntityMembership(
        ImmutableArray<EntityHandle> Documents,
        ImmutableArray<EntityHandle> Projects,
        ImmutableArray<EntityHandle> Components,
        ImmutableArray<EntityHandle> DeploymentUnits);

    private static PlannedArtifact CompactArtifact<T>(string path, ArtifactFamily family, T value, int records)
    {
        var payload = CanonicalJson.WriteCompact(value);
        return new PlannedArtifact(new RelativeArtifactPath(path), family, payload, records, Convert.ToHexStringLower(SHA256.HashData(payload.AsSpan())));
    }

    private static PlannedArtifact Artifact<T>(string path, ArtifactFamily family, T value, int records)
    {
        var payload = CanonicalJson.Write(value);
        return new PlannedArtifact(new RelativeArtifactPath(path), family, payload, records, Convert.ToHexStringLower(SHA256.HashData(payload.AsSpan())));
    }

    private static string Digest(IEnumerable<PlannedArtifact> artifacts)
    {
        var text = string.Join('\n', artifacts.Select(artifact => $"{artifact.Path.Value}:{artifact.ContentDigest}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
