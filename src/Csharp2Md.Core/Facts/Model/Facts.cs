using Csharp2Md.Core.Facts.Identity;

namespace Csharp2Md.Core.Facts.Model;

public sealed record SolutionFact(FactHeader Header, string Name, ImmutableArray<ProjectFactId> ProjectIds) : IFact;

public sealed record ProjectFact(
    FactHeader Header,
    ProjectFactId ProjectId,
    string Name,
    string RelativePath,
    ImmutableArray<TargetFactId> TargetIds,
    ImmutableArray<DocumentFactId> DocumentIds,
    ProjectEvaluationDetails? Evaluation = null) : IFact;

public sealed record ProjectEvaluationDetails(
    string DeclaredSdk,
    ImmutableArray<string> EvaluatedImports,
    ImmutableArray<string> TargetFrameworks,
    string RequestedAnalysis,
    FactResolution EffectiveResolution,
    bool RestorePerformed,
    string Isolation);

public sealed record TargetFact(
    FactHeader Header,
    TargetFactId TargetId,
    ProjectFactId ProjectId,
    string TargetFramework,
    TargetEvaluationDetails? Evaluation = null) : IFact;

public sealed record TargetEvaluationDetails(
    string OutputType,
    string AssemblyName,
    string RootNamespace,
    ImmutableArray<string> CompileItems,
    ImmutableArray<string> ProjectReferences,
    ImmutableArray<string> PackageReferences,
    ImmutableArray<string> References,
    ImmutableArray<string> Constants,
    string LanguageVersion,
    string NullableMode,
    ImmutableArray<string> CompiledExtensions);

public sealed record DocumentFact(
    FactHeader Header,
    DocumentFactId DocumentId,
    ProjectFactId ProjectId,
    string RelativePath,
    ImmutableArray<SourceSectionFact> Sections,
    ImmutableArray<SymbolFactId> SymbolIds) : IFact;

public sealed record SourceSectionFact(
    FactHeader Header,
    DocumentFactId DocumentId,
    string SectionKind,
    int OccurrenceOrdinal,
    int StartOffset,
    int Length,
    string Source) : IFact;

public sealed record SymbolFact(
    FactHeader Header,
    SymbolFactId SymbolId,
    DocumentFactId DocumentId,
    string SymbolKind,
    bool ContainsErrorSymbol,
    ImmutableArray<SymbolFactId> BaseAndInterfaceIds,
    ImmutableArray<string> Attributes,
    ImmutableArray<string> RelevantTypeReferences) : IFact;

public sealed record ComponentFact(
    FactHeader Header,
    ComponentFactId ComponentId,
    string ComponentKind,
    ImmutableArray<ProjectFactId> ProjectIds) : IFact;
