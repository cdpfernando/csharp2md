using Csharp2Md.Core.Facts.Identity;

namespace Csharp2Md.Core.Facts.Model;

public enum RelationPartition
{
    CompileTime,
    Inheritance,
    DependencyInjection,
    Http,
    Grpc,
    Events,
}

public sealed record RelationFact(
    FactHeader Header,
    RelationFactId RelationId,
    FactId SourceId,
    FactId? TargetId,
    RelationPartition Partition,
    string RelationKind,
    string? UnresolvedReason) : IFact
{
    public bool IsRuntime => Partition is
        RelationPartition.DependencyInjection or
        RelationPartition.Http or
        RelationPartition.Grpc or
        RelationPartition.Events;
}
