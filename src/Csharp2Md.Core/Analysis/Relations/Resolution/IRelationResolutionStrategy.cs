using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.Relations.Resolution;

/// <summary>
/// Everything a strategy is allowed to see while resolving one claim: the claim itself, the run's
/// complete symbol index, and the identities the run actually produced.
/// </summary>
/// <remarks>
/// There is deliberately no <c>SemanticModel</c> and no <c>SyntaxNode</c> here, and adding one is not
/// an option: both are discarded with the compilation at the end of pass one, and the resolver runs in
/// pass two, once every document has been analysed (design.md, "RelationResolutionContext"). What the
/// semantic pass learned reaches a strategy as claim fields - <see cref="RawRelation.ReceiverTypeText"/>
/// and <see cref="RawRelation.ArgumentTypes"/> - which is why <c>RelationCollector.RefineClaims</c>
/// exists. The context carries nothing that would let a strategy read a file, run a process or query a
/// network, so resolving the same claim twice always yields the same outcome (RELR-21).
/// </remarks>
internal sealed record RelationResolutionContext(
    RawRelation Relation,
    ISymbolIndex SymbolIndex,
    IReadOnlySet<FactId> KnownFactIds);

/// <summary>
/// What a strategy wants said about the claim it just handled, before the relation it belongs to has an
/// identity.
/// </summary>
/// <remarks>
/// SPEC_DEVIATION: design.md's <c>RelationResolutionOutcome</c> carries a built
/// <see cref="AnalysisDiagnostic"/>. Reason: RELR-37 requires every resolver diagnostic to be scoped to
/// its relation's fact id, and <see cref="AnalysisDiagnostic.Create"/> derives the diagnostic's own
/// identity from that scope - so the diagnostic cannot be built before the id exists. RELR-22 puts
/// minting in the resolver, not in a strategy, so the strategy describes the diagnostic and
/// <c>RelationResolver</c> builds it against the minted relation id.
/// </remarks>
internal sealed record RelationDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    ImmutableArray<DiagnosticData> Data = default);

/// <summary>
/// One strategy's answer for one claim. <see cref="None"/> means "declined - not my kind of claim", and
/// is distinct from an outcome with <see cref="Handled"/> set and no <see cref="TargetId"/>, which means
/// "mine, and the honest answer is that it has no target" (RELR-10, RELR-12).
/// </summary>
internal sealed record RelationResolutionOutcome(
    bool Handled,
    FactId? TargetId = null,
    ResolutionMethod Method = ResolutionMethod.Unresolved,
    ImmutableArray<FactId> Candidates = default,
    ImmutableArray<Evidence> AddedEvidence = default,
    string? UnresolvedReason = null,
    RelationDiagnostic? Diagnostic = null)
{
    /// <summary>The declined outcome: this strategy does not own this claim, so the chain continues.</summary>
    public static RelationResolutionOutcome None { get; } = new(Handled: false);
}

/// <summary>
/// The extension seam for relation resolution: one pure attempt at one claim, with no I/O and no engine
/// access, mirroring <c>IDataAccessAnalyzer</c>'s single-method shape. The resolver applies the
/// registered strategies in one fixed declared order and stops at the first that handles a claim
/// (RELR-09); a strategy that does not own a claim's kind returns <see cref="RelationResolutionOutcome.None"/>
/// and changes nothing (RELR-10).
/// </summary>
internal interface IRelationResolutionStrategy
{
    RelationResolutionOutcome TryResolve(RelationResolutionContext context);
}
