using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Classification.Passes;

internal sealed class ExecutesPass : IClassifierPass
{
    private static readonly FacetBinding EmptyFacets =
        FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    internal static ClassifierIdentity Identity { get; } =
        ClassifierIdentity.Create("csharp2md.classifier.executes", 1);

    public string Name => "Executes";

    public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.AnalysisVariants.IsDefaultOrEmpty)
        {
            return new ClassifierPassResult(0, 0, 0, 0);
        }

        var symbolsById = context.FactsByType<Symbol>()
            .ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);
        var relationCount = 0;

        foreach (var entryPoint in context.FactsByType<EntryPoint>()
            .OrderBy(static entry => entry.Reference.Id.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entryPoint.Symbol.Equals(default))
            {
                continue;
            }

            if (!symbolsById.TryGetValue(entryPoint.Symbol.Id.Value, out var symbol)
                || !symbol.Facets.Facets.Contains(SymbolFacet.Callable))
            {
                context.Accumulator.AddDiagnostic(
                    new DiagnosticRecord(
                        "executes-pass",
                        $"EntryPoint {entryPoint.Reference.Id.Value} references symbol {entryPoint.Symbol.Id.Value} which is not in the snapshot.",
                        entryPoint.Reference.Id.Value));
                continue;
            }

            var identities = context.ObservationsByOwner(entryPoint.Symbol)
                .Select(static observation => observation.Identity)
                .ToArray();
            if (identities.Length == 0)
            {
                context.Accumulator.AddDiagnostic(
                    new DiagnosticRecord(
                        "executes-pass",
                        $"EntryPoint {entryPoint.Reference.Id.Value} references symbol {entryPoint.Symbol.Id.Value} which is not in the snapshot.",
                        entryPoint.Reference.Id.Value));
                continue;
            }

            context.Accumulator.AddRelation(
                ConfirmedRelation.Create(
                    RelationKind.Executes,
                    entryPoint.Reference,
                    symbol.Reference,
                    EmptyFacets,
                    EvidenceChain.Create(identities),
                    Identity,
                    context.AnalysisVariants,
                    EvidenceMethod.Semantic,
                    targetFact: symbol));
            relationCount++;
        }

        return new ClassifierPassResult(0, relationCount, 0, 0);
    }
}
