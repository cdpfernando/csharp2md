using Csharp2Md.Analysis.Pipeline;

namespace Csharp2Md.Analysis.Classification;

internal sealed class ClassificationAndPromotionStage : IPipelineStage
{
    private readonly ImmutableArray<IClassifierPass> _passes;

    public ClassificationAndPromotionStage(ImmutableArray<IClassifierPass> passes) =>
        _passes = passes.IsDefault ? [] : passes;

    public string Name => "Classification and Promotion";

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
            var result = pass.Execute(classifierContext, cancellationToken);
            factCount += result.FactCount;
            relationCount += result.RelationCount;
            candidateCount += result.CandidateCount;
            unresolvedCount += result.UnresolvedCount;
            classifierContext.Refresh();
        }

        return ValueTask.FromResult(
            new StageResult(
                factCount,
                0,
                relationCount,
                StructuralCorruption: context.Accumulator.StructuralCorruption,
                HasUnknownsOrCandidatesOrFrontiers: candidateCount > 0 || unresolvedCount > 0));
    }
}
