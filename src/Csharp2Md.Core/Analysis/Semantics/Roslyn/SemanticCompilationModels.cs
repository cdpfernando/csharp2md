using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Analysis.Semantics.Roslyn;

internal sealed record SemanticSourceDocument(
    DocumentFactId DocumentId,
    string RelativePath,
    string SourceText);

internal sealed record SemanticCompilationRequest(
    EvaluatedTarget Target,
    string AssemblyName,
    ImmutableArray<SemanticSourceDocument> Documents);

internal enum SemanticBindingStatus
{
    Exact,
    Degraded,
    Unavailable,
}

internal sealed record SemanticDocumentBinding(
    DocumentFactId DocumentId,
    SyntaxTree SyntaxTree,
    SemanticModel? SemanticModel,
    SemanticBindingStatus Status,
    ImmutableArray<AnalysisDiagnostic> Diagnostics);

internal sealed record SemanticCompilationResult(
    TargetFactId TargetId,
    CSharpCompilation? Compilation,
    SemanticBindingStatus Status,
    ImmutableArray<SemanticDocumentBinding> Documents,
    ImmutableArray<string> ExcludedAnalyzerPaths,
    ImmutableArray<AnalysisDiagnostic> Diagnostics);

internal interface ISemanticCompilationAdapter
{
    SemanticCompilationResult CreateCompilation(
        SemanticCompilationRequest request,
        CancellationToken cancellationToken);
}

internal interface ICSharpCompilationFactory
{
    CSharpCompilation? Create(
        string assemblyName,
        IEnumerable<SyntaxTree> syntaxTrees,
        IEnumerable<MetadataReference> references,
        CSharpCompilationOptions options);

    SemanticModel? GetSemanticModel(CSharpCompilation compilation, SyntaxTree syntaxTree);
}

