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
            .SetItem(2, new ObservationExtractionStage());
}
