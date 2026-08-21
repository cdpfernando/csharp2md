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
    Data,
}

/// <summary>
/// The route by which a relation's target was proven. Distinct from <see cref="FactResolution"/>: that
/// answers "how proven is this fact", aggregated across every fact family; this answers "by what route did
/// we reach this target", and is meaningful only on a <see cref="RelationFact"/>. See AD-019.
/// </summary>
public enum ResolutionMethod
{
    Exact,
    Candidate,
    Syntactic,
    Configured,
    Convention,
    Dynamic,
    Heuristic,
    Unresolved,
}

/// <summary>
/// Wire strings for <see cref="ResolutionMethod"/>, written out in both directions rather than derived by
/// lowercasing, so a member added later cannot acquire (or accept) a wire name by accident.
/// </summary>
public static class ResolutionMethodWire
{
    public static string Name(ResolutionMethod method) => method switch
    {
        ResolutionMethod.Exact => "exact",
        ResolutionMethod.Candidate => "candidate",
        ResolutionMethod.Syntactic => "syntactic",
        ResolutionMethod.Configured => "configured",
        ResolutionMethod.Convention => "convention",
        ResolutionMethod.Dynamic => "dynamic",
        ResolutionMethod.Heuristic => "heuristic",
        ResolutionMethod.Unresolved => "unresolved",
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported resolution method."),
    };

    public static ResolutionMethod Parse(string wireValue) => wireValue switch
    {
        "exact" => ResolutionMethod.Exact,
        "candidate" => ResolutionMethod.Candidate,
        "syntactic" => ResolutionMethod.Syntactic,
        "configured" => ResolutionMethod.Configured,
        "convention" => ResolutionMethod.Convention,
        "dynamic" => ResolutionMethod.Dynamic,
        "heuristic" => ResolutionMethod.Heuristic,
        "unresolved" => ResolutionMethod.Unresolved,
        _ => throw new ArgumentOutOfRangeException(nameof(wireValue), wireValue, "Unsupported resolution method wire value."),
    };
}

public sealed record RelationFact(
    FactHeader Header,
    RelationFactId RelationId,
    FactId SourceId,
    FactId? TargetId,
    RelationPartition Partition,
    string RelationKind,
    string? UnresolvedReason,
    ImmutableArray<RelationDetail> Details = default,
    // SPEC_DEVIATION: design.md's target shape (relation-resolver/design.md:197-207) declares Method with
    // no default, required on every construction, once RelationResolver is the sole writer of RelationFact
    // (AD-018, Phase 4-5). Phase 1 (T1-T4) only lands the wire contract; the pre-existing producers that
    // still call this constructor directly - DatabaseFragmentBuilder, CompileTimeReferenceDetector and the
    // orphaned Detection/* detectors - are out of this feature's task list (grep confirms no task touches
    // them) and are not migrated until later phases. Exact is a safe transitional default: every relation
    // those producers construct with a non-null TargetId today is genuinely exact-resolved (a configured
    // database mapping or a same-solution project reference), so C2M-FV-008 (T3) never rejects their
    // output. Remove this default once every producer sets Method explicitly.
    ResolutionMethod Method = ResolutionMethod.Exact,
    ImmutableArray<FactId> Candidates = default) : IFact
{
    // Data is runtime: a persistence access names a resource that exists only when the program runs,
    // exactly like an HTTP or gRPC edge, and unlike a reference the compiler can resolve.
    public bool IsRuntime => Partition is
        RelationPartition.DependencyInjection or
        RelationPartition.Http or
        RelationPartition.Grpc or
        RelationPartition.Events or
        RelationPartition.Data;
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
