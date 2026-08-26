using Csharp2Md.Analysis.Classification.Configuration;

namespace Csharp2Md.Analysis.Classification.Passes;

internal sealed class ConfigurationPass : IClassifierPass
{
    public string Name => "Configuration";

    public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ConfigurationEmitter.Emit(ConfigurationModelBuilder.Build(context), context);
    }
}
