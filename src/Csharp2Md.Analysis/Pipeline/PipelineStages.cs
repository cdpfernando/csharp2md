using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Semantics;

namespace Csharp2Md.Analysis.Pipeline;

internal static class PipelineStages
{
    internal static ImmutableArray<IPipelineStage> CreateDefault() =>
        StubStages.CreateDefault()
            .SetItem(0, new InventoryStage())
            .SetItem(1, new SemanticAnalysisStage())
            .SetItem(2, new ObservationExtractionStage())
            .SetItem(3, new ClassificationAndPromotionStage(
            [
                new ComponentPass(),
                new EntryPointPass(),
                new BoundaryPass(),
                new ContractPass(),
                new PersistencePass(),
                new RelationPass(),
                new InvokesPass(),
                new ExecutesPass(),
            ]))
            .SetItem(5, new PersistenceStage());
}
