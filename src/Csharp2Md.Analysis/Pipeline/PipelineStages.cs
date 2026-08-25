namespace Csharp2Md.Analysis.Pipeline;

internal static class PipelineStages
{
    internal static ImmutableArray<IPipelineStage> CreateDefault() => StubStages.CreateDefault();
}
