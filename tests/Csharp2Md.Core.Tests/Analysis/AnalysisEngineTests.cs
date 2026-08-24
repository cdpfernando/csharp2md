using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Analysis.Inventory;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Facts.Validation;
using Csharp2Md.Core.Manifests;
using Csharp2Md.Core.Tests.Analysis.DataAccess;
using Csharp2Md.Core.Topic;

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
    public async Task AnalyzeAsync_RelationsBetweenTwoTypes_ProducesDocumentFragmentsWithNoRelationsAndOneSolutionLevelRelationFragment()
    {
        CreateProject(
            "App",
            """
            class Worker
            {
                void Run()
                {
                    Helper helper = new Helper();
                    helper.Do();
                }
            }

            class Helper
            {
                public void Do() { }
            }
            """);

        var result = await new AnalysisEngine().AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        var documentRoot = Path.Combine(TopicLayout.RawRoot(_output), "facts", "document");
        var documentPaths = Directory.EnumerateFiles(documentRoot, "*.json", SearchOption.AllDirectories).ToArray();
        Assert.NotEmpty(documentPaths);
        Assert.All(documentPaths, path =>
            Assert.Empty(FactualJsonSerializer.Deserialize(File.ReadAllBytes(path)).Relations));

        var structuralPath = Path.Combine(TopicLayout.RawRoot(_output), "facts", "relations", "structural.json");
        Assert.True(File.Exists(structuralPath));
        using var structural = JsonDocument.Parse(File.ReadAllText(structuralPath));
        var entries = structural.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Contains(entries, entry => entry.GetProperty("relation_kind").GetString() == "creates");
        Assert.Contains(entries, entry => entry.GetProperty("relation_kind").GetString() == "calls");
    }

    [Fact]
    public async Task AnalyzeAsync_AnUnresolvableRelation_LeavesExitCodeAtZeroDespiteItsC2MRELRDiagnostic()
    {
        CreateProject(
            "App",
            """
            class Worker
            {
                void Run()
                {
                    Nonexistent target = new Nonexistent();
                    target.Go();
                }
            }
            """);

        var result = await new AnalysisEngine().AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("C2M-RELR-", File.ReadAllText(Path.Combine(_output, "raw", "facts", "diagnostics.json")), StringComparison.Ordinal);
        var relationsRoot = Path.Combine(TopicLayout.RawRoot(_output), "facts", "relations");
        var unresolvedEntries = Directory.EnumerateFiles(relationsRoot, "*.json")
            .Where(path => !Path.GetFileName(path).Equals("resolution.json", StringComparison.Ordinal))
            .SelectMany(path =>
            {
                using var partition = JsonDocument.Parse(File.ReadAllText(path));
                return partition.RootElement.GetProperty("entries").EnumerateArray()
                    .Select(entry => entry.GetProperty("resolution_method").GetString())
                    .ToArray();
            });
        Assert.Contains("unresolved", unresolvedEntries);
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

    // COMP-02: the component fragment reaches the manifest exactly like any other pass-two fragment.
    [Fact]
    public async Task AnalyzeAsync_TwoProjects_PersistsOneComponentFragmentIntoTheManifest()
    {
        CreateProject("One", "class A { }");
        CreateProject("Two", "class B { }");

        _ = await new AnalysisEngine().AnalyzeAsync(Request());

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(_output, "raw", "facts", "manifest.json")));
        var fragment = Assert.Single(
            manifest.RootElement.GetProperty("fragments").EnumerateArray(),
            entry => entry.GetProperty("fact_id").GetString()!.StartsWith("id1:component", StringComparison.Ordinal));

        Assert.Equal(64, fragment.GetProperty("sha256").GetString()!.Length);
        Assert.True(fragment.GetProperty("byte_length").GetInt32() > 0);
    }

    // COMP-31: withholding the run's produced project ids from the component fragment's own validation
    // reproduces "a component references a project id the run never produced" - C2M-FV-002 fires and the
    // structural failure moves the exit code, exactly like any other fragment's validation failure would.
    [Fact]
    public async Task AnalyzeAsync_ComponentReferencingAnUnknownProjectId_FailsWithC2MFV002AndExitCodeOne()
    {
        CreateProject("App", "class C { }");
        FactValidationResult Validate(FactValidationInput input) =>
            input.Facts.Any(static fact => fact is ComponentFact)
                ? FactValidator.Validate(input with { KnownFactIds = input.KnownFactIds.Clear() })
                : FactValidator.Validate(input);
        var engine = new AnalysisEngine(new InertInventory(), Validate, null);

        var result = await engine.AnalyzeAsync(Request());

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("C2M-FV-002", File.ReadAllText(Path.Combine(_output, "raw", "facts", "diagnostics.json")), StringComparison.Ordinal);
    }

    // COMP-03: a project reached by three solution paths (fixtures/SyntheticSolution's
    // Acme.Shared.Contracts) still yields exactly one component, mirroring analysedProjects' own dedupe.
    [Fact]
    public async Task AnalyzeAsync_ProjectReachedBySeveralPaths_PersistsExactlyOneComponentPerProject()
    {
        var request = Assert.IsType<AnalysisRequest>(
            AnalysisRequest.Create(TestPaths.SyntheticSolution("."), _output).Request);

        var result = await new AnalysisEngine(new InertInventory(), FactValidator.Validate, null).AnalyzeAsync(request);

        Assert.Equal(0, result.ExitCode);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(_output, "raw", "facts", "manifest.json")));
        var fragmentEntry = Assert.Single(
            manifest.RootElement.GetProperty("fragments").EnumerateArray(),
            entry => entry.GetProperty("fact_id").GetString()!.StartsWith("id1:component", StringComparison.Ordinal));
        var relativePath = fragmentEntry.GetProperty("reference").GetString()!;
        var bytes = File.ReadAllBytes(Path.Combine(_output, "raw", relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var components = FactualJsonSerializer.Deserialize(bytes).Components;

        // Acme.Broken, Acme.DoesNotExist, Acme.Orders, Acme.Payments, Acme.Shared.Contracts.
        Assert.Equal(5, components.Length);
        Assert.Equal(5, components.Select(static component => component.ComponentId).Distinct(StringComparer.Ordinal).Count());
    }

    // COMP-27: ComponentGraphProjector.Project runs on its own statement before CoverageProjector.Project,
    // not inline inside the snapshot constructor after coverage is already computed (the diagnostic-
    // ordering trap design.md documents). Proven end-to-end: a relation whose target is rewritten (via the
    // same validate-interception technique the C2M-FV-002 test above uses) to a FactId shape GraphNodeIndex
    // never indexes must still produce a C2M-CG-001 entry in the real written raw/facts/diagnostics.json -
    // not merely in the projector's in-memory return value - and must not move the exit code.
    [Fact]
    public async Task AnalyzeAsync_ARelationWithAnUnmappableTarget_WritesC2MCG001ToDiagnosticsJsonWithoutChangingExitCode()
    {
        CreateProject(
            "App",
            """
            class Worker
            {
                void Run()
                {
                    Helper helper = new Helper();
                    helper.Do();
                }
            }

            class Helper
            {
                public void Do() { }
            }
            """);

        FactValidationResult Validate(FactValidationInput input)
        {
            var relation = input.Facts.OfType<RelationFact>().FirstOrDefault(static candidate => candidate.TargetId is not null);
            if (relation is null)
            {
                return FactValidator.Validate(input);
            }

            // A RelationFactId is a real, well-formed FactId shape - just not one of the five
            // (project/document/symbol/database object/database column) GraphNodeIndex resolves, so it
            // reproduces "an endpoint mapped to no node" without touching production code.
            var phantomTarget = RelationFactId.Create(relation.SourceId, "phantom", "claim=unmappable-target", 1).ToFactId();
            var rewritten = input.Facts
                .Select(fact => ReferenceEquals(fact, relation) ? relation with { TargetId = phantomTarget } : fact)
                .ToImmutableArray();
            return FactValidator.Validate(input with
            {
                Facts = rewritten,
                KnownFactIds = input.KnownFactIds.Add(phantomTarget),
            });
        }

        var engine = new AnalysisEngine(new InertInventory(), Validate, null);
        var result = await engine.AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        using var diagnostics = JsonDocument.Parse(File.ReadAllText(Path.Combine(_output, "raw", "facts", "diagnostics.json")));
        var entry = Assert.Single(
            diagnostics.RootElement.GetProperty("entries").EnumerateArray(),
            entry => entry.GetProperty("code").GetString() == "C2M-CG-001");
        Assert.Equal("information", entry.GetProperty("severity").GetString());
        Assert.Equal("1", Assert.Single(entry.GetProperty("data").EnumerateArray(), data => data.GetProperty("key").GetString() == "omitted_relation_count")
            .GetProperty("value").GetString());
    }

    // Pass two runs after the document loop, so a table configured in one document resolves an entity
    // declared in another and the data partition is actually written.
    [Fact]
    public async Task AnalyzeAsync_EfCoreProject_WritesTheResolvedDataRelationPartition()
    {
        CreateProjectWithFiles("Orders", EfCoreDocuments());

        var result = await new AnalysisEngine().AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        using var data = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(_output, "raw", "facts", "relations", "data.json")));
        var kinds = data.RootElement.GetProperty("entries").EnumerateArray()
            .Select(entry => entry.GetProperty("relation_kind").GetString())
            .ToArray();
        Assert.Contains("maps-to", kinds);
        Assert.Contains("exposes", kinds);
        Assert.Contains("reads", kinds);
    }

    // The persistence fragment enters the manifest like any other, hash and length included.
    [Fact]
    public async Task AnalyzeAsync_EfCoreProject_PersistsThePersistenceFragmentIntoTheManifest()
    {
        CreateProjectWithFiles("Orders", EfCoreDocuments());

        _ = await new AnalysisEngine().AnalyzeAsync(Request());

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(_output, "raw", "facts", "manifest.json")));
        var fragment = Assert.Single(
            manifest.RootElement.GetProperty("fragments").EnumerateArray(),
            entry => entry.GetProperty("fact_id").GetString()!
                .StartsWith("id1:database-", StringComparison.Ordinal));

        Assert.Equal(64, fragment.GetProperty("sha256").GetString()!.Length);
        Assert.True(fragment.GetProperty("byte_length").GetInt32() > 0);
    }

    // The node catalogue is written from the same run, listing every literal-proven table and column.
    [Fact]
    public async Task AnalyzeAsync_EfCoreProject_WritesTheConfiguredTableAndColumnToTheNodeCatalogue()
    {
        CreateProjectWithFiles("Orders", EfCoreDocuments());

        _ = await new AnalysisEngine().AnalyzeAsync(Request());

        using var catalogue = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(_output, "raw", "facts", "database.json")));
        Assert.Equal("database", catalogue.RootElement.GetProperty("kind").GetString());
        Assert.Equal(2, catalogue.RootElement.GetProperty("schema_version").GetInt32());
        var table = Assert.Single(catalogue.RootElement.GetProperty("objects").EnumerateArray());
        Assert.Equal("tb_order", table.GetProperty("name").GetString());
        Assert.Equal("table", table.GetProperty("kind").GetString());
        Assert.Equal("unknown", table.GetProperty("connection_name").GetString());
        var column = Assert.Single(catalogue.RootElement.GetProperty("columns").EnumerateArray());
        Assert.Equal("order_status", column.GetProperty("name").GetString());
        Assert.Equal(table.GetProperty("object_id").GetString(), column.GetProperty("object_id").GetString());
    }

    // DAD-18: a persistence analyzer throwing mid-run is a diagnostic, not a run failure - contrast
    // with AnalyzeAsync_StructuralValidationFailure_ReturnsExitOneAndOmitsFragment above, where the
    // exit code genuinely does change. Only a structural failure may move it; C2M-DA-001 must not.
    [Fact]
    public async Task AnalyzeAsync_WhenADataAccessAnalyzerThrows_LeavesTheExitCodeUnchangedAndKeepsAnalysingRemainingDocuments()
    {
        CreateProjectWithFiles(
            "App",
            ("Broken.cs", "class Broken { void Run() { } }"),
            ("Fine.cs", "class Fine { void Run() { } }"));
        var failing = StubDataAccessAnalyzer.AppendingThenThrowing(
            "csharp2md.dataaccess.failing", new InvalidOperationException("boom"));
        var engine = new AnalysisEngine(
            new InertInventory(), FactValidator.Validate, null, dataAccessAnalyzers: [failing]);

        var result = await engine.AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        using var diagnostics = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(_output, "raw", "facts", "diagnostics.json")));
        var entries = diagnostics.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal(2, entries.Length);
        Assert.All(entries, entry => Assert.Equal("C2M-DA-001", entry.GetProperty("code").GetString()));
        var scopeIds = entries.Select(entry => entry.GetProperty("scope_id").GetString()).ToArray();
        Assert.Contains(scopeIds, scopeId => scopeId!.Contains("Broken.cs", StringComparison.Ordinal));
        Assert.Contains(scopeIds, scopeId => scopeId!.Contains("Fine.cs", StringComparison.Ordinal));
        Assert.NotEqual(scopeIds[0], scopeIds[1]);

        var documentFragments = Directory.EnumerateFiles(
            Path.Combine(_output, "raw", "facts", "document"), "*.json", SearchOption.AllDirectories);
        var symbolNames = documentFragments
            .SelectMany(path => JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("symbols").EnumerateArray())
            .Select(symbol => symbol.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("Broken", symbolNames);
        Assert.Contains("Fine", symbolNames);
    }

    // Spec Edge Case: a codebase with no persistence API usage records nothing and complains about nothing.
    [Fact]
    public async Task AnalyzeAsync_ProjectWithoutPersistenceCode_WritesAnEmptyDataPartitionAndNoDiagnostic()
    {
        CreateProject("App", "class OrderRepository { void Run() { } }");

        var result = await new AnalysisEngine().AnalyzeAsync(Request());

        Assert.Equal(0, result.ExitCode);
        using var data = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(_output, "raw", "facts", "relations", "data.json")));
        Assert.Empty(data.RootElement.GetProperty("entries").EnumerateArray());
        var diagnostics = File.ReadAllText(Path.Combine(_output, "raw", "facts", "diagnostics.json"));
        Assert.DoesNotContain("C2M-DA-", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("C2M-FV-", diagnostics, StringComparison.Ordinal);
    }

    /// <summary>
    /// The entity, the context exposing and querying it, and a configuration in a third document -
    /// the cross-document shape that forced pass two to run after the document loop. The query sits
    /// beside its context because the claim pass catalogues entity sets per document.
    /// </summary>
    private static (string File, string Source)[] EfCoreDocuments() =>
    [
        ("Order.cs", "public class Order { public int Id { get; set; } public string Status { get; set; } }"),
        ("OrderDbContext.cs", """
            public class OrderDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; }
            }

            public class OrderQueries
            {
                private OrderDbContext _context;

                public object Get(int id) => _context.Orders.Where(order => order.Id == id);
            }
            """),
        ("OrderConfiguration.cs", """
            public class OrderConfiguration
            {
                public void Configure(ModelBuilder builder)
                {
                    builder.Entity<Order>().ToTable("tb_order");
                    builder.Entity<Order>().Property(order => order.Status).HasColumnName("order_status");
                }
            }
            """),
    ];

    private AnalysisRequest Request(AnalysisOptions? options = null) =>
        Assert.IsType<AnalysisRequest>(AnalysisRequest.Create(_input, _output, options: options).Request);

    private void CreateProject(string name, string source)
    {
        var directory = Path.Combine(_input, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{name}.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(directory, "C.cs"), source);
    }

    private void CreateProjectWithFiles(string name, params (string File, string Source)[] documents)
    {
        var directory = Path.Combine(_input, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{name}.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        foreach (var (file, source) in documents)
        {
            File.WriteAllText(Path.Combine(directory, file), source);
        }
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
