using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Detection.Contracts;

internal sealed record DetectorResult
{
    public ImmutableArray<IFact> Facts { get; }

    public ImmutableArray<AnalysisDiagnostic> Diagnostics { get; }

    private DetectorResult(ImmutableArray<IFact> facts, ImmutableArray<AnalysisDiagnostic> diagnostics)
    {
        Facts = facts;
        Diagnostics = diagnostics;
    }

    public static DetectorResult Create(
        IEnumerable<IFact>? facts = null,
        IEnumerable<AnalysisDiagnostic>? diagnostics = null) =>
        new(
            (facts ?? [])
                .OrderBy(static fact => fact.Header.Id.Value, StringComparer.Ordinal)
                .ToImmutableArray(),
            (diagnostics ?? [])
                .Order()
                .ToImmutableArray());
}
