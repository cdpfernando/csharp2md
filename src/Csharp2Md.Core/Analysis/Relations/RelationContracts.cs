using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.Relations;

/// <summary>
/// One pass-one observation about a relation, carrying everything pass two needs and nothing it does
/// not. Never serialized: <c>RelationResolver</c> turns claims into facts once the run's complete
/// <c>SymbolIndex</c> exists. Mirrors <c>RawDatabaseClaim</c>'s shape
/// (Analysis/DataAccess/DataAccessContracts.cs), including its construction-time evidence guard and
/// its never-serialized contract.
/// </summary>
internal sealed record RawRelation
{
    private readonly Evidence _evidence;

    /// <summary>The relation kind observed, e.g. <c>calls</c>, <c>inherits</c>, <c>publishes</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The enclosing member's symbol id, or the document's id when there is no member.</summary>
    public required FactId OwnerId { get; init; }

    /// <summary>Enforced here, so a claim without evidence cannot exist.</summary>
    public required Evidence Evidence
    {
        get => _evidence;
        init => _evidence = RequireEvidence(value);
    }

    public required FactResolution ShapeConfidence { get; init; }

    public required RelationPartition Partition { get; init; }

    /// <summary>
    /// Exactly what <c>RelationCollector.DetailsFor</c> produces today - the identity-bearing details
    /// the resolver's fingerprint is minted from. No resolution field is ever a detail (AD-019).
    /// </summary>
    public required ImmutableArray<RelationDetail> Details { get; init; }

    /// <summary>The observed target text pass one could read, before any resolution.</summary>
    public string? TargetText { get; init; }

    /// <summary>The receiver expression's own source text, for a <c>calls</c> candidate.</summary>
    public string? ReceiverText { get; init; }

    /// <summary>The receiver's declared type name, when pass one (or pass-one refinement) could read one.</summary>
    public string? ReceiverTypeText { get; init; }

    /// <summary>The invoked member's name, for a <c>calls</c> candidate.</summary>
    public string? MemberName { get; init; }

    public int? ArgumentCount { get; init; }

    /// <summary>Simple type names, one per argument; an entry is <c>null</c> when its type could not be read.</summary>
    public ImmutableArray<string?> ArgumentTypes { get; init; } = [];

    /// <summary>The enclosing document's namespace, captured once per document.</summary>
    public string? Namespace { get; init; }

    public string? ProjectId { get; init; }

    /// <summary>The enclosing document's using directives, captured once per document.</summary>
    public ImmutableArray<string> Imports { get; init; } = [];

    /// <summary>A target already proven by the producing stage, e.g. a resolved database mapping.</summary>
    public FactId? TargetId { get; init; }

    /// <summary>The resolution method the producing stage reported, set only alongside <see cref="TargetId"/>.</summary>
    public ResolutionMethod? ProducerMethod { get; init; }

    public string? UnresolvedReason { get; init; }

    private static Evidence RequireEvidence(Evidence evidence) =>
        evidence == default
            ? throw new ArgumentException("A relation claim requires evidence.", nameof(evidence))
            : evidence;
}
