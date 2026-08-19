using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Analysis.Inventory;
using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Csharp2Md.Core.Analysis.Semantics.Roslyn;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Tests.Analysis;

[Trait("Category", "Integration")]
public sealed class TrustedSemanticAnalysisEngineTests : IDisposable
{
    private readonly string _input = Directory.CreateTempSubdirectory("csharp2md-semantic-engine-input-").FullName;
    private readonly string _output = Path.Combine(Path.GetTempPath(), $"csharp2md-semantic-engine-output-{Guid.NewGuid():N}");

    public void Dispose()
    {
        Directory.Delete(_input, recursive: true);
        if (Directory.Exists(_output)) Directory.Delete(_output, recursive: true);
    }

    [Fact]
    public async Task HealthyTarget_ProducesExactSemanticProjectAndSymbolFacts()
    {
        CreateProject("class C { public int Value { get; } }");

        var result = await Engine(Evaluator(Healthy)).AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(AnalysisMode.Semantic, result.EffectiveMode);
        Assert.Contains("\"resolution\": \"exact\"", AllFactJson(), StringComparison.Ordinal);
        Assert.Contains("T%3AC", AllFactJson(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluationException_DegradesToSyntaxAndReturnsZero()
    {
        CreateProject("class C { }");
        var evaluator = Evaluator((Func<TrustedProjectEvaluationRequest, CancellationToken, ProjectEvaluationResult>)
            ((_, _) => throw new InvalidOperationException("unsafe detail")));

        var result = await Engine(evaluator).AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(AnalysisMode.SyntaxOnly, result.EffectiveMode);
        Assert.Contains(result.Diagnostics, value => value.Contains("evaluation failed", StringComparison.OrdinalIgnoreCase));
        Assert.True(DocumentMarkdownExists("C.cs"));
    }

    [Fact]
    public async Task EvaluationTimeout_DegradesToSyntaxAndReturnsZero()
    {
        CreateProject("class C { }");
        var evaluator = Evaluator((request, _) => Task.FromResult(FailedEvaluation(request.ProjectId, timedOut: true)));

        var result = await Engine(evaluator).AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(AnalysisMode.SyntaxOnly, result.EffectiveMode);
        Assert.Contains("C2M-EVAL-004", File.ReadAllText(Path.Combine(_output, "raw", "facts", "diagnostics.json")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesWithoutBecomingFallback()
    {
        CreateProject("class C { }");
        var evaluator = Evaluator(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Engine(evaluator).AnalyzeAsync(Request(), cancellation.Token));
    }

    [Fact]
    public async Task CompilationException_DegradesOnlyDocumentAndRetainsSyntaxOutput()
    {
        CreateProject("class C { }");

        var result = await Engine(Evaluator(Healthy), compilation: new ThrowingCompilation()).AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(AnalysisMode.Semantic, result.EffectiveMode);
        Assert.True(DocumentMarkdownExists("C.cs"));
        Assert.Contains("C2M-ENGINE-002", DiagnosticsJson(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingSemanticBinding_DegradesOnlyMissingDocument()
    {
        CreateProject("class C { }");
        File.WriteAllText(Path.Combine(_input, "App", "Other.cs"), "class Other { }");

        var result = await Engine(Evaluator(Healthy), compilation: new DropLastBindingCompilation()).AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        Assert.True(DocumentMarkdownExists("C.cs"));
        Assert.True(DocumentMarkdownExists("Other.cs"));
        Assert.Contains("C2M-ENGINE-004", DiagnosticsJson(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratorsDisabled_NeverInvokesGeneratorAdapter()
    {
        CreateProject("class C { }");
        var generator = new RecordingGenerator();

        _ = await Engine(Evaluator(HealthyWithGenerator), generator: generator).AnalyzeAsync(Request());

        Assert.Equal(0, generator.Calls);
    }

    [Fact]
    public async Task GeneratorsEnabled_RecordsLoadedExtension()
    {
        CreateProject("class C { }");
        var generator = new RecordingGenerator();

        var result = await Engine(Evaluator(HealthyWithGenerator), generator: generator)
            .AnalyzeAsync(Request(includeGenerators: true));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, generator.Calls);
        Assert.Contains("Fake.Generator", File.ReadAllText(Path.Combine(_output, "raw", "facts", "manifest.json")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratorException_DegradesGeneratorScopeAndReturnsZero()
    {
        CreateProject("class C { }");
        var generator = new RecordingGenerator { Exception = new InvalidOperationException("boom") };

        var result = await Engine(Evaluator(HealthyWithGenerator), generator: generator)
            .AnalyzeAsync(Request(includeGenerators: true));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("C2M-ENGINE-003", DiagnosticsJson(), StringComparison.Ordinal);
        Assert.True(DocumentMarkdownExists("C.cs"));
    }

    [Fact]
    public async Task MultiTargetProject_PersistsDistinctTargetFacts()
    {
        CreateProject("class C { }");
        var evaluator = Evaluator((request, _) => Task.FromResult(Evaluation(
            request.ProjectId,
            Target(request, "net9.0"),
            Target(request, "net10.0"))));

        var result = await Engine(evaluator).AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        var facts = AllFactJson();
        Assert.Contains("net9.0", facts, StringComparison.Ordinal);
        Assert.Contains("net10.0", facts, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SemanticTargetScopes_AreSequentialAndNeverOverlap()
    {
        CreateProject("class C { }");
        var observer = new SemanticScopeObserver();
        var evaluator = Evaluator((request, _) => Task.FromResult(Evaluation(
            request.ProjectId,
            Target(request, "net9.0"),
            Target(request, "net10.0"))));

        _ = await Engine(evaluator, observer: observer).AnalyzeAsync(Request());

        Assert.Equal(1, observer.MaximumActiveSemanticScopes);
        Assert.Equal(0, observer.ActiveSemanticScopes);
        Assert.Equal(2, observer.CompletedSemanticScopes);
    }

    [Fact]
    public async Task SourceExcludedFromCompileItems_IsPersistedWithNotAttemptedCoverage()
    {
        CreateProject("class C { }");
        File.WriteAllText(Path.Combine(_input, "App", "Excluded.cs"), "class Excluded { }");
        var evaluator = Evaluator((request, _) => Task.FromResult(Evaluation(
            request.ProjectId,
            Target(request, "net10.0", [Path.Combine(_input, "App", "C.cs")]))));

        var result = await Engine(evaluator).AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        Assert.True(DocumentMarkdownExists("Excluded.cs"));
        using var coverage = JsonDocument.Parse(File.ReadAllText(Path.Combine(_output, "raw", "facts", "coverage.json")));
        var excluded = coverage.RootElement.GetProperty("entries").EnumerateArray()
            .Single(entry => entry.GetProperty("scope_id").GetString()!.Contains("Excluded.cs", StringComparison.Ordinal));
        Assert.Equal("not-attempted", excluded.GetProperty("attempt").GetString());
    }

    [Fact]
    public async Task MixedTargetFailure_PreservesHealthyTargetAndPartialProject()
    {
        CreateProject("class C { }");
        var evaluator = Evaluator((request, _) =>
        {
            var failedId = TargetFactId.Create(request.ProjectId, "net9.0");
            var failed = new EvaluatedTarget(failedId, "net9.0", false, EmptyProperties(), EmptyItems(),
                [Diagnostic("C2M-EVAL-002", failedId.ToFactId(), DiagnosticStage.Evaluation)]);
            return Task.FromResult(Evaluation(request.ProjectId, Target(request, "net10.0"), failed));
        });

        var result = await Engine(evaluator).AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(AnalysisMode.Semantic, result.EffectiveMode);
        Assert.Contains("\"resolution\": \"partial\"", AllFactJson(), StringComparison.Ordinal);
        Assert.Contains("C2M-EVAL-002", DiagnosticsJson(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SyntaxOnlyMode_DoesNotInvokeAnySemanticAdapter()
    {
        CreateProject("class C { }");
        var evaluator = Evaluator((Func<TrustedProjectEvaluationRequest, CancellationToken, ProjectEvaluationResult>)
            ((_, _) => throw new InvalidOperationException("must not execute")));
        var generator = new RecordingGenerator { Exception = new InvalidOperationException("must not execute") };

        var result = await Engine(evaluator, new ThrowingCompilation(), generator).AnalyzeAsync(SyntaxRequest());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(AnalysisMode.SyntaxOnly, result.EffectiveMode);
        Assert.Equal(0, generator.Calls);
    }

    private AnalysisEngine Engine(
        IProjectEvaluationAdapter evaluator,
        ISemanticCompilationAdapter? compilation = null,
        ISourceGeneratorAdapter? generator = null,
        IAnalysisEngineObserver? observer = null) =>
        new(
            new InertInventory(),
            FactValidator.Validate,
            observer,
            evaluator,
            compilation ?? new SemanticCompilationAdapter(),
            generator ?? new RecordingGenerator());

    private AnalysisRequest Request(bool includeGenerators = false) =>
        Assert.IsType<AnalysisRequest>(AnalysisRequest.Create(
            _input,
            _output,
            options: new AnalysisOptions
            {
                Mode = AnalysisMode.Semantic,
                Trust = TrustMode.TrustedSolution,
                IncludeSourceGenerators = includeGenerators,
            }).Request);

    private AnalysisRequest SyntaxRequest() =>
        Assert.IsType<AnalysisRequest>(AnalysisRequest.Create(_input, _output).Request);

    private void CreateProject(string source)
    {
        var directory = Path.Combine(_input, "App");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(directory, "C.cs"), source);
    }

    private bool DocumentMarkdownExists(string fileName) =>
        File.Exists(Path.Combine(_output, "raw", "codebase", "App", fileName + ".md"));

    private string DiagnosticsJson() => File.ReadAllText(Path.Combine(_output, "raw", "facts", "diagnostics.json"));

    private string AllFactJson() => string.Join('\n',
        Directory.EnumerateFiles(Path.Combine(_output, "raw", "facts"), "*.json", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

    private ProjectEvaluationResult Healthy(TrustedProjectEvaluationRequest request, CancellationToken _) =>
        Evaluation(request.ProjectId, Target(request, "net10.0"));

    private ProjectEvaluationResult HealthyWithGenerator(TrustedProjectEvaluationRequest request, CancellationToken _) =>
        Evaluation(request.ProjectId, [typeof(TrustedSemanticAnalysisEngineTests).Assembly.Location], Target(request, "net10.0"));

    private EvaluatedTarget Target(
        TrustedProjectEvaluationRequest request,
        string targetFramework,
        IEnumerable<string>? compilePaths = null)
    {
        compilePaths ??= Directory.EnumerateFiles(Path.GetDirectoryName(request.ProjectPath)!, "*.cs");
        var items = EmptyItems()
            .Add("Compile", compilePaths.Select(path => Item(path, ("FullPath", Path.GetFullPath(path)))).ToImmutableArray())
            .Add("Reference", [Item(typeof(object).Assembly.Location)]);
        return new EvaluatedTarget(
            TargetFactId.Create(request.ProjectId, targetFramework),
            targetFramework,
            true,
            EmptyProperties()
                .Add("AssemblyName", "App")
                .Add("OutputType", "Library")
                .Add("RootNamespace", "App")
                .Add("LangVersion", "preview")
                .Add("Nullable", "enable"),
            items,
            []);
    }

    private static ProjectEvaluationResult Evaluation(ProjectFactId projectId, params EvaluatedTarget[] targets) =>
        Evaluation(projectId, [], targets);

    private static ProjectEvaluationResult Evaluation(
        ProjectFactId projectId,
        ImmutableArray<string> generators,
        params EvaluatedTarget[] targets) =>
        new(projectId, "Microsoft.NET.Sdk", targets.Select(static target => target.TargetFramework).ToImmutableArray(),
            [], [], generators, [.. targets], [], TimedOut: false);

    private static ProjectEvaluationResult FailedEvaluation(ProjectFactId projectId, bool timedOut)
    {
        var diagnostic = Diagnostic("C2M-EVAL-004", projectId.ToFactId(), DiagnosticStage.Evaluation);
        return new ProjectEvaluationResult(projectId, "", [], [], [], [], [], [diagnostic], timedOut);
    }

    private static AnalysisDiagnostic Diagnostic(string code, FactId scopeId, DiagnosticStage stage) =>
        AnalysisDiagnostic.Create(code, DiagnosticSeverity.Warning, stage, scopeId, "Controlled semantic failure.");

    private static EvaluatedItem Item(string identity, params (string Key, string Value)[] metadata) =>
        new(identity, metadata.ToImmutableDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal));

    private static ImmutableDictionary<string, string> EmptyProperties() =>
        ImmutableDictionary.Create<string, string>(StringComparer.Ordinal);

    private static ImmutableDictionary<string, ImmutableArray<EvaluatedItem>> EmptyItems() =>
        ImmutableDictionary.Create<string, ImmutableArray<EvaluatedItem>>(StringComparer.Ordinal);

    private static IProjectEvaluationAdapter Evaluator(
        Func<TrustedProjectEvaluationRequest, CancellationToken, ProjectEvaluationResult> evaluate) =>
        new StubEvaluator((request, cancellationToken) => Task.FromResult(evaluate(request, cancellationToken)));

    private static IProjectEvaluationAdapter Evaluator(
        Func<TrustedProjectEvaluationRequest, CancellationToken, Task<ProjectEvaluationResult>> evaluate) =>
        new StubEvaluator(evaluate);

    private sealed class StubEvaluator(
        Func<TrustedProjectEvaluationRequest, CancellationToken, Task<ProjectEvaluationResult>> evaluate)
        : IProjectEvaluationAdapter
    {
        public Task<ProjectEvaluationResult> EvaluateAsync(
            TrustedProjectEvaluationRequest request,
            CancellationToken cancellationToken) => evaluate(request, cancellationToken);
    }

    private sealed class ThrowingCompilation : ISemanticCompilationAdapter
    {
        public SemanticCompilationResult CreateCompilation(SemanticCompilationRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("controlled compilation failure");
    }

    private sealed class DropLastBindingCompilation : ISemanticCompilationAdapter
    {
        public SemanticCompilationResult CreateCompilation(SemanticCompilationRequest request, CancellationToken cancellationToken)
        {
            var result = new SemanticCompilationAdapter().CreateCompilation(request, cancellationToken);
            return result with { Documents = result.Documents.Take(1).ToImmutableArray() };
        }
    }

    private sealed class RecordingGenerator : ISourceGeneratorAdapter
    {
        public int Calls { get; private set; }
        public Exception? Exception { get; init; }

        public SourceGeneratorExecutionResult Run(SourceGeneratorExecutionRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Exception is not null) throw Exception;
            return new SourceGeneratorExecutionResult(request.SemanticCompilation.Compilation, [], ["Fake.Generator"], []);
        }
    }

    private sealed class SemanticScopeObserver : IAnalysisEngineObserver
    {
        public int ActiveSemanticScopes { get; private set; }
        public int MaximumActiveSemanticScopes { get; private set; }
        public int CompletedSemanticScopes { get; private set; }

        public void ScopeStarted(string scope)
        {
            if (!scope.StartsWith("semantic:", StringComparison.Ordinal)) return;
            ActiveSemanticScopes++;
            MaximumActiveSemanticScopes = Math.Max(MaximumActiveSemanticScopes, ActiveSemanticScopes);
        }

        public void ScopeCompleted(string scope)
        {
            if (!scope.StartsWith("semantic:", StringComparison.Ordinal)) return;
            ActiveSemanticScopes--;
            CompletedSemanticScopes++;
        }
    }
}
