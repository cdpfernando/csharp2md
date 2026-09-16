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
    internal static PackageBudget Default { get; } = new(1_500, 64L * 1024 * 1024);
}

internal sealed class PackageBudgetExceededException : InvalidOperationException
{
    internal PackageBudgetExceededException(string limit) : base($"package-budget: '{limit}'.") { }
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
        budget ??= PackageBudget.Default;
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
        var measurement = new PublicationMeasurements(extraction, payloads.Length + 3, [new FilteredCount("retention", extraction.FilteredCount)]);
        payloads = payloads.Add(Artifact("measurements.json", ArtifactFamily.Measurement, measurement, 1));
        payloads = payloads.Add(Artifact("certification.json", ArtifactFamily.Certification, new PackageCertification([]), 0));
        payloads = payloads.Add(CompactArtifact("manifest.json", ArtifactFamily.Manifest, machine.Manifest, 1));
        var ordered = payloads.OrderBy(artifact => artifact.Path.Value, StringComparer.Ordinal).ToImmutableArray();
        var bytes = ordered.Sum(artifact => (long)artifact.Payload.Length);
        if (ordered.Length > budget.MaximumArtifacts)
        {
            throw new PackageBudgetExceededException("artifacts");
        }

        if (bytes > budget.MaximumBytes)
        {
            throw new PackageBudgetExceededException("bytes");
        }

        return new PackagePlan(machine.Manifest, ordered, Digest(ordered), measurement);
    }

    private static SolutionRetrievalModel BuildSolution(FactualGraph graph, bool includeTests)
    {
        var retained = RetentionPolicy.Apply(graph, RetainedGraphBuilder.Build(graph), includeTests);
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
