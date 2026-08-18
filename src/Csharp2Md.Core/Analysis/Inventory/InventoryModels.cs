namespace Csharp2Md.Core.Analysis.Inventory;

internal sealed record InventoryResult(
    ImmutableArray<InventoryService> Services,
    ImmutableArray<InventoryDiagnostic> Diagnostics)
{
    public bool IsSuccess => Diagnostics.All(static diagnostic => diagnostic.Severity != InventoryDiagnosticSeverity.Error);
}

internal sealed record InventoryService(
    string Name,
    string RootPath,
    string? SolutionPath,
    ImmutableArray<InventoryProject> Projects);

internal sealed record InventoryProject(
    string Name,
    string RelativePath,
    ImmutableArray<string> SourceFiles,
    ImmutableArray<string> ConfigurationFiles,
    ImmutableArray<string> DeclaredImports,
    ImmutableArray<string> AnalyzerPaths,
    ImmutableArray<string> GeneratorPaths,
    ImmutableDictionary<string, string> SourcePaths);

internal enum InventoryDiagnosticSeverity { Warning, Error }

internal sealed record InventoryDiagnostic(
    string Code,
    InventoryDiagnosticSeverity Severity,
    string Message);

internal interface IInventoryExecutionObserver
{
    void ExecutableAdapterInvoked(string adapterKind);
}
