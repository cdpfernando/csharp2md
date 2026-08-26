using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Classification.Topology;

/// <summary>
/// The resolved grouping picture, built from the ledger alone. Holds no Roslyn type and constructs
/// no Domain fact (CDC-09, CDC-10, CDC-11, CDC-12, CDC-19, CDC-20).
/// </summary>
internal sealed record TopologyModel(
    ImmutableArray<ComponentGroup> Groups,
    ImmutableArray<DeploymentNode> Deployments,
    ImmutableArray<InclusionEdge> Inclusions,
    ImmutableArray<UnreachedComponent> Unreached,
    TopologyCoverage Coverage);

/// <summary>
/// One component and every project whose code it groups (CDC-09, CDC-10, CDC-11, CDC-12).
/// </summary>
internal sealed record ComponentGroup(
    string ComponentName,
    GroupingEvidence Evidence,
    ImmutableArray<ProjectId> Projects);

/// <summary>
/// Why these projects group together. Recorded for diagnostics, not published as a facet.
/// </summary>
internal enum GroupingEvidence
{
    Deployable,
    PrivateUse,
    Shared,
    Unreached,
}

/// <summary>
/// One application-derived deployment unit, named by that application's logical path (CDC-19).
/// </summary>
internal sealed record DeploymentNode(string Name, ProjectId Application);

/// <summary>
/// One component shipping inside one deployment unit, with the observations that prove the reach
/// (CDC-20, CDC-21).
/// </summary>
internal sealed record InclusionEdge(
    string ComponentName,
    string DeploymentName,
    EvidenceChain Evidence);

/// <summary>
/// A component no application reaches. Emits an unresolved <c>included-in</c>, not a confirmed
/// relation (CDC-22).
/// </summary>
internal sealed record UnreachedComponent(string ComponentName, EvidenceChain Evidence);

/// <summary>
/// Run-coverage counts for the grouping walk. The coverage diagnostic itself is CDC-56 / T35.
/// </summary>
internal sealed record TopologyCoverage(
    int ProjectsGrouped,
    int ApplicationsFound,
    int ComponentsWithoutDeployment);
