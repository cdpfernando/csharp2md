namespace Acme.Shared.Contracts.Diagnostics;

/// <summary>
/// RELR-04/T29 (spec.md's second P1 Independent Test): the same simple name "AuditRecorder" is also
/// declared in <c>Acme.Orders.Diagnostics</c>. Both are real, independently-compilable types - unlike
/// an intentionally-broken reference, this scenario relies only on the resolver's symbol index
/// containing two same-named entries, which it does regardless of which one any particular project's
/// own compiler happens to bind to. <c>AmbiguousReferenceProbe</c> (Acme.Payments) references this
/// copy by name and compiles cleanly against it; the tie is still genuine because
/// <c>Acme.Orders.Diagnostics.AuditRecorder</c> is indexed too, and "creates" claims are never
/// semantically refined (RelationCollector.RefineClaims only touches inherits/implements/publishes), so
/// the resolver only ever sees the plain observed name "AuditRecorder", never which one Acme.Payments's
/// own compilation resolved.
/// </summary>
public sealed class AuditRecorder;
