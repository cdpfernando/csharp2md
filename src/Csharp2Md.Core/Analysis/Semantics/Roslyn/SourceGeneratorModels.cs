using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Analysis.Semantics.Roslyn;

internal sealed record SourceGeneratorExecutionRequest(
    ProjectFactId ProjectId,
    SemanticCompilationResult SemanticCompilation,
    ImmutableArray<string> GeneratorPaths)
{
    public static SourceGeneratorExecutionRequest? Create(
        ProjectFactId projectId,
        SemanticCompilationResult semanticCompilation,
        IEnumerable<string> generatorPaths,
        AnalysisOptions options)
    {
        ArgumentNullException.ThrowIfNull(semanticCompilation);
        ArgumentNullException.ThrowIfNull(generatorPaths);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IncludeSourceGenerators)
        {
            return null;
        }

        if (options.Validate() is { } error)
        {
            throw new ArgumentException(AnalysisOptions.MessageFor(error), nameof(options));
        }

        if (options is not { Mode: AnalysisMode.Semantic, Trust: TrustMode.TrustedSolution })
        {
            throw new ArgumentException("Source generators require trusted semantic analysis.", nameof(options));
        }

        return new SourceGeneratorExecutionRequest(
            projectId,
            semanticCompilation,
            generatorPaths
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray());
    }
}

internal sealed record GeneratedSemanticDocument(
    DocumentFactId DocumentId,
    TargetFactId TargetId,
    string GeneratorName,
    string RelativePath,
    string SourceText);

internal sealed record SourceGeneratorExecutionResult(
    CSharpCompilation? Compilation,
    ImmutableArray<GeneratedSemanticDocument> GeneratedDocuments,
    ImmutableArray<string> LoadedExtensions,
    ImmutableArray<AnalysisDiagnostic> Diagnostics);

internal interface ISourceGeneratorAdapter
{
    SourceGeneratorExecutionResult Run(
        SourceGeneratorExecutionRequest request,
        CancellationToken cancellationToken);
}

