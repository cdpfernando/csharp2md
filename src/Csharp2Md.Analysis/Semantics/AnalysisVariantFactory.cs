using Csharp2Md.Domain.Identity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Analysis.Semantics;

internal static class AnalysisVariantFactory
{
    internal const string DefaultConfiguration = "Debug";

    internal const string LocalEnvironment = "local";

    internal static AnalysisVariantId Create(Project project, string targetFramework, string configuration)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFramework);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration);

        IEnumerable<string> symbols = project.ParseOptions is CSharpParseOptions parseOptions
            ? parseOptions.PreprocessorSymbolNames
            : [];

        return AnalysisVariantId.Create(targetFramework, configuration, symbols, LocalEnvironment);
    }
}
