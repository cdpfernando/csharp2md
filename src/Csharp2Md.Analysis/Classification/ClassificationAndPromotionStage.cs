using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Classification;

internal sealed class ClassificationAndPromotionStage : IPipelineStage
{
    private readonly ImmutableArray<IClassifierPass> _passes;

    public ClassificationAndPromotionStage(ImmutableArray<IClassifierPass> passes) =>
        _passes = passes.IsDefault ? [] : passes;

    public string Name => "Classification and Promotion";

    /// <summary>The passes this stage runs, in the order it runs them.</summary>
    public ImmutableArray<IClassifierPass> Passes => _passes;

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_passes.IsDefaultOrEmpty)
        {
            return ValueTask.FromResult(
                new StageResult(0, 0, 0, StructuralCorruption: false, HasUnknownsOrCandidatesOrFrontiers: false));
        }

        var classifierContext = new ClassifierContext(context);
        var factCount = 0;
        var relationCount = 0;
        var candidateCount = 0;
        var unresolvedCount = 0;

        foreach (var pass in _passes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClassifierPassResult result;
            if (pass is InvokesPass invokesPass)
            {
                // GCPC-012, GCPC-014: capture InvokesPass's own per-occurrence disposition ledger while
                // it still exists -- an excluded occurrence leaves no other published trace, so this is
                // the only point the invocation-accounting envelope can still be built from it.
                result = invokesPass.Execute(classifierContext, cancellationToken, out var ledger);
                PublishInvocationAccounting(context, ledger);
            }
            else
            {
                result = pass.Execute(classifierContext, cancellationToken);
            }

            factCount += result.FactCount;
            relationCount += result.RelationCount;
            candidateCount += result.CandidateCount;
            unresolvedCount += result.UnresolvedCount;
            classifierContext.Refresh();
        }

        context.Accumulator.SetContractAccounting(ContractAccounting.Build(context.Accumulator.ToSnapshot()));

        return ValueTask.FromResult(
            new StageResult(
                factCount,
                0,
                relationCount,
                StructuralCorruption: context.Accumulator.StructuralCorruption,
                HasUnknownsOrCandidatesOrFrontiers: candidateCount > 0 || unresolvedCount > 0));
    }

    private static void PublishInvocationAccounting(PipelineContext context, InvocationDispositionLedger ledger)
    {
        var snapshot = context.Accumulator.ToSnapshot();
        var recognizedOccurrences = snapshot.Observations
            .Where(static o => o.Identity.Kind is ObservationKind.Invocation or ObservationKind.ObjectCreation)
            .Select(static o => o.Identity)
            .ToArray();
        var openFrontierOccurrences = snapshot.Frontiers.Select(static f => f.Occurrence).ToArray();

        context.Accumulator.SetInvocationAccounting(
            InvocationAccounting.Build(recognizedOccurrences, ledger, openFrontierOccurrences));
    }
}
