using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Classification.Persistence;

/// <summary>
/// The resolved persistence picture, built from the ledger alone and consumed by the emitter. These
/// records hold no Roslyn type and construct no Domain fact, so the correlation rules that fill them
/// are unit-testable against a hand-built ledger and the emitter stays one ordered walk (PK-31).
/// </summary>
internal sealed record PersistenceModel(
    ImmutableArray<StoreNode> Stores,
    ImmutableArray<UnresolvedNode> Unresolved,
    CoverageCounts Coverage);

/// <summary>One <c>DbContext</c>-derived type and everything reached through it.</summary>
internal sealed record StoreNode(
    string ContextTypeFqn,
    DataStoreTechnology Technology,
    string Name,
    ImmutableArray<ObjectNode> Objects);

/// <summary>
/// One physical table, view or procedure. <see cref="EntityTypeFqn"/> is null for a SQL-derived
/// object, which is never merged with an entity-set object however similar the names (PK-20).
/// </summary>
internal sealed record ObjectNode(
    string? EntityTypeFqn,
    DataObjectForm Form,
    string SchemaName,
    string TableName,
    MappingStateKind MappingState,
    FactReference? ClrSymbol,
    ImmutableArray<FieldNode> Fields,
    ImmutableArray<OperationNode> Operations,
    EvidenceChain Evidence);

/// <summary>
/// One column. <see cref="PropertyName"/> is null for a column seen only in a SQL statement;
/// <see cref="FieldName"/> is the physical name when a <c>HasColumnName</c> proves one and the CLR
/// name otherwise (PK-25, PK-26).
/// </summary>
internal sealed record FieldNode(
    string? PropertyName,
    string FieldName,
    MappingStateKind MappingState,
    FactReference? ClrSymbol,
    EvidenceChain Evidence);

/// <summary>
/// One <c>(object, operation kind)</c> pair. <see cref="Callables"/> is a collection because
/// <c>DataOperation</c> identity carries no symbol: two callables performing the same operation on
/// the same object share one fact reached by one <c>accesses-data</c> relation each (PK-38).
/// </summary>
internal sealed record OperationNode(
    DataOperationKind Kind,
    ClassifierIdentity Classifier,
    ImmutableArray<FactReference> Callables,
    ImmutableArray<string> FieldNames,
    EvidenceChain Evidence);

/// <summary>Something recognized but not resolved, kept rather than guessed (PK-39 … PK-41).</summary>
internal sealed record UnresolvedNode(
    RelationKind Kind,
    FactReference Source,
    UnresolvedCause Cause,
    EvidenceChain Available);

/// <summary>
/// The run-coverage numerator, denominator and unresolved owners (PK-51, PK-52). No percentage and
/// no verdict: the certification envelope is built elsewhere (PK-53).
/// </summary>
internal readonly record struct CoverageCounts(
    int RecognizedOccurrences,
    int ResolvedOccurrences,
    ImmutableArray<string> UnresolvedOwnerIds);
