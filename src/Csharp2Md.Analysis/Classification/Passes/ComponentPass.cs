using Csharp2Md.Analysis.Classification.Topology;

namespace Csharp2Md.Analysis.Classification.Passes;

internal sealed class ComponentPass : IClassifierPass
{
    public string Name => "Components";

    public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return TopologyEmitter.Emit(TopologyModelBuilder.Build(context), context);
    }
}
