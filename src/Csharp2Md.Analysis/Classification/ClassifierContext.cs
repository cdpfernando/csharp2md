using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Classification;

internal sealed class ClassifierContext
{
    private FactualSnapshot _snapshot = FactualSnapshot.Empty;
    private ILookup<ObservationKind, Observation> _observationsByKind =
        Array.Empty<Observation>().ToLookup(static _ => ObservationKind.Invocation);
    private ILookup<string, Observation> _observationsByOwner =
        Array.Empty<Observation>().ToLookup(static _ => string.Empty, StringComparer.Ordinal);
    private IReadOnlyDictionary<string, Symbol> _symbolsBySignature =
        new Dictionary<string, Symbol>(StringComparer.Ordinal);

    public ClassifierContext(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        Accumulator = pipeline.Accumulator;
        AnalysisVariants = pipeline.AnalysisVariants;
        Refresh();
    }

    public SnapshotAccumulator Accumulator { get; }

    public ImmutableArray<AnalysisVariantId> AnalysisVariants { get; }

    public ImmutableArray<IFact> Facts => _snapshot.Facts;

    public ImmutableArray<Observation> Observations => _snapshot.Observations;

    public SolutionId SolutionId => FactsByType<Solution>().Single().Id;

    public ImmutableArray<T> FactsByType<T>()
        where T : class, IFact =>
        [.. _snapshot.Facts.OfType<T>()];

    public IReadOnlyList<Observation> ObservationsByKind(ObservationKind kind) =>
        [.. _observationsByKind[kind]];

    public IReadOnlyList<Observation> ObservationsByOwner(FactReference owner) =>
        [.. _observationsByOwner[owner.Id.Value]];

    public IReadOnlyDictionary<string, Symbol> SymbolsBySignatureKey() => _symbolsBySignature;

    public void Refresh()
    {
        _snapshot = Accumulator.ToSnapshot();
        _observationsByKind = _snapshot.Observations.ToLookup(static observation => observation.Identity.Kind);
        _observationsByOwner = _snapshot.Observations.ToLookup(
            static observation => observation.Identity.Owner.Id.Value,
            StringComparer.Ordinal);
        _symbolsBySignature = _snapshot.Facts.OfType<Symbol>()
            .GroupBy(static symbol => SignatureKey(symbol.OwningProject, symbol.Signature), StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
    }

    public static string SignatureKey(ProjectId projectId, CanonicalSymbolSignature signature) =>
        projectId.Value + "\u001f" + signature.Value;
}
