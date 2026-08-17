using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Detection.Contracts;

internal sealed record ProjectDetectionContext(
    ProjectFact Project,
    ImmutableArray<TargetFact> Targets,
    ImmutableArray<SymbolFact> Symbols);

internal sealed record DocumentDetectionContext(
    ProjectFact Project,
    TargetFact? Target,
    DocumentFact Document,
    ImmutableArray<SymbolFact> Symbols);
