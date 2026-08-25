using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Storage;

public sealed record FactualSnapshot
{
    public static FactualSnapshot Empty { get; } = new(
        ImmutableArray<IFact>.Empty,
        ImmutableArray<Observation>.Empty,
        ImmutableArray<ConfirmedRelation>.Empty,
        ImmutableArray<CandidateLink>.Empty,
        ImmutableArray<UnresolvedRecord>.Empty,
        ImmutableArray<OpenFrontier>.Empty);

    public ImmutableArray<IFact> Facts { get; }

    public ImmutableArray<Observation> Observations { get; }

    public ImmutableArray<ConfirmedRelation> ConfirmedRelations { get; }

    public ImmutableArray<CandidateLink> Candidates { get; }

    public ImmutableArray<UnresolvedRecord> Unresolved { get; }

    public ImmutableArray<OpenFrontier> Frontiers { get; }

    public FactualSnapshot(
        ImmutableArray<IFact> facts,
        ImmutableArray<Observation> observations,
        ImmutableArray<ConfirmedRelation> confirmedRelations,
        ImmutableArray<CandidateLink> candidates,
        ImmutableArray<UnresolvedRecord> unresolved,
        ImmutableArray<OpenFrontier> frontiers)
    {
        Facts = facts;
        Observations = observations;
        ConfirmedRelations = confirmedRelations;
        Candidates = candidates;
        Unresolved = unresolved;
        Frontiers = frontiers;
    }

    public FactualSnapshot Merge(FactualSnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new FactualSnapshot(
            Facts.AddRange(other.Facts),
            Observations.AddRange(other.Observations),
            ConfirmedRelations.AddRange(other.ConfirmedRelations),
            Candidates.AddRange(other.Candidates),
            Unresolved.AddRange(other.Unresolved),
            Frontiers.AddRange(other.Frontiers));
    }
}
