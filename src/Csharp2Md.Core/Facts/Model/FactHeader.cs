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
}

public sealed record FactHeader
{
    public FactId Id { get; }

    public FactKind Kind { get; }

    public FactResolution Resolution { get; }

    public ImmutableArray<FactProvenance> Provenance { get; }

    public ImmutableArray<Evidence> Evidence { get; }

    public ImmutableArray<DiagnosticId> DiagnosticIds { get; }

    private FactHeader(
        FactId id,
        FactKind kind,
        FactResolution resolution,
        ImmutableArray<FactProvenance> provenance,
        ImmutableArray<Evidence> evidence,
        ImmutableArray<DiagnosticId> diagnosticIds)
    {
        Id = id;
        Kind = kind;
        Resolution = resolution;
        Provenance = provenance;
        Evidence = evidence;
        DiagnosticIds = diagnosticIds;
    }

    public static FactHeader Create(
        FactId id,
        FactKind kind,
        FactResolution resolution,
        IEnumerable<FactProvenance>? provenance = null,
        IEnumerable<Evidence>? evidence = null,
        IEnumerable<DiagnosticId>? diagnosticIds = null) =>
        new(
            id,
            kind,
            resolution,
            (provenance ?? []).Distinct().Order().ToImmutableArray(),
            (evidence ?? []).Distinct().Order().ToImmutableArray(),
            (diagnosticIds ?? []).Distinct().OrderBy(static diagnostic => diagnostic.Value, StringComparer.Ordinal).ToImmutableArray());
}

public interface IFact
{
    FactHeader Header { get; }
}
