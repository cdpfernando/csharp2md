using System.Text.Json.Serialization;

namespace Csharp2Md.Core.Facts.Serialization;

public sealed record FactProvenanceJson(
    [property: JsonPropertyOrder(0)] string EngineId,
    [property: JsonPropertyOrder(1)] string EngineVersion,
    [property: JsonPropertyOrder(2)] string? DetectorId,
    [property: JsonPropertyOrder(3)] string? DetectorVersion);

public sealed record EvidenceJson(
    [property: JsonPropertyOrder(0)] string DocumentId,
    [property: JsonPropertyOrder(1)] string RelativePath,
    [property: JsonPropertyOrder(2)] int StartLine,
    [property: JsonPropertyOrder(3)] int StartColumn,
    [property: JsonPropertyOrder(4)] int EndLine,
    [property: JsonPropertyOrder(5)] int EndColumn);

public sealed record FactHeaderJson(
    [property: JsonPropertyOrder(0)] string Id,
    [property: JsonPropertyOrder(1)] string Kind,
    [property: JsonPropertyOrder(2)] string Resolution,
    [property: JsonPropertyOrder(3)] ImmutableArray<FactProvenanceJson> Provenance,
    [property: JsonPropertyOrder(4)] ImmutableArray<EvidenceJson> Evidence,
    [property: JsonPropertyOrder(5)] ImmutableArray<string> DiagnosticIds);

public sealed record DiagnosticDataJson(
    [property: JsonPropertyOrder(0)] string Key,
    [property: JsonPropertyOrder(1)] string Value);

public sealed record AnalysisDiagnosticJson(
    [property: JsonPropertyOrder(0)] string Id,
    [property: JsonPropertyOrder(1)] string Code,
    [property: JsonPropertyOrder(2)] string Severity,
    [property: JsonPropertyOrder(3)] string Stage,
    [property: JsonPropertyOrder(4)] string ScopeId,
    [property: JsonPropertyOrder(5)] string Message,
    [property: JsonPropertyOrder(6)] ImmutableArray<DiagnosticDataJson> Data,
    [property: JsonPropertyOrder(7)] ImmutableArray<EvidenceJson> Evidence,
    [property: JsonPropertyOrder(8)] string? ExtensionId);

public sealed record SolutionFactJson(
    [property: JsonPropertyOrder(0)] FactHeaderJson Header,
    [property: JsonPropertyOrder(1)] string Name,
    [property: JsonPropertyOrder(2)] ImmutableArray<string> ProjectIds);

public sealed record ProjectFactJson(
    [property: JsonPropertyOrder(0)] FactHeaderJson Header,
    [property: JsonPropertyOrder(1)] string ProjectId,
    [property: JsonPropertyOrder(2)] string Name,
    [property: JsonPropertyOrder(3)] string RelativePath,
    [property: JsonPropertyOrder(4)] ImmutableArray<string> TargetIds,
    [property: JsonPropertyOrder(5)] ImmutableArray<string> DocumentIds,
    [property: JsonPropertyOrder(6)] ProjectEvaluationJson? Evaluation = null);

public sealed record ProjectEvaluationJson(
    [property: JsonPropertyOrder(0)] string DeclaredSdk,
    [property: JsonPropertyOrder(1)] ImmutableArray<string> EvaluatedImports,
    [property: JsonPropertyOrder(2)] ImmutableArray<string> TargetFrameworks,
    [property: JsonPropertyOrder(3)] string RequestedAnalysis,
    [property: JsonPropertyOrder(4)] string EffectiveResolution,
    [property: JsonPropertyOrder(5)] bool RestorePerformed,
    [property: JsonPropertyOrder(6)] string Isolation);

public sealed record TargetFactJson(
    [property: JsonPropertyOrder(0)] FactHeaderJson Header,
    [property: JsonPropertyOrder(1)] string TargetId,
    [property: JsonPropertyOrder(2)] string ProjectId,
    [property: JsonPropertyOrder(3)] string TargetFramework,
    [property: JsonPropertyOrder(4)] TargetEvaluationJson? Evaluation = null);

public sealed record TargetEvaluationJson(
    [property: JsonPropertyOrder(0)] string OutputType,
    [property: JsonPropertyOrder(1)] string AssemblyName,
    [property: JsonPropertyOrder(2)] string RootNamespace,
    [property: JsonPropertyOrder(3)] ImmutableArray<string> CompileItems,
    [property: JsonPropertyOrder(4)] ImmutableArray<string> ProjectReferences,
    [property: JsonPropertyOrder(5)] ImmutableArray<string> PackageReferences,
    [property: JsonPropertyOrder(6)] ImmutableArray<string> References,
    [property: JsonPropertyOrder(7)] ImmutableArray<string> Constants,
    [property: JsonPropertyOrder(8)] string LanguageVersion,
    [property: JsonPropertyOrder(9)] string NullableMode,
    [property: JsonPropertyOrder(10)] ImmutableArray<string> CompiledExtensions);

public sealed record DocumentFactJson(
    [property: JsonPropertyOrder(0)] FactHeaderJson Header,
    [property: JsonPropertyOrder(1)] string DocumentId,
    [property: JsonPropertyOrder(2)] string ProjectId,
    [property: JsonPropertyOrder(3)] string RelativePath,
    [property: JsonPropertyOrder(4)] ImmutableArray<string> SectionIds,
    [property: JsonPropertyOrder(5)] ImmutableArray<string> SymbolIds);

public sealed record SourceSectionFactJson(
    [property: JsonPropertyOrder(0)] FactHeaderJson Header,
    [property: JsonPropertyOrder(1)] string DocumentId,
    [property: JsonPropertyOrder(2)] string SectionKind,
    [property: JsonPropertyOrder(3)] int OccurrenceOrdinal,
    [property: JsonPropertyOrder(4)] int StartOffset,
    [property: JsonPropertyOrder(5)] int Length,
    [property: JsonPropertyOrder(6)] string Source);

public sealed record SymbolFactJson(
    [property: JsonPropertyOrder(0)] FactHeaderJson Header,
    [property: JsonPropertyOrder(1)] string SymbolId,
    [property: JsonPropertyOrder(2)] string DocumentId,
    [property: JsonPropertyOrder(3)] string SymbolKind,
    [property: JsonPropertyOrder(4)] bool ContainsErrorSymbol,
    [property: JsonPropertyOrder(5)] ImmutableArray<string> BaseAndInterfaceIds,
    [property: JsonPropertyOrder(6)] ImmutableArray<string> Attributes,
    [property: JsonPropertyOrder(7)] ImmutableArray<string> RelevantTypeReferences,
    [property: JsonPropertyOrder(8)] SymbolSemanticJson? Semantics,
    [property: JsonPropertyOrder(9)] string Name,
    [property: JsonPropertyOrder(10)] string FullyQualifiedName,
    [property: JsonPropertyOrder(11)] string? Namespace,
    [property: JsonPropertyOrder(12)] string? ContainingType,
    [property: JsonPropertyOrder(13)] string? ContainingSymbolId,
    [property: JsonPropertyOrder(14)] string Signature,
    [property: JsonPropertyOrder(15)] int Arity,
    [property: JsonPropertyOrder(16)] ImmutableArray<string> ParameterTypes);

public sealed record SymbolSemanticJson(
    [property: JsonPropertyOrder(0)] ImmutableArray<string> ImplementedMemberIds,
    [property: JsonPropertyOrder(1)] string? OverriddenMemberId);

public sealed record ComponentFactJson(
    [property: JsonPropertyOrder(0)] FactHeaderJson Header,
    [property: JsonPropertyOrder(1)] string ComponentId,
    [property: JsonPropertyOrder(2)] string ComponentKind,
    [property: JsonPropertyOrder(3)] ImmutableArray<string> ProjectIds);

public sealed record RelationFactJson(
    [property: JsonPropertyOrder(0)] FactHeaderJson Header,
    [property: JsonPropertyOrder(1)] string RelationId,
    [property: JsonPropertyOrder(2)] string SourceId,
    [property: JsonPropertyOrder(3)] string? TargetId,
    [property: JsonPropertyOrder(4)] string Partition,
    [property: JsonPropertyOrder(5)] string RelationKind,
    [property: JsonPropertyOrder(6)] string? UnresolvedReason,
    [property: JsonPropertyOrder(7)] ImmutableArray<RelationDetailJson>? Details = null,
    // Non-nullable and required as of schema v5 (T4). Defaults to "exact" rather than "unresolved" so a
    // relation whose mapper does not yet set this field explicitly (FactStore.MapRelation does not read
    // RelationFact.Method until a later phase wires the resolver) never contradicts its own target_id: every
    // relation reaching this mapper today with a non-null target_id is genuinely exact-resolved (see
    // RelationFact.Method's matching SPEC_DEVIATION note in Facts/Model/RelationFact.cs).
    [property: JsonPropertyOrder(8)] string ResolutionMethod = "exact",
    [property: JsonPropertyOrder(9)] ImmutableArray<string>? Candidates = null);

public sealed record RelationDetailJson(
    [property: JsonPropertyOrder(0)] string Key,
    [property: JsonPropertyOrder(1)] string Value);

public sealed record DatabaseObjectFactJson(
    [property: JsonPropertyOrder(0)] FactHeaderJson Header,
    [property: JsonPropertyOrder(1)] string ObjectId,
    [property: JsonPropertyOrder(2)] string ConnectionName,
    [property: JsonPropertyOrder(3)] string Kind,
    [property: JsonPropertyOrder(4)] string Name);

public sealed record DatabaseColumnFactJson(
    [property: JsonPropertyOrder(0)] FactHeaderJson Header,
    [property: JsonPropertyOrder(1)] string ColumnId,
    [property: JsonPropertyOrder(2)] string ObjectId,
    [property: JsonPropertyOrder(3)] string Name);

public sealed record CoverageFactJson(
    [property: JsonPropertyOrder(0)] string ScopeId,
    [property: JsonPropertyOrder(1)] string FactLevel,
    [property: JsonPropertyOrder(2)] string? DetectorId,
    [property: JsonPropertyOrder(3)] string Applicability,
    [property: JsonPropertyOrder(4)] string Attempt,
    [property: JsonPropertyOrder(5)] string Resolution,
    [property: JsonPropertyOrder(6)] ImmutableArray<string> DiagnosticIds);

public sealed record FactualJsonDocument(
    [property: JsonPropertyOrder(0)] int SchemaVersion,
    [property: JsonPropertyOrder(1)] ImmutableArray<SolutionFactJson> Solutions,
    [property: JsonPropertyOrder(2)] ImmutableArray<ProjectFactJson> Projects,
    [property: JsonPropertyOrder(3)] ImmutableArray<TargetFactJson> Targets,
    [property: JsonPropertyOrder(4)] ImmutableArray<DocumentFactJson> Documents,
    [property: JsonPropertyOrder(5)] ImmutableArray<SourceSectionFactJson> SourceSections,
    [property: JsonPropertyOrder(6)] ImmutableArray<SymbolFactJson> Symbols,
    [property: JsonPropertyOrder(7)] ImmutableArray<ComponentFactJson> Components,
    [property: JsonPropertyOrder(8)] ImmutableArray<RelationFactJson> Relations,
    [property: JsonPropertyOrder(9)] ImmutableArray<DatabaseObjectFactJson> DatabaseObjects,
    [property: JsonPropertyOrder(10)] ImmutableArray<DatabaseColumnFactJson> DatabaseColumns,
    [property: JsonPropertyOrder(11)] ImmutableArray<AnalysisDiagnosticJson> Diagnostics,
    [property: JsonPropertyOrder(12)] ImmutableArray<CoverageFactJson> Coverage);
