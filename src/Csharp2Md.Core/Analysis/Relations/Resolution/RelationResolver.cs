using System.Text.RegularExpressions;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.Relations.Resolution;

/// <summary>
/// Everything one run of the resolver produced: every claim turned into exactly one <see cref="RelationFact"/>
/// (RELR-12), plus every diagnostic a strategy or the resolver itself raised along the way.
/// </summary>
internal sealed record RelationResolution(
    ImmutableArray<RelationFact> Facts,
    ImmutableArray<AnalysisDiagnostic> Diagnostics)
{
    public static RelationResolution Empty { get; } = new([], []);
}

/// <summary>
/// Pass two's only writer of <see cref="RelationFact"/> (RELR-01, AD-018). Walks the registered strategy
/// chain over every buffered claim, in one fixed declared order, and stops at the first strategy that
/// handles a claim (RELR-09). Identity is minted from the claim alone - never from the resolution outcome
/// (RELR-22) - which is why minting happens before a strategy ever runs: a strategy that throws, or one
/// that resolves nothing, still yields a relation with a stable id.
/// </summary>
internal sealed class RelationResolver
{
    private const string EngineId = "csharp2md.relations";
    private const string EngineVersion = "1";
    private const string StrategyFailureCode = "C2M-RELR-006";
    private const DiagnosticStage Stage = DiagnosticStage.Detector;

    private static readonly FactProvenance Provenance = new(EngineId, EngineVersion);
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// The strategies every run applies, in the fixed declared order design.md's strategy-chain table
    /// specifies. Extended in place as T17-T20 add the resolving strategies before <see cref="UnresolvedStrategy"/>,
    /// which must always stay last: it is the terminal strategy that makes RELR-12 structural.
    /// </summary>
    public static ImmutableArray<IRelationResolutionStrategy> RegisteredStrategies { get; } =
    [
        new ExistingTargetStrategy(),
        new DatabaseRelationStrategy(),
        new ReceiverTypeStrategy(),
        new SymbolIndexStrategy(),
        new UnresolvedStrategy(),
    ];

    public static RelationResolver Default { get; } = new(RegisteredStrategies);

    private readonly ImmutableArray<IRelationResolutionStrategy> _strategies;

    public RelationResolver(ImmutableArray<IRelationResolutionStrategy> strategies)
    {
        if (strategies.IsDefaultOrEmpty)
        {
            throw new ArgumentException("At least one strategy is required.", nameof(strategies));
        }

        _strategies = strategies;
    }

    /// <summary>
    /// SPEC_DEVIATION: design.md's Components section lists this method as
    /// <c>Resolve(RelationClaimSnapshot, ISymbolIndex, CancellationToken)</c>. Reason: <see cref="RelationResolutionContext"/>
    /// (T15) requires <c>KnownFactIds</c> - "the identities the run actually produced" - so RELR-02/RELR-03
    /// ("present in the run's fact set") can be judged correctly. <see cref="ISymbolIndex"/> alone only
    /// covers <c>SymbolFact</c>; a database-resolved target references a <c>DatabaseObjectFactId</c> or
    /// <c>DatabaseColumnFactId</c> instead. The caller (T23) is what can see both catalogues, so it supplies
    /// the union explicitly rather than this method rebuilding it from an index that cannot express it.
    /// </summary>
    public RelationResolution Resolve(
        RelationClaimSnapshot claims,
        ISymbolIndex index,
        IReadOnlySet<FactId> knownFactIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(knownFactIds);

        if (claims.Claims.IsDefaultOrEmpty)
        {
            return RelationResolution.Empty;
        }

        var facts = ImmutableArray.CreateBuilder<RelationFact>(claims.Claims.Length);
        var diagnostics = ImmutableArray.CreateBuilder<AnalysisDiagnostic>();
        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var claim in claims.Claims)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fingerprint = ClaimFingerprint(claim.Details);
            var ordinalKey = string.Join('\0', claim.OwnerId.Value, claim.Kind, fingerprint);
            var ordinal = NextOrdinal(ordinals, ordinalKey);
            var id = RelationFactId.Create(claim.OwnerId, claim.Kind, fingerprint, ordinal);
            var scopeId = id.ToFactId();

            var outcome = ResolveOne(claim, index, knownFactIds, scopeId, diagnostics, cancellationToken);

            var evidence = outcome.AddedEvidence.IsDefaultOrEmpty
                ? [claim.Evidence]
                : (ImmutableArray<Evidence>)[claim.Evidence, .. outcome.AddedEvidence];

            var header = FactHeader.Create(
                scopeId, FactKind.Relation, ResolutionFor(outcome.Method), [Provenance], evidence);

            facts.Add(new RelationFact(
                header,
                id,
                claim.OwnerId,
                outcome.TargetId,
                claim.Partition,
                claim.Kind,
                outcome.UnresolvedReason,
                claim.Details,
                outcome.Method,
                outcome.Candidates));

            if (outcome.Diagnostic is { } description)
            {
                diagnostics.Add(AnalysisDiagnostic.Create(
                    description.Code, description.Severity, Stage, scopeId, description.Message, description.Data));
            }
        }

        return new RelationResolution(
            facts.OrderBy(static fact => fact.RelationId.Value, StringComparer.Ordinal).ToImmutableArray(),
            diagnostics.Order().ToImmutableArray());
    }

    /// <summary>
    /// Walks the chain for one claim. A strategy that throws has its outcome discarded, contributes
    /// <c>C2M-RELR-006</c>, and the walk continues with the next strategy (RELR-11); the walk itself
    /// never throws for that reason. <see cref="UnresolvedStrategy"/> is registered last and always
    /// handles, so every claim yields an outcome - the post-loop throw below is an unreachable
    /// defensive invariant, not an assumed contract.
    /// </summary>
    private RelationResolutionOutcome ResolveOne(
        RawRelation claim,
        ISymbolIndex index,
        IReadOnlySet<FactId> knownFactIds,
        FactId scopeId,
        ImmutableArray<AnalysisDiagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        var context = new RelationResolutionContext(claim, index, knownFactIds);
        foreach (var strategy in _strategies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RelationResolutionOutcome outcome;
            try
            {
                outcome = strategy.TryResolve(context);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostics.Add(FailureDiagnostic(scopeId, claim, strategy, exception));
                continue;
            }

            if (outcome.Handled)
            {
                return outcome;
            }
        }

        throw new InvalidOperationException(
            "No strategy handled the relation. The terminal strategy must always handle.");
    }

    private static AnalysisDiagnostic FailureDiagnostic(
        FactId scopeId, RawRelation claim, IRelationResolutionStrategy strategy, Exception exception) =>
        AnalysisDiagnostic.Create(
            StrategyFailureCode,
            DiagnosticSeverity.Warning,
            Stage,
            scopeId,
            "Relation resolution strategy failed; its outcome was discarded and the chain continued.",
            [
                new DiagnosticData("strategy", strategy.GetType().Name),
                new DiagnosticData("relation_kind", claim.Kind),
                new DiagnosticData("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
            ]);

    /// <summary>
    /// The header-level proof strength a resolved <see cref="ResolutionMethod"/> implies. Deliberately
    /// independent of <see cref="RawRelation.ShapeConfidence"/>: shape confidence answers "is this really
    /// an <c>inherits</c> relation", not "was its target found", and merging the two would let a
    /// strongly-shaped but unresolved relation report a header resolution stronger than its own target
    /// proof (e.g. <c>Exact</c> shape plus <c>Unresolved</c> target must never read as <c>Exact</c> overall).
    /// </summary>
    private static FactResolution ResolutionFor(ResolutionMethod method) => method switch
    {
        ResolutionMethod.Exact => FactResolution.Exact,
        ResolutionMethod.Candidate => FactResolution.Candidate,
        ResolutionMethod.Syntactic => FactResolution.Syntactic,
        ResolutionMethod.Configured => FactResolution.Exact,
        ResolutionMethod.Convention => FactResolution.Heuristic,
        ResolutionMethod.Dynamic => FactResolution.Unresolved,
        ResolutionMethod.Heuristic => FactResolution.Heuristic,
        ResolutionMethod.Unresolved => FactResolution.Unresolved,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported resolution method."),
    };

    private static int NextOrdinal(Dictionary<string, int> ordinals, string key)
    {
        var ordinal = ordinals.GetValueOrDefault(key) + 1;
        ordinals[key] = ordinal;
        return ordinal;
    }

    /// <summary>
    /// Ported verbatim from the pre-claim <c>RelationCollector.ClaimFor</c>/<c>Canonicalize</c> (T21's
    /// named ordinal risk): the fingerprint a relation's identity is minted from must be byte-for-byte
    /// what the pre-refactor collector produced, or every existing <c>relation_id</c> silently renumbers.
    /// </summary>
    private static string ClaimFingerprint(ImmutableArray<RelationDetail> details) =>
        Canonicalize(string.Join('|', details.Select(static detail => $"{detail.Key}={detail.Value}")));

    private static string Canonicalize(string value) => WhitespaceRun.Replace(value, " ").Trim();
}

/// <summary>
/// The terminal strategy (RELR-12, RELR-15, RELR-16): always handles, so the chain can never fall through
/// and drop a relation. Reached only by a claim none of the resolving strategies claimed - most commonly a
/// <c>calls</c> claim with no captured receiver and an empty <c>target_text</c>, or any claim whose observed
/// target could not be found by whichever lookup a preceding strategy attempted.
/// </summary>
internal sealed class UnresolvedStrategy : IRelationResolutionStrategy
{
    private const string NoCandidateCode = "C2M-RELR-001";

    public RelationResolutionOutcome TryResolve(RelationResolutionContext context)
    {
        var claim = context.Relation;
        var observed = string.IsNullOrWhiteSpace(claim.TargetText) ? null : claim.TargetText;
        var reason = observed is { } text
            ? $"No candidate found for '{text}'."
            : $"No target text was observed for the '{claim.Kind}' relation.";

        return new RelationResolutionOutcome(
            Handled: true,
            Method: ResolutionMethod.Unresolved,
            UnresolvedReason: reason,
            Diagnostic: new RelationDiagnostic(
                NoCandidateCode,
                DiagnosticSeverity.Information,
                $"No candidate was found for the observed target text of a '{claim.Kind}' relation.",
                observed is { } value
                    ? [new DiagnosticData("relation_kind", claim.Kind), new DiagnosticData("target_text", value)]
                    : [new DiagnosticData("relation_kind", claim.Kind)]));
    }
}
