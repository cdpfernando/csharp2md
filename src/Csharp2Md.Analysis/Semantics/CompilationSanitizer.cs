using Microsoft.CodeAnalysis;

namespace Csharp2Md.Analysis.Semantics;

internal static class CompilationSanitizer
{
    internal static Project Strip(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.WithAnalyzerReferences([]);
    }
}
