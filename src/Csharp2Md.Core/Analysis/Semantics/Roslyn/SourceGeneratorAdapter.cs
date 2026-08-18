using System.Reflection;
using System.Runtime.Loader;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Analysis.Semantics.Roslyn;

internal sealed class SourceGeneratorAdapter : ISourceGeneratorAdapter
{
    public SourceGeneratorExecutionResult Run(
        SourceGeneratorExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.SemanticCompilation.Compilation is not { } compilation)
        {
            return new SourceGeneratorExecutionResult(
                null,
                [],
                [],
                [Diagnostic(
                    "C2M-GEN-001",
                    request.SemanticCompilation.TargetId.ToFactId(),
                    "Source generators were not run because the target compilation is unavailable.",
                    "compilation-unavailable")]);
        }

        var loadContext = new GeneratorLoadContext(request.GeneratorPaths);
        try
        {
            var generators = ImmutableArray.CreateBuilder<ISourceGenerator>();
            var loadedExtensions = ImmutableArray.CreateBuilder<string>();
            var diagnostics = ImmutableArray.CreateBuilder<AnalysisDiagnostic>();
            foreach (var generatorPath in request.GeneratorPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var assembly = loadContext.LoadFromAssemblyPath(generatorPath);
                    foreach (var type in assembly.GetTypes()
                        .Where(static type => !type.IsAbstract)
                        .OrderBy(static type => type.FullName, StringComparer.Ordinal))
                    {
                        if (typeof(ISourceGenerator).IsAssignableFrom(type))
                        {
                            generators.Add((ISourceGenerator)Activator.CreateInstance(type)!);
                            loadedExtensions.Add(type.FullName!);
                        }
                        else if (typeof(IIncrementalGenerator).IsAssignableFrom(type))
                        {
                            var incremental = (IIncrementalGenerator)Activator.CreateInstance(type)!;
                            generators.Add(incremental.AsSourceGenerator());
                            loadedExtensions.Add(type.FullName!);
                        }
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    diagnostics.Add(Diagnostic(
                        "C2M-GEN-002",
                        request.SemanticCompilation.TargetId.ToFactId(),
                        "A source-generator assembly could not be loaded.",
                        $"{Path.GetFileName(generatorPath)}:{exception.GetType().Name}"));
                }
            }

            if (generators.Count == 0)
            {
                return new SourceGeneratorExecutionResult(
                    compilation,
                    [],
                    [],
                    diagnostics.Order().ToImmutableArray());
            }

            var parseOptions = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions
                ?? CSharpParseOptions.Default;
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators.ToImmutable(),
                parseOptions: parseOptions);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var updatedCompilation,
                out var driverDiagnostics,
                cancellationToken);
            var runResult = driver.GetRunResult();
            var sourceDocuments = request.SemanticCompilation.Documents
                .ToDictionary(static binding => binding.SyntaxTree, static binding => binding.DocumentId);
            foreach (var diagnostic in driverDiagnostics.Concat(runResult.Diagnostics))
            {
                diagnostics.Add(MapDiagnostic(
                    request.SemanticCompilation.TargetId,
                    diagnostic,
                    sourceDocuments));
            }

            foreach (var failedGenerator in runResult.Results.Where(static result => result.Exception is not null))
            {
                diagnostics.Add(Diagnostic(
                    "C2M-GEN-003",
                    request.SemanticCompilation.TargetId.ToFactId(),
                    "A source generator failed; pre-generator semantic facts were retained.",
                    GeneratorTypeName(failedGenerator.Generator)));
            }

            var generatedDocuments = runResult.Results
                .OrderBy(static result => GeneratorTypeName(result.Generator), StringComparer.Ordinal)
                .SelectMany(result => result.GeneratedSources
                    .OrderBy(static source => source.HintName, StringComparer.Ordinal)
                    .Select(source => MapGeneratedDocument(
                        request.ProjectId,
                        request.SemanticCompilation.TargetId,
                        GeneratorTypeName(result.Generator),
                        source)))
                .ToImmutableArray();
            return new SourceGeneratorExecutionResult(
                (CSharpCompilation)updatedCompilation,
                generatedDocuments,
                loadedExtensions
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToImmutableArray(),
                diagnostics
                    .GroupBy(static diagnostic => diagnostic.Id)
                    .Select(static group => group.First())
                    .Order()
                    .ToImmutableArray());
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static GeneratedSemanticDocument MapGeneratedDocument(
        ProjectFactId projectId,
        TargetFactId targetId,
        string generatorName,
        GeneratedSourceResult source)
    {
        var generatorSegment = SanitizePathSegment(generatorName);
        var hintPath = source.HintName.Replace('\\', '/').TrimStart('/');
        var relativePath = $".generated/{generatorSegment}/{hintPath}";
        return new GeneratedSemanticDocument(
            DocumentFactId.Create(projectId, relativePath),
            targetId,
            generatorName,
            relativePath,
            source.SourceText.ToString());
    }

    private static AnalysisDiagnostic MapDiagnostic(
        TargetFactId targetId,
        Microsoft.CodeAnalysis.Diagnostic diagnostic,
        IReadOnlyDictionary<SyntaxTree, DocumentFactId> sourceDocuments)
    {
        var evidence = ImmutableArray<Evidence>.Empty;
        if (diagnostic.Location is { IsInSource: true, SourceTree: { } tree }
            && sourceDocuments.TryGetValue(tree, out var documentId))
        {
            var span = diagnostic.Location.GetLineSpan().Span;
            evidence = [new Evidence(
                documentId,
                tree.FilePath.Replace('\\', '/'),
                span.Start.Line + 1,
                span.Start.Character + 1,
                span.End.Line + 1,
                span.End.Character + 1)];
        }

        return AnalysisDiagnostic.Create(
            diagnostic.Id,
            MapSeverity(diagnostic.Severity),
            DiagnosticStage.Generator,
            targetId.ToFactId(),
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture),
            evidence: evidence);
    }

    private static Csharp2Md.Core.Facts.Metadata.DiagnosticSeverity MapSeverity(
        Microsoft.CodeAnalysis.DiagnosticSeverity severity) => severity switch
        {
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error => Csharp2Md.Core.Facts.Metadata.DiagnosticSeverity.Error,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => Csharp2Md.Core.Facts.Metadata.DiagnosticSeverity.Warning,
            _ => Csharp2Md.Core.Facts.Metadata.DiagnosticSeverity.Information,
        };

    private static AnalysisDiagnostic Diagnostic(
        string code,
        FactId scopeId,
        string message,
        string detail) =>
        AnalysisDiagnostic.Create(
            code,
            Csharp2Md.Core.Facts.Metadata.DiagnosticSeverity.Warning,
            DiagnosticStage.Generator,
            scopeId,
            message,
            [new DiagnosticData("detail", detail)]);

    private static string SanitizePathSegment(string value) =>
        string.Concat(value.Select(static character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '_'));

    private static string GeneratorTypeName(ISourceGenerator generator) =>
        generator.GetGeneratorType().FullName ?? generator.GetGeneratorType().Name;

    private sealed class GeneratorLoadContext(ImmutableArray<string> generatorPaths)
        : AssemblyLoadContext(isCollectible: true)
    {
        private readonly ImmutableArray<string> _searchDirectories = generatorPaths
            .Select(Path.GetDirectoryName)
            .Where(static path => path is not null)
            .Select(static path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var shared = Default.Assemblies.FirstOrDefault(
                assembly => AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName));
            if (shared is not null)
            {
                return shared;
            }

            var dependencyName = $"{assemblyName.Name}.dll";
            var dependencyPath = _searchDirectories
                .Select(directory => Path.Combine(directory, dependencyName))
                .FirstOrDefault(File.Exists);
            return dependencyPath is null ? null : LoadFromAssemblyPath(dependencyPath);
        }
    }
}
