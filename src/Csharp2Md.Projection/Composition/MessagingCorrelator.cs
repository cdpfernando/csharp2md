using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;

namespace Csharp2Md.Projection.Composition;

internal sealed record CrossSolutionRelation(
    string Kind,
    string SourceFactId,
    string SourceSolutionIdentity,
    string SourceArtifactKey,
    int SourceOrdinal,
    string TargetFactId,
    string TargetSolutionIdentity,
    string TargetArtifactKey,
    int TargetOrdinal,
    string MatchedKey);

internal sealed record MessagingMatchResult(
    ImmutableArray<CrossSolutionRelation> Relations,
    ImmutableArray<CrossSolutionRelation> Candidates);

internal static class MessagingCorrelator
{
    private static readonly string Outbound = FacetAxes.WireValue(BoundaryDirection.Outbound);
    private static readonly string Inbound = FacetAxes.WireValue(BoundaryDirection.Inbound);
    private static readonly string Messaging = FacetAxes.WireValue(BoundaryProtocol.Messaging);
    private static readonly string TargetsKind = TaxonomyTables.Default.Relations
        .Single(static descriptor => descriptor.Kind == RelationKind.Targets)
        .WireName;

    public static MessagingMatchResult Match(IReadOnlyList<SolutionContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var outbound = OutboundOperations(contributions);
        var inbound = InboundOperations(contributions);
        var relations = ImmutableArray.CreateBuilder<CrossSolutionRelation>();
        foreach (var source in outbound)
        {
            foreach (var target in inbound)
            {
                if (string.Equals(source.Contribution.SolutionIdentity, target.Contribution.SolutionIdentity, StringComparison.Ordinal))
                {
                    continue;
                }

                var matchedKey = source.Operation.ProtocolOperationKey;
                if (matchedKey is null
                    || !string.Equals(matchedKey, target.Operation.ProtocolOperationKey, StringComparison.Ordinal))
                {
                    continue;
                }

                relations.Add(new CrossSolutionRelation(
                    TargetsKind,
                    source.Operation.FactId,
                    source.Contribution.SolutionIdentity,
                    source.Operation.ArtifactKey,
                    source.Operation.Ordinal,
                    target.Operation.FactId,
                    target.Contribution.SolutionIdentity,
                    target.Operation.ArtifactKey,
                    target.Operation.Ordinal,
                    matchedKey));
            }
        }

        return new MessagingMatchResult(relations.ToImmutable(), []);
    }

    private static ImmutableArray<LocatedOperation> OutboundOperations(IReadOnlyList<SolutionContribution> contributions)
    {
        var located = ImmutableArray.CreateBuilder<LocatedOperation>();
        foreach (var contribution in contributions)
        {
            if (contribution.BoundaryOperations.IsDefaultOrEmpty)
            {
                continue;
            }

            foreach (var operation in contribution.BoundaryOperations)
            {
                if (operation.ProtocolOperationKey is null)
                {
                    continue;
                }

                if (operation.Direction == Outbound && operation.Protocol == Messaging)
                {
                    located.Add(new LocatedOperation(contribution, operation));
                }
            }
        }

        return located.ToImmutable();
    }

    private static ImmutableArray<LocatedOperation> InboundOperations(IReadOnlyList<SolutionContribution> contributions)
    {
        var located = ImmutableArray.CreateBuilder<LocatedOperation>();
        foreach (var contribution in contributions)
        {
            if (contribution.BoundaryOperations.IsDefaultOrEmpty)
            {
                continue;
            }

            foreach (var operation in contribution.BoundaryOperations)
            {
                if (operation.ProtocolOperationKey is null)
                {
                    continue;
                }

                if (operation.Direction == Inbound && operation.Protocol == Messaging)
                {
                    located.Add(new LocatedOperation(contribution, operation));
                }
            }
        }

        return located.ToImmutable();
    }

    private readonly record struct LocatedOperation(
        SolutionContribution Contribution,
        ContributedBoundaryOperation Operation);
}
