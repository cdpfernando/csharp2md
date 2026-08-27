using Csharp2Md.Domain.Facets;
using Csharp2Md.Storage;

namespace Csharp2Md.Projection.Composition;

internal sealed record CorrelationCandidate(
    string SourceFactId,
    string SourceSolutionIdentity,
    string TargetFactId,
    string TargetSolutionIdentity,
    string MatchedKey,
    string? DestinationScope);

internal sealed record HttpMatchResult(
    ImmutableArray<CorrelationCandidate> Candidates,
    ImmutableArray<CrossSolutionRelation> Relations);

internal static class HttpCorrelator
{
    private static readonly string Outbound = FacetAxes.WireValue(BoundaryDirection.Outbound);
    private static readonly string Inbound = FacetAxes.WireValue(BoundaryDirection.Inbound);
    private static readonly string Http = FacetAxes.WireValue(BoundaryProtocol.Http);

    public static HttpMatchResult Match(IReadOnlyList<SolutionContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var outbound = OutboundOperations(contributions);
        var inbound = InboundOperations(contributions);
        var candidates = ImmutableArray.CreateBuilder<CorrelationCandidate>();
        foreach (var source in outbound)
        {
            if (source.Operation.HttpMethod is null || source.Operation.Route is null)
            {
                continue;
            }

            var matchedKey = source.Operation.HttpMethod + " " + source.Operation.Route;
            foreach (var target in inbound)
            {
                if (string.Equals(source.Contribution.SolutionIdentity, target.Contribution.SolutionIdentity, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(matchedKey, target.Operation.ProtocolOperationKey, StringComparison.Ordinal))
                {
                    continue;
                }

                candidates.Add(new CorrelationCandidate(
                    source.Operation.FactId,
                    source.Contribution.SolutionIdentity,
                    target.Operation.FactId,
                    target.Contribution.SolutionIdentity,
                    matchedKey,
                    source.Operation.DestinationScope));
            }
        }

        return new HttpMatchResult(candidates.ToImmutable(), []);
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
                if (operation.Direction == Outbound && operation.Protocol == Http)
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
                if (operation.Direction == Inbound && operation.Protocol == Http)
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
