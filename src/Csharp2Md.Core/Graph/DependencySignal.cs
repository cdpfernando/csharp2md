namespace Csharp2Md.Core.Graph;

/// <summary>
/// Emitted by detectors in Stage 2. Messaging signals are half-edges: they carry a
/// <see cref="Role"/> and a topic name, and are correlated into a real edge by the
/// GraphBuilder in Stage 3 (P2-14).
/// </summary>
public sealed record DependencySignal(
    ServiceName SourceService,
    ServiceName? TargetService,
    string RawTarget,
    DependencyKind Kind,
    CommunicationType Communication,
    ResolutionKind Resolution,
    MessagingRole? Role,
    SourceLocation Location);
