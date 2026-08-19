using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Csharp2Md.Core.Analysis.Semantics.Roslyn;
using Csharp2Md.Core.Facts.Identity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Tests.Analysis.Semantics.Roslyn;

[Trait("Category", "Integration")]
public sealed class SemanticCompilationAdapterTests
{
    [Fact]
    public void HealthyTarget_CreatesCompilationAndSemanticModelFromEvaluatedInputs()
    {
        var request = Request("net10.0", "namespace Acme; public sealed class Probe { public string Name => \"ok\"; }");

        var result = new SemanticCompilationAdapter().CreateCompilation(request, CancellationToken.None);

        Assert.Equal(SemanticBindingStatus.Exact, result.Status);
        Assert.NotNull(result.Compilation);
        var binding = Assert.Single(result.Documents);
        Assert.NotNull(binding.SemanticModel);
        var declaration = binding.SyntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        Assert.Equal("Acme.Probe", binding.SemanticModel.GetDeclaredSymbol(declaration)!.ToDisplayString());
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void MultiTargetRequests_CreateDistinctCompilationsAndTargetIdentities()
    {
        var adapter = new SemanticCompilationAdapter();

        var net9 = adapter.CreateCompilation(Request("net9.0", "public sealed class Probe;", "Probe.Net9"), CancellationToken.None);
        var net10 = adapter.CreateCompilation(Request("net10.0", "public sealed class Probe;", "Probe.Net10"), CancellationToken.None);

        Assert.Equal("Probe.Net9", net9.Compilation!.AssemblyName);
        Assert.Equal("Probe.Net10", net10.Compilation!.AssemblyName);
        Assert.NotEqual(net9.TargetId, net10.TargetId);
        Assert.NotSame(net9.Compilation, net10.Compilation);
    }

    [Fact]
    public void EvaluatedConstants_AreAppliedIndependentlyToTargetParsing()
    {
        var properties = Properties("net10.0").SetItem("DefineConstants", "NET10");
        var request = Request(
            "net10.0",
            "#if NET10\npublic sealed class Active;\n#else\npublic sealed class Wrong;\n#endif",
            properties: properties);

        var result = new SemanticCompilationAdapter().CreateCompilation(request, CancellationToken.None);

        var root = Assert.Single(result.Documents).SyntaxTree.GetRoot();
        Assert.Contains(root.DescendantNodes().OfType<ClassDeclarationSyntax>(), declaration => declaration.Identifier.ValueText == "Active");
        Assert.DoesNotContain(root.DescendantNodes().OfType<ClassDeclarationSyntax>(), declaration => declaration.Identifier.ValueText == "Wrong");
    }

    [Fact]
    public void AnalyzerItems_AreExcludedBeforeDirectCompilationAndNeverLoadedAsReferences()
    {
        var analyzer = Path.Combine(Path.GetTempPath(), "MarkerAnalyzer.That.Does.Not.Exist.dll");
        var items = EmptyItems().SetItem("Analyzer", [new EvaluatedItem(analyzer, ImmutableDictionary<string, string>.Empty)]);
        var request = Request("net10.0", "public sealed class Probe;", items: items);

        var result = new SemanticCompilationAdapter().CreateCompilation(request, CancellationToken.None);

        Assert.Equal([analyzer], result.ExcludedAnalyzerPaths.ToArray());
        Assert.Equal(SemanticBindingStatus.Exact, result.Status);
        Assert.NotNull(result.Compilation);
        Assert.DoesNotContain(result.Compilation.References, reference => reference.Display == analyzer);
    }

    [Fact]
    public void InvalidReference_RetainsSourceAndReturnsDegradedCompilation()
    {
        var request = Request("net10.0", "public sealed class Probe : Missing.Namespace.BaseType;");

        var result = new SemanticCompilationAdapter().CreateCompilation(request, CancellationToken.None);

        Assert.Equal(SemanticBindingStatus.Degraded, result.Status);
        Assert.NotNull(result.Compilation);
        Assert.Single(result.Documents);
        Assert.NotNull(result.Documents[0].SemanticModel);
        Assert.Equal("C2M-COMP-003", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ErrorTypeSymbol_RemainsADegradedSemanticResult()
    {
        var request = Request("net10.0", "public sealed class Probe { public MissingType Value = null!; }");

        var result = new SemanticCompilationAdapter().CreateCompilation(request, CancellationToken.None);

        var binding = Assert.Single(result.Documents);
        var type = binding.SyntaxTree.GetRoot().DescendantNodes().OfType<VariableDeclarationSyntax>().Single().Type;
        Assert.Equal(TypeKind.Error, binding.SemanticModel!.GetTypeInfo(type).Type!.TypeKind);
        Assert.Equal(SemanticBindingStatus.Degraded, binding.Status);
        Assert.Equal(SemanticBindingStatus.Degraded, result.Status);
    }

    [Fact]
    public void NullCompilation_ReturnsTargetScopedUnavailableResult()
    {
        var request = Request("net10.0", "public sealed class Probe;");

        var result = new SemanticCompilationAdapter(new NullCompilationFactory())
            .CreateCompilation(request, CancellationToken.None);

        Assert.Null(result.Compilation);
        Assert.Empty(result.Documents);
        Assert.Equal(SemanticBindingStatus.Unavailable, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("C2M-COMP-002", diagnostic.Code);
        Assert.Equal(request.Target.TargetId.ToFactId(), diagnostic.ScopeId);
    }

    [Fact]
    public void NullSemanticModel_DegradesOnlyItsDocumentScope()
    {
        var request = Request("net10.0", "public sealed class Probe;");

        var result = new SemanticCompilationAdapter(new NullModelFactory())
            .CreateCompilation(request, CancellationToken.None);

        Assert.NotNull(result.Compilation);
        var binding = Assert.Single(result.Documents);
        Assert.Null(binding.SemanticModel);
        Assert.Equal(SemanticBindingStatus.Unavailable, binding.Status);
        var diagnostic = Assert.Single(binding.Diagnostics);
        Assert.Equal("C2M-COMP-004", diagnostic.Code);
        Assert.Equal(request.Documents[0].DocumentId.ToFactId(), diagnostic.ScopeId);
    }

    [Fact]
    public void ThrowingSemanticModel_DegradesOnlyFailingDocumentAndRetainsOtherBinding()
    {
        var request = Request(
            "net10.0",
            ("Bad.cs", "public sealed class Bad;"),
            ("Good.cs", "public sealed class Good;"));

        var result = new SemanticCompilationAdapter(new SelectivelyThrowingModelFactory())
            .CreateCompilation(request, CancellationToken.None);

        Assert.Equal(SemanticBindingStatus.Unavailable, result.Documents.Single(document => document.SyntaxTree.FilePath == "Bad.cs").Status);
        Assert.Equal(SemanticBindingStatus.Exact, result.Documents.Single(document => document.SyntaxTree.FilePath == "Good.cs").Status);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("C2M-COMP-005", diagnostic.Code);
        Assert.Equal(request.Documents.Single(document => document.RelativePath == "Bad.cs").DocumentId.ToFactId(), diagnostic.ScopeId);
    }

    [Fact]
    public void Cancellation_StopsBeforeCompilationConstruction()
    {
        var factory = new RecordingFactory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = Assert.ThrowsAny<OperationCanceledException>(() =>
            new SemanticCompilationAdapter(factory).CreateCompilation(
                Request("net10.0", "public sealed class Probe;"),
                cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, factory.CreateCalls);
    }

    private static SemanticCompilationRequest Request(
        string targetFramework,
        string source,
        string assemblyName = "Probe",
        ImmutableDictionary<string, string>? properties = null,
        ImmutableDictionary<string, ImmutableArray<EvaluatedItem>>? items = null) =>
        Request(targetFramework, assemblyName, properties, items, ("Probe.cs", source));

    private static SemanticCompilationRequest Request(
        string targetFramework,
        params (string Path, string Source)[] documents) =>
        Request(targetFramework, "Probe", properties: null, items: null, documents);

    private static SemanticCompilationRequest Request(
        string targetFramework,
        string assemblyName,
        ImmutableDictionary<string, string>? properties,
        ImmutableDictionary<string, ImmutableArray<EvaluatedItem>>? items,
        params (string Path, string Source)[] documents)
    {
        var projectId = ProjectFactId.Create("Probe.csproj");
        var targetId = TargetFactId.Create(projectId, targetFramework);
        var target = new EvaluatedTarget(
            targetId,
            targetFramework,
            Succeeded: true,
            properties ?? Properties(targetFramework),
            items ?? EmptyItems(),
            []);
        return new SemanticCompilationRequest(
            target,
            assemblyName,
            documents.Select(document => new SemanticSourceDocument(
                    DocumentFactId.Create(projectId, document.Path),
                    document.Path,
                    document.Source))
                .ToImmutableArray());
    }

    private static ImmutableDictionary<string, string> Properties(string targetFramework) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TargetFramework"] = targetFramework,
            ["OutputType"] = "Library",
            ["DefineConstants"] = string.Empty,
            ["LangVersion"] = "preview",
            ["Nullable"] = "enable",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    private static ImmutableDictionary<string, ImmutableArray<EvaluatedItem>> EmptyItems() =>
        new Dictionary<string, ImmutableArray<EvaluatedItem>>(StringComparer.Ordinal)
        {
            ["Reference"] = [],
            ["Analyzer"] = [],
        }.ToImmutableDictionary(StringComparer.Ordinal);

    private class DelegatingFactory : ICSharpCompilationFactory
    {
        public virtual CSharpCompilation? Create(
            string assemblyName,
            IEnumerable<SyntaxTree> syntaxTrees,
            IEnumerable<MetadataReference> references,
            CSharpCompilationOptions options) =>
            CSharpCompilation.Create(assemblyName, syntaxTrees, references, options);

        public virtual SemanticModel? GetSemanticModel(CSharpCompilation compilation, SyntaxTree syntaxTree) =>
            compilation.GetSemanticModel(syntaxTree);
    }

    private sealed class NullCompilationFactory : DelegatingFactory
    {
        public override CSharpCompilation? Create(
            string assemblyName,
            IEnumerable<SyntaxTree> syntaxTrees,
            IEnumerable<MetadataReference> references,
            CSharpCompilationOptions options) => null;
    }

    private sealed class NullModelFactory : DelegatingFactory
    {
        public override SemanticModel? GetSemanticModel(CSharpCompilation compilation, SyntaxTree syntaxTree) => null;
    }

    private sealed class SelectivelyThrowingModelFactory : DelegatingFactory
    {
        public override SemanticModel? GetSemanticModel(CSharpCompilation compilation, SyntaxTree syntaxTree) =>
            syntaxTree.FilePath == "Bad.cs"
                ? throw new InvalidOperationException("controlled model failure")
                : base.GetSemanticModel(compilation, syntaxTree);
    }

    private sealed class RecordingFactory : DelegatingFactory
    {
        public int CreateCalls { get; private set; }

        public override CSharpCompilation? Create(
            string assemblyName,
            IEnumerable<SyntaxTree> syntaxTrees,
            IEnumerable<MetadataReference> references,
            CSharpCompilationOptions options)
        {
            CreateCalls++;
            return base.Create(assemblyName, syntaxTrees, references, options);
        }
    }
}
