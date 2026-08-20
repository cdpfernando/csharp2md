using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Analysis.Inventory;
using Csharp2Md.Core.Facts.Validation;
using Csharp2Md.Core.Manifests;

namespace Csharp2Md.Core.Tests.Analysis;

[Trait("Category", "Integration")]
public sealed class AnalysisEngineTests : IDisposable
{
    private readonly string _input = Directory.CreateTempSubdirectory("csharp2md-engine-input-").FullName;
    private readonly string _output = Path.Combine(Path.GetTempPath(), $"csharp2md-engine-output-{Guid.NewGuid():N}");

    public void Dispose()
    {
        Directory.Delete(_input, recursive: true);
        if (Directory.Exists(_output)) Directory.Delete(_output, recursive: true);
    }

    [Fact]
    public async Task AnalyzeAsync_DefaultSyntaxOnly_WritesFactsAndMarkdown()
    {
        CreateProject("App", "class C { void Run() { } }");

        var result = await new AnalysisEngine().AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(AnalysisMode.SyntaxOnly, result.EffectiveMode);
        Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(_output, "raw", "facts", "document"), "*.json", SearchOption.AllDirectories));
        Assert.True(File.Exists(Path.Combine(_output, "raw", "codebase", "App", "C.cs.md")));
    }

    [Fact]
    public async Task AnalyzeAsync_BrokenUnrestoredProject_UsesCompleteSyntaxFallback()
    {
        CreateProject("Broken", "class Broken {");
        File.WriteAllText(Path.Combine(_input, "Broken", "Broken.csproj"), "<Project Sdk=\"Missing.Sdk/99\"><Broken>");

        var result = await new AnalysisEngine().AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("1 document(s)", result.Summary, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_output, "raw", "codebase", "Broken", "C.cs.md")));
    }

    [Fact]
    public async Task AnalyzeAsync_SyntaxOnly_InvokesNoExecutableInventoryAdapter()
    {
        CreateProject("App", "class C { }");
        var executable = new RecordingExecutableObserver();
        var engine = new AnalysisEngine(new InertInventory(executable), FactValidator.Validate, null);

        _ = await engine.AnalyzeAsync(Request());

        Assert.Empty(executable.Invocations);
    }

    [Fact]
    public async Task AnalyzeAsync_ScopesAreCanonicalSequentialAndNonOverlapping()
    {
        CreateProject("Zulu", "class Z { }");
        CreateProject("Alpha", "class A { }");
        var observer = new SequentialObserver();
        var engine = new AnalysisEngine(new InertInventory(), FactValidator.Validate, observer);

        _ = await engine.AnalyzeAsync(Request());

        Assert.Empty(observer.ActiveScopes);
        Assert.True(observer.MaxDocuments == 1);
        Assert.True(observer.Events.FindIndex(value => value.Contains("project:Alpha/Alpha.csproj", StringComparison.Ordinal))
            < observer.Events.FindIndex(value => value.Contains("project:Zulu/Zulu.csproj", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AnalyzeAsync_StructuralValidationFailure_ReturnsExitOneAndOmitsFragment()
    {
        CreateProject("App", "class C { }");
        var engine = new AnalysisEngine(new InertInventory(), _ => new FactValidationResult(null, []), null);

        var result = await engine.AnalyzeAsync(Request());

        Assert.Equal(1, result.ExitCode);
        var documentFacts = Path.Combine(_output, "raw", "facts", "document");
        Assert.True(!Directory.Exists(documentFacts) || !Directory.EnumerateFiles(documentFacts, "*.json", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task AnalyzeAsync_PreparesOutputOnceWithoutDeletingPersistedFragments()
    {
        CreateProject("App", "class C { }");

        _ = await new AnalysisEngine().AnalyzeAsync(Request());

        Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(_output, "raw", "facts"), "*.json", SearchOption.AllDirectories));
        Assert.True(File.Exists(Path.Combine(_output, "raw", "facts", "manifest.json")));
    }

    [Fact]
    public async Task AnalyzeAsync_ManifestInput_ResolvesRelativeServicePath()
    {
        CreateProject("App", "class C { }");
        var manifestPath = Path.Combine(_input, "csharp2md.json");
        File.WriteAllText(manifestPath, "{\"services\":[{\"path\":\"App\"}]}");
        var request = Assert.IsType<AnalysisRequest>(AnalysisRequest.Create(manifestPath, _output).Request);

        var result = await new AnalysisEngine().AnalyzeAsync(request);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("1 project(s)", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeAsync_CancelledBeforeStart_ThrowsWithoutOutput()
    {
        CreateProject("App", "class C { }");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => new AnalysisEngine().AnalyzeAsync(Request(), cancellation.Token));
        Assert.False(Directory.Exists(_output));
    }

    [Fact]
    public async Task AnalyzeAsync_TrustedSemanticProjectWithoutTargets_RetainsValidSyntaxFacts()
    {
        CreateProject("App", "class C { }");
        var options = new AnalysisOptions { Mode = AnalysisMode.Semantic, Trust = TrustMode.TrustedSolution };

        var result = await new AnalysisEngine().AnalyzeAsync(Request(options));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(AnalysisMode.SyntaxOnly, result.EffectiveMode);
        Assert.True(File.Exists(Path.Combine(_output, "raw", "codebase", "App", "C.cs.md")));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("no evaluable targets", StringComparison.Ordinal));
        Assert.Contains("C2M-ENGINE-006", File.ReadAllText(Path.Combine(_output, "raw", "facts", "diagnostics.json")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeAsync_ManifestCoverageIncludesEveryInventoriedScope()
    {
        CreateProject("One", "class A { }");
        CreateProject("Two", "class B { }");

        _ = await new AnalysisEngine().AnalyzeAsync(Request());
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(_output, "raw", "facts", "manifest.json")));
        var coverage = manifest.RootElement.GetProperty("coverage");

        Assert.Equal(2, coverage.GetProperty("services").GetInt32());
        Assert.Equal(2, coverage.GetProperty("projects").GetInt32());
        Assert.Equal(2, coverage.GetProperty("documents").GetInt32());
    }

    [Fact]
    public async Task AnalyzeAsync_ProjectReachedBySeveralPaths_IsAnalysedOnceAndTheRunCompletes()
    {
        // fixtures/SyntheticSolution has no top-level solution, so every project directory becomes its
        // own service. Acme.Orders.slnx and Acme.Payments.slnx both list Acme.Shared.Contracts, and it
        // is a service root in its own right - three paths reach the same project.
        var observer = new SequentialObserver();
        var request = Assert.IsType<AnalysisRequest>(
            AnalysisRequest.Create(TestPaths.SyntheticSolution("."), _output).Request);

        var result = await new AnalysisEngine(new InertInventory(), FactValidator.Validate, observer)
            .AnalyzeAsync(request);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, observer.Events.Count(value =>
            value == "start:project:Acme.Shared.Contracts/Acme.Shared.Contracts.csproj"));
        // Acme.Broken, Acme.DoesNotExist, Acme.Orders, Acme.Payments, Acme.Shared.Contracts.
        Assert.Contains("Analyzed 5 project(s)", result.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Contains("Acme.Shared.Contracts", StringComparison.Ordinal));
    }

    private AnalysisRequest Request(AnalysisOptions? options = null) =>
        Assert.IsType<AnalysisRequest>(AnalysisRequest.Create(_input, _output, options: options).Request);

    private void CreateProject(string name, string source)
    {
        var directory = Path.Combine(_input, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{name}.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(directory, "C.cs"), source);
    }

    private sealed class RecordingExecutableObserver : IInventoryExecutionObserver
    {
        public List<string> Invocations { get; } = [];
        public void ExecutableAdapterInvoked(string adapterKind) => Invocations.Add(adapterKind);
    }

    private sealed class SequentialObserver : IAnalysisEngineObserver
    {
        public List<string> ActiveScopes { get; } = [];
        public List<string> Events { get; } = [];
        public int MaxDocuments { get; private set; }

        public void ScopeStarted(string scope)
        {
            Events.Add("start:" + scope);
            ActiveScopes.Add(scope);
            MaxDocuments = Math.Max(MaxDocuments, ActiveScopes.Count(static value => value.StartsWith("document:", StringComparison.Ordinal)));
        }

        public void ScopeCompleted(string scope)
        {
            Events.Add("end:" + scope);
            Assert.True(ActiveScopes.Remove(scope));
        }
    }
}
