using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Classification.Configuration;

/// <summary>
/// The resolved configuration picture, built from the ledger alone. Holds no Roslyn type and
/// constructs no Domain fact (CDC-35, CDC-36, CDC-37, CDC-38, CDC-39, CDC-43).
/// </summary>
internal sealed record ConfigurationModel(
    ImmutableArray<DeclaredKey> Keys,
    ImmutableArray<ConfiguredEdge> Edges,
    ImmutableArray<TargetDecision> Targets,
    ImmutableArray<UnboundKeyRead> UnboundReads,
    ConfigurationCoverage Coverage);

/// <summary>
/// One key path declared by one configuration document, owned by one component (CDC-35).
/// </summary>
internal sealed record DeclaredKey(
    string KeyPath,
    KeyResolution Resolution,
    string? Address,
    FactReference OwningComponent,
    ObservationIdentity Evidence);

/// <summary>How a declared leaf value is known: a constant, an environment indirection, or neither.</summary>
internal enum KeyResolution
{
    Literal,
    Dynamic,
    Unknown,
}

/// <summary>
/// A fact proven to be supported by a declared key (CDC-36, CDC-37, CDC-38, CDC-39).
/// </summary>
internal sealed record ConfiguredEdge(
    FactReference Source,
    string KeyPath,
    EvidenceChain Evidence);

/// <summary>What to do with one 5A candidate <c>targets</c> link (CDC-43, CDC-44, CDC-45).</summary>
internal sealed record TargetDecision(
    CandidateLink Candidate,
    TargetOutcome Outcome,
    EvidenceChain Evidence);

/// <summary>Promote a candidate, keep it with a frontier, or leave it untouched.</summary>
internal enum TargetOutcome
{
    Promote,
    Frontier,
    Leave,
}

/// <summary>
/// A symbol-owned configuration read whose key is declared nowhere (CDC-42).
/// </summary>
internal sealed record UnboundKeyRead(FactReference Symbol, ObservationIdentity Evidence);

/// <summary>
/// Run-coverage counts for the configuration walk. The coverage diagnostic itself is CDC-57 / T36.
/// </summary>
internal sealed record ConfigurationCoverage(
    int KeysDeclared,
    int KeysBound,
    int KeysReadButNotDeclared);
