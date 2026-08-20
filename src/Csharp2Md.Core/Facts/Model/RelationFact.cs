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
    Structural,
}

public sealed record RelationFact(
    FactHeader Header,
    RelationFactId RelationId,
    FactId SourceId,
    FactId? TargetId,
    RelationPartition Partition,
    string RelationKind,
    string? UnresolvedReason,
    ImmutableArray<RelationDetail> Details = default) : IFact
{
    public bool IsRuntime => Partition is
        RelationPartition.DependencyInjection or
        RelationPartition.Http or
        RelationPartition.Grpc or
        RelationPartition.Events;
}

public readonly record struct RelationDetail(string Key, string Value) : IComparable<RelationDetail>
{
    public int CompareTo(RelationDetail other)
    {
        var keyComparison = StringComparer.Ordinal.Compare(Key, other.Key);
        return keyComparison != 0
            ? keyComparison
            : StringComparer.Ordinal.Compare(Value, other.Value);
    }
}
