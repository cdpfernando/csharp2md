using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;

namespace Csharp2Md.Core.Detection.Contracts;

internal sealed record ProjectDetectionContext(
    ProjectFact Project,
    ImmutableArray<TargetFact> Targets,
    ImmutableArray<SymbolFact> Symbols,
    SolutionAnalysisIndex Index);

internal sealed record DocumentDetectionContext(
    ProjectFact Project,
    TargetFact? Target,
    DocumentFact Document,
    ImmutableArray<SymbolFact> Symbols,
    SolutionAnalysisIndex Index,
    SemanticDetectionDocument? SemanticDocument = null);

internal sealed class SemanticDetectionDocument
{
    public SyntaxTree SyntaxTree { get; }

    public SemanticModel SemanticModel { get; }

    public SemanticDetectionDocument(SyntaxTree syntaxTree, SemanticModel semanticModel)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(semanticModel);
        if (semanticModel.SyntaxTree != syntaxTree)
        {
            throw new ArgumentException("The semantic model must belong to the supplied syntax tree.", nameof(semanticModel));
        }

        SyntaxTree = syntaxTree;
        SemanticModel = semanticModel;
    }
}
