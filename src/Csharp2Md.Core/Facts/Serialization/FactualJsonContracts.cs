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
    [property: JsonPropertyOrder(5)] ImmutableArray<string> DocumentIds);

public sealed record TargetFactJson(
    [property: JsonPropertyOrder(0)] FactHeaderJson Header,
    [property: JsonPropertyOrder(1)] string TargetId,
    [property: JsonPropertyOrder(2)] string ProjectId,
    [property: JsonPropertyOrder(3)] string TargetFramework);

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
    [property: JsonPropertyOrder(7)] ImmutableArray<string> RelevantTypeReferences);

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
    [property: JsonPropertyOrder(6)] string? UnresolvedReason);

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
    [property: JsonPropertyOrder(9)] ImmutableArray<AnalysisDiagnosticJson> Diagnostics,
    [property: JsonPropertyOrder(10)] ImmutableArray<CoverageFactJson> Coverage);
