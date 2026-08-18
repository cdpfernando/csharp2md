using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Facts.Model;

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
    SolutionAnalysisIndex Index);
