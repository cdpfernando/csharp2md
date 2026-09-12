using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Semantics;

namespace Csharp2Md.Analysis.Pipeline;

internal static class PipelineStages
{
    internal static ImmutableArray<IPipelineStage> CreateDefault() =>
    [
        new InventoryStage(),
        new SemanticAnalysisStage(),
        new ObservationExtractionStage(),
        new ClassificationAndPromotionStage(
        [
            new ComponentPass(),
            new EntryPointPass(),
            new BoundaryPass(),
            new ContractPass(),
            new PersistencePass(),
            new ConfigurationPass(),
            new RelationPass(),
            new InvokesPass(),
            new ExecutesPass(),
        ]),
        new ValidationAndCoverageStage(),
        new PersistenceStage(),
    ];
}
