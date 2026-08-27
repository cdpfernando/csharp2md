using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Composition;

public sealed class BatchComposer : IBatchComposer
{
    internal const string CrossSolutionRelationsKey = "composition/cross-solution-relations.json";
    internal const string SharedContractsKey = "composition/shared-contracts.json";
    internal const string CorrelationCandidatesKey = "composition/correlation-candidates.json";
    internal const string ComponentsAndDeploymentUnitsKey = "composition/components-and-deployment-units.json";
    internal const string ExternalSystemsKey = "composition/external-systems.json";

    private readonly int _ceilingBytes;

    public BatchComposer()
        : this(ShardWriter.DefaultCeilingBytes)
    {
    }

    public BatchComposer(int ceilingBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ceilingBytes);
        _ceilingBytes = ceilingBytes;
    }

    public SolutionContribution Contribute(
        PublishedPackageView view,
        SolutionCoordinate coordinate,
        string packageDirectory)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);

        return new SolutionContribution(
            coordinate.Identity.Value,
            coordinate.SolutionFileName,
            packageDirectory,
            ContributeBoundaryOperations(view),
            ContributeIdentities(view, view.Document.Contracts),
            ContributeNamed(view, view.Document.Components, static dto => dto.Identity, static dto => dto.Name),
            ContributeNamed(view, view.Document.DeploymentUnits, static dto => dto.Identity, static dto => dto.Name),
            ContributeNamed(view, view.Document.ExternalSystems, static dto => dto.Identity, static dto => dto.Name.Value));
    }

    public ImmutableArray<StagedFragment> Compose(BatchView batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var contributions = batch.Contributions;
        if (contributions.IsDefaultOrEmpty || HasNoCompositionFacts(contributions))
        {
            return [];
        }

        var fragments = ImmutableArray.CreateBuilder<StagedFragment>();
        var messaging = MessagingCorrelator.Match(contributions);
        Add(
            fragments,
            CrossSolutionRelationsKey,
            messaging.Relations.Select(static relation => (
                PairSortKey(
                    relation.SourceSolutionIdentity,
                    relation.SourceFactId,
                    relation.TargetSolutionIdentity,
                    relation.TargetFactId),
                (JsonNode)RelationNode(relation))).ToList());
        Add(
            fragments,
            SharedContractsKey,
            ContractCorrelator.Match(contributions).Select(static contract => (
                contract.ContractFactId,
                (JsonNode)ContractNode(contract))).ToList());
        var http = HttpCorrelator.Match(contributions);
        Add(
            fragments,
            CorrelationCandidatesKey,
            http.Candidates.Select(static candidate => (
                PairSortKey(
                    candidate.SourceSolutionIdentity,
                    candidate.SourceFactId,
                    candidate.TargetSolutionIdentity,
                    candidate.TargetFactId),
                (JsonNode)CandidateNode(candidate))).ToList());
        Add(fragments, ComponentsAndDeploymentUnitsKey, GroupEntries(GlobalComponentCatalog.Group(contributions)));
        Add(fragments, ExternalSystemsKey, GroupEntries(GlobalExternalSystemCatalog.Group(contributions)));
        return fragments.ToImmutable();
    }

    private void Add(
        ImmutableArray<StagedFragment>.Builder fragments,
        string canonicalKey,
        IReadOnlyList<(string FactId, JsonNode Entry)> entries) =>
        fragments.AddRange(ShardWriter.Write(canonicalKey, entries, _ceilingBytes));

    private static bool HasNoCompositionFacts(ImmutableArray<SolutionContribution> contributions) =>
        contributions.All(static contribution =>
            contribution.BoundaryOperations.IsDefaultOrEmpty
            && contribution.Contracts.IsDefaultOrEmpty
            && contribution.Components.IsDefaultOrEmpty
            && contribution.DeploymentUnits.IsDefaultOrEmpty
            && contribution.ExternalSystems.IsDefaultOrEmpty);

    private static string PairSortKey(
        string sourceSolution,
        string sourceFactId,
        string targetSolution,
        string targetFactId) =>
        string.Concat(sourceSolution, "\0", sourceFactId, "\0", targetSolution, "\0", targetFactId);

    private static List<(string FactId, JsonNode Entry)> GroupEntries(ImmutableArray<NamedIdentityGroup> groups)
    {
        var entries = new List<(string FactId, JsonNode Entry)>(groups.Length);
        foreach (var group in groups)
        {
            var first = group.Entries[0];
            entries.Add((
                string.Concat(group.CanonicalName, "\0", first.SolutionIdentity, "\0", first.FactId),
                GroupNode(group)));
        }

        return entries;
    }

    private static JsonObject RelationNode(CrossSolutionRelation relation) => new()
    {
        ["kind"] = relation.Kind,
        ["source_fact_id"] = relation.SourceFactId,
        ["source_solution_identity"] = relation.SourceSolutionIdentity,
        ["source_artifact_key"] = relation.SourceArtifactKey,
        ["source_ordinal"] = relation.SourceOrdinal,
        ["target_fact_id"] = relation.TargetFactId,
        ["target_solution_identity"] = relation.TargetSolutionIdentity,
        ["target_artifact_key"] = relation.TargetArtifactKey,
        ["target_ordinal"] = relation.TargetOrdinal,
        ["matched_key"] = relation.MatchedKey,
    };

    private static JsonObject ContractNode(SharedContract contract)
    {
        var owners = new JsonArray();
        foreach (var owner in contract.Owners)
        {
            owners.Add(new JsonObject
            {
                ["solution_identity"] = owner.SolutionIdentity,
                ["artifact_key"] = owner.ArtifactKey,
                ["ordinal"] = owner.Ordinal,
            });
        }

        return new JsonObject
        {
            ["fact_id"] = contract.ContractFactId,
            ["owners"] = owners,
        };
    }

    private static JsonObject CandidateNode(CorrelationCandidate candidate) => new()
    {
        ["source_fact_id"] = candidate.SourceFactId,
        ["source_solution_identity"] = candidate.SourceSolutionIdentity,
        ["target_fact_id"] = candidate.TargetFactId,
        ["target_solution_identity"] = candidate.TargetSolutionIdentity,
        ["matched_key"] = candidate.MatchedKey,
        ["destination_scope"] = candidate.DestinationScope,
    };

    private static JsonObject GroupNode(NamedIdentityGroup group)
    {
        var entries = new JsonArray();
        foreach (var entry in group.Entries)
        {
            entries.Add(new JsonObject
            {
                ["fact_id"] = entry.FactId,
                ["solution_identity"] = entry.SolutionIdentity,
                ["artifact_key"] = entry.ArtifactKey,
                ["ordinal"] = entry.Ordinal,
            });
        }

        var node = new JsonObject
        {
            ["canonical_name"] = group.CanonicalName,
        };
        if (group.SharedIdentity is not null)
        {
            node["shared_identity"] = group.SharedIdentity;
        }

        node["entries"] = entries;
        return node;
    }

    private static ImmutableArray<ContributedBoundaryOperation> ContributeBoundaryOperations(PublishedPackageView view)
    {
        if (view.Document.BoundaryOperations.IsDefaultOrEmpty)
        {
            return [];
        }

        var contributed = ImmutableArray.CreateBuilder<ContributedBoundaryOperation>();
        foreach (var operation in view.Document.BoundaryOperations)
        {
            if (!view.TryLocate(operation.Identity.Id, out var citation))
            {
                continue;
            }

            contributed.Add(new ContributedBoundaryOperation(
                operation.Identity.Id,
                operation.Identity.FactType,
                operation.Direction,
                operation.Protocol,
                operation.DestinationScope,
                operation.HttpMethod,
                operation.Route?.Value,
                operation.ProtocolOperationKey?.Value,
                citation.ArtifactKey,
                citation.Ordinal));
        }

        return contributed.ToImmutable();
    }

    private static ImmutableArray<ContributedIdentity> ContributeIdentities(
        PublishedPackageView view,
        ImmutableArray<ContractDto> contracts)
    {
        if (contracts.IsDefaultOrEmpty)
        {
            return [];
        }

        var contributed = ImmutableArray.CreateBuilder<ContributedIdentity>();
        foreach (var contract in contracts)
        {
            if (!view.TryLocate(contract.Identity.Id, out var citation))
            {
                continue;
            }

            contributed.Add(new ContributedIdentity(
                contract.Identity.Id,
                contract.Identity.FactType,
                citation.ArtifactKey,
                citation.Ordinal));
        }

        return contributed.ToImmutable();
    }

    private static ImmutableArray<ContributedNamedIdentity> ContributeNamed<T>(
        PublishedPackageView view,
        ImmutableArray<T> facts,
        Func<T, FactReferenceDto> identity,
        Func<T, string> name)
    {
        if (facts.IsDefaultOrEmpty)
        {
            return [];
        }

        var contributed = ImmutableArray.CreateBuilder<ContributedNamedIdentity>();
        foreach (var fact in facts)
        {
            var reference = identity(fact);
            if (!view.TryLocate(reference.Id, out var citation))
            {
                continue;
            }

            contributed.Add(new ContributedNamedIdentity(
                reference.Id,
                reference.FactType,
                name(fact),
                citation.ArtifactKey,
                citation.Ordinal));
        }

        return contributed.ToImmutable();
    }
}
