using Csharp2Md.Analysis.Inventory;

namespace Csharp2Md.Analysis.Pipeline;

internal static class PipelineStages
{
    internal static ImmutableArray<IPipelineStage> CreateDefault() =>
        StubStages.CreateDefault().SetItem(0, new InventoryStage());
}
