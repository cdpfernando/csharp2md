using System.Text;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using EvaluatedItem = Csharp2Md.Core.Analysis.Semantics.MSBuild.EvaluatedItem;

namespace Csharp2Md.Core.Analysis.Semantics.Roslyn;

internal sealed class SemanticCompilationAdapter(
    ICSharpCompilationFactory? compilationFactory = null) : ISemanticCompilationAdapter
{
    private readonly ICSharpCompilationFactory _compilationFactory =
        compilationFactory ?? new CSharpCompilationFactory();

    public SemanticCompilationResult CreateCompilation(
        SemanticCompilationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var parseOptions = CreateParseOptions(request.Target.Properties);
        var syntaxTrees = request.Documents
            .OrderBy(static document => document.RelativePath, StringComparer.Ordinal)
            .Select(document => CSharpSyntaxTree.ParseText(
                document.SourceText,
                parseOptions,
                document.RelativePath,
                Encoding.UTF8,
                cancellationToken))
            .ToImmutableArray();
        var references = CreateMetadataReferences(request.Target.Items);
        var analyzerPaths = request.Target.Items.GetValueOrDefault("Analyzer", [])
            .Select(static item => item.Identity)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

        CSharpCompilation? compilation;
        try
        {
            compilation = _compilationFactory.Create(
                request.AssemblyName,
                syntaxTrees,
                references,
                new CSharpCompilationOptions(MapOutputKind(request.Target.Properties)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(request.Target.TargetId, analyzerPaths, "C2M-COMP-001", exception.Message);
        }

        if (compilation is null)
        {
            return Unavailable(
                request.Target.TargetId,
                analyzerPaths,
                "C2M-COMP-002",
                "Roslyn returned no compilation for the evaluated target.");
        }

        var compilerErrors = compilation.GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .OrderBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        var diagnostics = ImmutableArray.CreateBuilder<AnalysisDiagnostic>();
        if (!compilerErrors.IsEmpty)
        {
            diagnostics.Add(Diagnostic(
                "C2M-COMP-003",
                request.Target.TargetId.ToFactId(),
                "The target compilation contains error diagnostics.",
                string.Join(',', compilerErrors.Select(static diagnostic => diagnostic.Id).Distinct(StringComparer.Ordinal))));
        }

        var bindings = ImmutableArray.CreateBuilder<SemanticDocumentBinding>(request.Documents.Length);
        var orderedDocuments = request.Documents
            .OrderBy(static item => item.RelativePath, StringComparer.Ordinal)
            .ToImmutableArray();
        for (var index = 0; index < orderedDocuments.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = orderedDocuments[index];
            var syntaxTree = syntaxTrees[index];
            try
            {
                var semanticModel = _compilationFactory.GetSemanticModel(compilation, syntaxTree);
                if (semanticModel is null)
                {
                    var diagnostic = Diagnostic(
                        "C2M-COMP-004",
                        document.DocumentId.ToFactId(),
                        "Roslyn returned no semantic model for the document.",
                        document.RelativePath);
                    diagnostics.Add(diagnostic);
                    bindings.Add(new SemanticDocumentBinding(
                        document.DocumentId,
                        syntaxTree,
                        null,
                        SemanticBindingStatus.Unavailable,
                        [diagnostic]));
                    continue;
                }

                var documentHasErrors = compilerErrors.Any(error => error.Location.SourceTree == syntaxTree);
                bindings.Add(new SemanticDocumentBinding(
                    document.DocumentId,
                    syntaxTree,
                    semanticModel,
                    documentHasErrors ? SemanticBindingStatus.Degraded : SemanticBindingStatus.Exact,
                    []));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var diagnostic = Diagnostic(
                    "C2M-COMP-005",
                    document.DocumentId.ToFactId(),
                    "Semantic-model creation failed for the document.",
                    exception.Message);
                diagnostics.Add(diagnostic);
                bindings.Add(new SemanticDocumentBinding(
                    document.DocumentId,
                    syntaxTree,
                    null,
                    SemanticBindingStatus.Unavailable,
                    [diagnostic]));
            }
        }

        var status = bindings.Any(static binding => binding.Status is not SemanticBindingStatus.Exact)
            || !compilerErrors.IsEmpty
            ? SemanticBindingStatus.Degraded
            : SemanticBindingStatus.Exact;
        return new SemanticCompilationResult(
            request.Target.TargetId,
            compilation,
            status,
            bindings.ToImmutable(),
            analyzerPaths,
            diagnostics.Order().ToImmutableArray());
    }

    private static CSharpParseOptions CreateParseOptions(ImmutableDictionary<string, string> properties)
    {
        var languageVersionText = properties.GetValueOrDefault("LangVersion", string.Empty);
        var languageVersion = LanguageVersionFacts.TryParse(languageVersionText, out var parsed)
            ? parsed
            : LanguageVersion.Default;
        var constants = properties.GetValueOrDefault("DefineConstants", string.Empty)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        return new CSharpParseOptions(
            languageVersion,
            DocumentationMode.Parse,
            SourceCodeKind.Regular,
            constants);
    }

    private static OutputKind MapOutputKind(ImmutableDictionary<string, string> properties) =>
        properties.GetValueOrDefault("OutputType", string.Empty) switch
        {
            "Exe" => OutputKind.ConsoleApplication,
            "WinExe" => OutputKind.WindowsApplication,
            "Module" => OutputKind.NetModule,
            _ => OutputKind.DynamicallyLinkedLibrary,
        };

    private static ImmutableArray<MetadataReference> CreateMetadataReferences(
        ImmutableDictionary<string, ImmutableArray<EvaluatedItem>> items)
    {
        var candidatePaths = TrustedPlatformAssemblyPaths()
            .Concat(items.GetValueOrDefault("Reference", []).SelectMany(ReferenceCandidatePaths))
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
        return candidatePaths
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static IEnumerable<string> ReferenceCandidatePaths(EvaluatedItem reference)
    {
        if (reference.Metadata.TryGetValue("FullPath", out var fullPath))
        {
            yield return fullPath;
        }

        if (reference.Metadata.TryGetValue("HintPath", out var hintPath))
        {
            yield return hintPath;
        }

        if (Path.IsPathFullyQualified(reference.Identity))
        {
            yield return reference.Identity;
        }
    }

    private static IEnumerable<string> TrustedPlatformAssemblyPaths() =>
        (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

    private static SemanticCompilationResult Unavailable(
        TargetFactId targetId,
        ImmutableArray<string> analyzerPaths,
        string code,
        string detail)
    {
        var diagnostic = Diagnostic(
            code,
            targetId.ToFactId(),
            "Semantic compilation is unavailable for the target.",
            detail);
        return new SemanticCompilationResult(
            targetId,
            null,
            SemanticBindingStatus.Unavailable,
            [],
            analyzerPaths,
            [diagnostic]);
    }

    private static AnalysisDiagnostic Diagnostic(
        string code,
        FactId scopeId,
        string message,
        string detail) =>
        AnalysisDiagnostic.Create(
            code,
            Csharp2Md.Core.Facts.Metadata.DiagnosticSeverity.Warning,
            DiagnosticStage.Compilation,
            scopeId,
            message,
            string.IsNullOrWhiteSpace(detail)
                ? []
                : [new DiagnosticData("detail", Sanitize(detail))]);

    private static string Sanitize(string value) =>
        string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private sealed class CSharpCompilationFactory : ICSharpCompilationFactory
    {
        public CSharpCompilation Create(
            string assemblyName,
            IEnumerable<SyntaxTree> syntaxTrees,
            IEnumerable<MetadataReference> references,
            CSharpCompilationOptions options) =>
            CSharpCompilation.Create(assemblyName, syntaxTrees, references, options);

        public SemanticModel GetSemanticModel(CSharpCompilation compilation, SyntaxTree syntaxTree) =>
            compilation.GetSemanticModel(syntaxTree);
    }
}
