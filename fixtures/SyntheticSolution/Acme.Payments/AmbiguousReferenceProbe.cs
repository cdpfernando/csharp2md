namespace Acme.Payments;

/// <summary>
/// RELR-04/T29 (spec.md's second P1 Independent Test): a third project (distinct from both
/// <c>Acme.Orders.Diagnostics.AuditRecorder</c> and <c>Acme.Shared.Contracts.Diagnostics.AuditRecorder</c>,
/// the two projects that declare the tied name) references "AuditRecorder" by its simple name.
/// Deliberately fully-qualified rather than imported via <c>using</c>: a <c>using</c> directive would
/// itself become a resolution hint (SymbolIndex.PriorityTier's "imported namespace" tier) that favours
/// Acme.Shared.Contracts's copy over Acme.Orders's, collapsing the intended two-declaration tie into
/// the unrelated semantic/syntactic double-registration artifact for the one declaration alone. The
/// syntax-only "creates" candidate still observes the bare simple name "AuditRecorder" regardless of
/// qualification, so both declarations remain equally-ranked candidates for SymbolIndexStrategy.
/// </summary>
public sealed class AmbiguousReferenceProbe
{
    public object Create() => new Acme.Shared.Contracts.Diagnostics.AuditRecorder();
}
