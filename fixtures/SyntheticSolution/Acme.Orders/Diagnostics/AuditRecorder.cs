namespace Acme.Orders.Diagnostics;

/// <summary>
/// RELR-04/T29 (spec.md's second P1 Independent Test): the same simple name "AuditRecorder" is also
/// declared in <c>Acme.Payments.Diagnostics</c>. A bare, unqualified reference to "AuditRecorder" from
/// a third project ties between the two, which <c>SymbolIndexStrategy</c> must report as an ambiguous
/// candidate rather than picking one.
/// </summary>
public sealed class AuditRecorder;
