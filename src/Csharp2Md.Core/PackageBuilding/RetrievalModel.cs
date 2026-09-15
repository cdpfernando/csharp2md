using Csharp2Md.Core.Analysis;

namespace Csharp2Md.Core.PackageBuilding;

internal enum AggregationScope
{
    Document,
    Project,
    Component,
    DeploymentUnit,
}

internal enum DependencyCategory
{
    ProjectReference,
    InternalInvocation,
    StructuralTypeUse,
    Http,
    Grpc,
    Messaging,
    Contract,
    Persistence,
}

internal enum DependencyNature
{
    Direct,
    Transitive,
}

internal readonly record struct EntityHandle
{
    public string Value { get; }

    public EntityHandle(string value) => Value = CanonicalText.Require(value, nameof(value));
}

internal readonly record struct VariantHandle
{
    public string Value { get; }

    public VariantHandle(string value) => Value = CanonicalText.Require(value, nameof(value));
}

internal readonly record struct RelationHandle
{
    public string Value { get; }

    public RelationHandle(string value) => Value = CanonicalText.Require(value, nameof(value));
}

internal readonly record struct EvidenceHandle
{
    public string Value { get; }

    public EvidenceHandle(string value) => Value = CanonicalText.Require(value, nameof(value));
}

internal readonly record struct CycleHandle
{
    public string Value { get; }

    public CycleHandle(string value) => Value = CanonicalText.Require(value, nameof(value));
}

internal sealed record RetainedGraph
{
    public ImmutableArray<LogicalEntity> Entities { get; }

    public ImmutableArray<FactualRelation> Relations { get; }

    public ImmutableArray<KnowledgeGap> Gaps { get; }

    public ImmutableArray<EvidenceRecord> Evidence { get; }

    public ImmutableArray<SourceDocumentSnapshot> CitedSources { get; }

    public RetentionMeasurements Measurements { get; }

    public RetainedGraph(
        ImmutableArray<LogicalEntity> entities,
        ImmutableArray<FactualRelation> relations,
        ImmutableArray<KnowledgeGap> gaps,
        ImmutableArray<EvidenceRecord> evidence,
        ImmutableArray<SourceDocumentSnapshot> citedSources,
        RetentionMeasurements measurements)
    {
        ArgumentNullException.ThrowIfNull(measurements);
        Entities = Own(entities);
        Relations = Own(relations);
        Gaps = Own(gaps);
        Evidence = Own(evidence);
        CitedSources = Own(citedSources);
        Measurements = measurements;
    }

    private static ImmutableArray<T> Own<T>(ImmutableArray<T> items) =>
        items.IsDefault ? ImmutableArray<T>.Empty : ImmutableArray.CreateRange(items);
}

internal sealed record RetentionMeasurements
{
    public int RetainedCount { get; }

    public int FilteredCount { get; }

    public bool IncludesTests { get; }

    public RetentionMeasurements(int retainedCount, int filteredCount, bool includesTests = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(retainedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(filteredCount);
        RetainedCount = retainedCount;
        FilteredCount = filteredCount;
        IncludesTests = includesTests;
    }
}

internal sealed record AggregatedDependency
{
    public AggregationScope Scope { get; }

    public EntityHandle Source { get; }

    public EntityHandle Target { get; }

    public DependencyCategory Category { get; }

    public DependencyNature Nature { get; }

    public int OccurrenceCount { get; }

    public ImmutableArray<VariantHandle> Variants { get; }

    public ImmutableArray<RelationHandle> Relations { get; }

    public ImmutableArray<EvidenceHandle> Evidence { get; }

    public AggregatedDependency(
        AggregationScope scope,
        EntityHandle source,
        EntityHandle target,
        DependencyCategory category,
        DependencyNature nature,
        int occurrenceCount,
        ImmutableArray<VariantHandle> variants,
        ImmutableArray<RelationHandle> relations,
        ImmutableArray<EvidenceHandle> evidence)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        if (!Enum.IsDefined(nature))
        {
            throw new ArgumentOutOfRangeException(nameof(nature));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(occurrenceCount);
        Scope = scope;
        Source = source;
        Target = target;
        Category = category;
        Nature = nature;
        OccurrenceCount = occurrenceCount;
        Variants = variants.IsDefault ? ImmutableArray<VariantHandle>.Empty : ImmutableArray.CreateRange(variants);
        Relations = relations.IsDefault ? ImmutableArray<RelationHandle>.Empty : ImmutableArray.CreateRange(relations);
        Evidence = evidence.IsDefault ? ImmutableArray<EvidenceHandle>.Empty : ImmutableArray.CreateRange(evidence);
    }
}

internal sealed record GapCounts
{
    public int Candidate { get; }

    public int Unknown { get; }

    public int OpenFrontier { get; }

    public GapCounts(int candidate, int unknown, int openFrontier)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(candidate);
        ArgumentOutOfRangeException.ThrowIfNegative(unknown);
        ArgumentOutOfRangeException.ThrowIfNegative(openFrontier);
        Candidate = candidate;
        Unknown = unknown;
        OpenFrontier = openFrontier;
    }
}

internal sealed record ImpactTarget
{
    public EntityHandle Entity { get; }

    public int Depth { get; }

    public ImpactTarget(EntityHandle entity, int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(depth);
        Entity = entity;
        Depth = depth;
    }
}

internal sealed record ScopeMeasures
{
    public AggregationScope Scope { get; }

    public EntityHandle Entity { get; }

    public int FanIn { get; }

    public int FanOut { get; }

    public int CrossComponentEdges { get; }

    public ImmutableArray<CycleHandle> Cycles { get; }

    public ImmutableArray<ImpactTarget> ReverseImpact { get; }

    public GapCounts Gaps { get; }

    public ScopeMeasures(
        AggregationScope scope,
        EntityHandle entity,
        int fanIn,
        int fanOut,
        int crossComponentEdges,
        ImmutableArray<CycleHandle> cycles,
        ImmutableArray<ImpactTarget> reverseImpact,
        GapCounts gaps)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(fanIn);
        ArgumentOutOfRangeException.ThrowIfNegative(fanOut);
        ArgumentOutOfRangeException.ThrowIfNegative(crossComponentEdges);
        ArgumentNullException.ThrowIfNull(gaps);
        Scope = scope;
        Entity = entity;
        FanIn = fanIn;
        FanOut = fanOut;
        CrossComponentEdges = crossComponentEdges;
        Cycles = cycles.IsDefault ? ImmutableArray<CycleHandle>.Empty : ImmutableArray.CreateRange(cycles);
        ReverseImpact = reverseImpact.IsDefault ? ImmutableArray<ImpactTarget>.Empty : ImmutableArray.CreateRange(reverseImpact);
        Gaps = gaps;
    }
}

internal sealed record NavigationIndexes
{
    public string Identity { get; }

    public string Roots { get; }

    public string Outgoing { get; }

    public string Incoming { get; }

    public string Contracts { get; }

    public string Persistence { get; }

    public string Evidence { get; }

    public NavigationIndexes(
        string identity,
        string roots,
        string outgoing,
        string incoming,
        string contracts,
        string persistence,
        string evidence)
    {
        Identity = CanonicalText.Require(identity, nameof(identity));
        Roots = CanonicalText.Require(roots, nameof(roots));
        Outgoing = CanonicalText.Require(outgoing, nameof(outgoing));
        Incoming = CanonicalText.Require(incoming, nameof(incoming));
        Contracts = CanonicalText.Require(contracts, nameof(contracts));
        Persistence = CanonicalText.Require(persistence, nameof(persistence));
        Evidence = CanonicalText.Require(evidence, nameof(evidence));
    }
}

internal sealed record SolutionNavigation
{
    public SolutionIdentity Solution { get; }

    public ImmutableArray<EntityHandle> Roots { get; }

    public SolutionNavigation(SolutionIdentity solution, ImmutableArray<EntityHandle> roots)
    {
        ArgumentNullException.ThrowIfNull(solution);
        Solution = solution;
        Roots = roots.IsDefault ? ImmutableArray<EntityHandle>.Empty : ImmutableArray.CreateRange(roots);
    }
}

internal sealed record RetrievalModel
{
    public ImmutableArray<SolutionNavigation> Solutions { get; }

    public ImmutableArray<AggregatedDependency> Dependencies { get; }

    public ImmutableArray<ScopeMeasures> Measures { get; }

    public NavigationIndexes Indexes { get; }

    public RetrievalModel(
        ImmutableArray<SolutionNavigation> solutions,
        ImmutableArray<AggregatedDependency> dependencies,
        ImmutableArray<ScopeMeasures> measures,
        NavigationIndexes indexes)
    {
        ArgumentNullException.ThrowIfNull(indexes);
        Solutions = solutions.IsDefault ? ImmutableArray<SolutionNavigation>.Empty : ImmutableArray.CreateRange(solutions);
        Dependencies = dependencies.IsDefault ? ImmutableArray<AggregatedDependency>.Empty : ImmutableArray.CreateRange(dependencies);
        Measures = measures.IsDefault ? ImmutableArray<ScopeMeasures>.Empty : ImmutableArray.CreateRange(measures);
        Indexes = indexes;
    }
}
