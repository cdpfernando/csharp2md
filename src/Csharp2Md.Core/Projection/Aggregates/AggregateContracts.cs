using System.Text.Json.Serialization;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Facts.Storage;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed record AggregateEnvelope(
    [property: JsonPropertyOrder(0)] int SchemaVersion,
    [property: JsonPropertyOrder(1)] string Kind,
    [property: JsonPropertyOrder(2)] ImmutableArray<string> Entries);

internal sealed record DiagnosticAggregate(
    [property: JsonPropertyOrder(0)] int SchemaVersion,
    [property: JsonPropertyOrder(1)] string Kind,
    [property: JsonPropertyOrder(2)] ImmutableArray<AnalysisDiagnosticJson> Entries);

internal sealed record CoverageAggregate(
    [property: JsonPropertyOrder(0)] int SchemaVersion,
    [property: JsonPropertyOrder(1)] string Kind,
    [property: JsonPropertyOrder(2)] ImmutableArray<CoverageFactJson> Entries);

internal sealed record ManifestAnalysis(
    [property: JsonPropertyOrder(0)] string Requested,
    [property: JsonPropertyOrder(1)] string Effective);

internal sealed record ManifestCoverage(
    [property: JsonPropertyOrder(0)] int Services,
    [property: JsonPropertyOrder(1)] int Projects,
    [property: JsonPropertyOrder(2)] int Documents);

internal sealed record ManifestFragment(
    [property: JsonPropertyOrder(0)] string FactId,
    [property: JsonPropertyOrder(1)] string Reference,
    [property: JsonPropertyOrder(2)] string Sha256,
    [property: JsonPropertyOrder(3)] int ByteLength);

internal sealed record FactualManifest(
    [property: JsonPropertyOrder(0)] int SchemaVersion,
    [property: JsonPropertyOrder(1)] string ToolVersion,
    [property: JsonPropertyOrder(2)] ManifestAnalysis Analysis,
    [property: JsonPropertyOrder(3)] string Trust,
    [property: JsonPropertyOrder(4)] bool RestorePerformed,
    [property: JsonPropertyOrder(5)] string Isolation,
    [property: JsonPropertyOrder(6)] ImmutableArray<string> Extensions,
    [property: JsonPropertyOrder(7)] ManifestCoverage Coverage,
    [property: JsonPropertyOrder(8)] ImmutableArray<ManifestFragment> Fragments,
    [property: JsonPropertyOrder(9)] ImmutableArray<string> Hashes);

internal sealed record AggregateOutputSnapshot(
    string Topic,
    string Domain,
    string ToolVersion,
    AnalysisMode RequestedAnalysis,
    AnalysisMode EffectiveAnalysis,
    TrustMode Trust,
    ImmutableArray<string> Extensions,
    ManifestCoverage Coverage,
    ImmutableArray<StoredFactFragment> Fragments,
    CoverageProjectionResult? HonestCoverage = null);

internal sealed record AggregateWriteResult(string RawRoot, string ManifestPath);
