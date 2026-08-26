using Csharp2Md.Analysis.Classification.Persistence;

namespace Csharp2Md.Analysis.Classification.Passes;

internal sealed class PersistencePass : IClassifierPass
{
    public string Name => "Persistence";

    public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return PersistenceEmitter.Emit(PersistenceModelBuilder.Build(context, cancellationToken), context);
    }
}
