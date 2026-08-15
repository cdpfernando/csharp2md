namespace Csharp2Md.Core.Graph;

/// <summary>Emitted by the GraphBuilder in Stage 3, after messaging correlation.</summary>
public sealed record DependencyEdge(
    ServiceName Source,
    ServiceName Target,
    CommunicationType Communication,
    ResolutionKind Resolution,
    IReadOnlyList<SourceLocation> Evidence);

public sealed record DependencyGraph(IReadOnlyList<DependencyEdge> Edges);
