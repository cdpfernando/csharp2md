using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;

namespace Csharp2Md.Core.Facts.Model;

public enum FactKind
{
    Solution,
    Project,
    Target,
    Document,
    SourceSection,
    Symbol,
    Component,
    Relation,
    DatabaseObject,
    DatabaseColumn,
}

public sealed record FactHeader
{
    public FactId Id { get; }

    public FactKind Kind { get; }

    public FactResolution Resolution { get; }

    public ImmutableArray<FactProvenance> Provenance { get; }

    public ImmutableArray<Evidence> Evidence { get; }

    public ImmutableArray<DiagnosticId> DiagnosticIds { get; }

    public bool GeneratedOrigin { get; }

    private FactHeader(
        FactId id,
        FactKind kind,
        FactResolution resolution,
        ImmutableArray<FactProvenance> provenance,
        ImmutableArray<Evidence> evidence,
        ImmutableArray<DiagnosticId> diagnosticIds,
        bool generatedOrigin)
    {
        Id = id;
        Kind = kind;
        Resolution = resolution;
        Provenance = provenance;
        Evidence = evidence;
        DiagnosticIds = diagnosticIds;
        GeneratedOrigin = generatedOrigin;
    }

    public static FactHeader Create(
        FactId id,
        FactKind kind,
        FactResolution resolution,
        IEnumerable<FactProvenance>? provenance = null,
        IEnumerable<Evidence>? evidence = null,
        IEnumerable<DiagnosticId>? diagnosticIds = null,
        bool generatedOrigin = false) =>
        new(
            id,
            kind,
            resolution,
            (provenance ?? []).Distinct().Order().ToImmutableArray(),
            (evidence ?? []).Distinct().Order().ToImmutableArray(),
            (diagnosticIds ?? []).Distinct().OrderBy(static diagnostic => diagnostic.Value, StringComparer.Ordinal).ToImmutableArray(),
            generatedOrigin);
}

public interface IFact
{
    FactHeader Header { get; }
}
