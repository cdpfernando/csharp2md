using Csharp2Md.Core.Analysis.Semantics;
using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Csharp2Md.Core.Analysis.Semantics.Roslyn;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Facts.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Tests.Analysis.Semantics;

[Trait("Category", "Integration")]
public sealed class SymbolFactEnricherTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/Probe/Probe.csproj");
    private static readonly TargetFactId TargetId = TargetFactId.Create(ProjectId, "net10.0");

    [Fact]
    public void Overloads_ReceiveDistinctStableDocumentationIdentities()
    {
        var result = Enrich("namespace Acme; public class Api { public void Run(int value) { } public void Run(string value) { } }");

        var methods = result.Symbols.Where(static symbol => symbol.SymbolKind == "method").ToArray();
        Assert.Equal(2, methods.Length);
        Assert.All(methods, static method => Assert.Equal(FactResolution.Exact, method.Header.Resolution));
        Assert.Equal(2, methods.Select(static method => method.SymbolId).Distinct().Count());
        Assert.Contains(methods, method => method.SymbolId.Value.Contains("System.Int32", StringComparison.Ordinal));
        Assert.Contains(methods, method => method.SymbolId.Value.Contains("System.String", StringComparison.Ordinal));
    }

    [Fact]
    public void GenericTypeAndMethod_RetainArityAndResolvedTypeReferences()
    {
        var result = Enrich("namespace Acme; public class Box<T> { public T Map<TValue>(TValue value) => default!; }");

        var type = result.Symbols.Single(static symbol => symbol.SymbolKind == "class");
        var method = result.Symbols.Single(static symbol => symbol.SymbolKind == "method");
        Assert.Contains("Box%601", type.SymbolId.Value, StringComparison.Ordinal);
        Assert.Contains("Map%60%601", method.SymbolId.Value, StringComparison.Ordinal);
        Assert.Contains("global::T", method.RelevantTypeReferences);
        Assert.Contains("global::TValue", method.RelevantTypeReferences);
    }

    [Fact]
    public void RecordDeclaration_IsBoundAsAnExactResolvedSymbol()
    {
        var result = Enrich("namespace Acme; public sealed record Payment(string Id);");

        var record = result.Symbols.Single(static symbol => symbol.SymbolKind == "record");
        Assert.Equal(FactResolution.Exact, record.Header.Resolution);
        Assert.Contains("T%3AAcme.Payment", record.SymbolId.Value, StringComparison.Ordinal);
        Assert.False(record.ContainsErrorSymbol);
    }

    [Fact]
    public void LocalBaseAndInterfaceTypes_AreRecordedByResolvedIdentity()
    {
        var result = Enrich("namespace Acme; public class Base { } public interface IProbe { } public sealed class Probe : Base, IProbe { }");

        var baseType = result.Symbols.Single(symbol => symbol.SymbolId.Value.Contains("T%3AAcme.Base", StringComparison.Ordinal));
        var contract = result.Symbols.Single(symbol => symbol.SymbolId.Value.Contains("T%3AAcme.IProbe", StringComparison.Ordinal));
        var implementation = result.Symbols.Single(symbol => symbol.SymbolId.Value.Contains("T%3AAcme.Probe", StringComparison.Ordinal));
        Assert.Equal([baseType.SymbolId, contract.SymbolId], implementation.BaseAndInterfaceIds.ToArray());
    }

    [Fact]
    public void ImplicitInterfaceImplementation_ReferencesTheInterfaceMember()
    {
        var result = Enrich("namespace Acme; public interface IProbe { string Run(int value); } public sealed class Probe : IProbe { public string Run(int value) => value.ToString(); }");

        var contract = result.Symbols.Single(symbol => symbol.SymbolKind == "method" && symbol.SymbolId.Value.Contains("IProbe.Run", StringComparison.Ordinal));
        var implementation = result.Symbols.Single(symbol => symbol.SymbolKind == "method" && symbol.SymbolId.Value.Contains("M%3AAcme.Probe.Run", StringComparison.Ordinal));
        Assert.Equal([contract.SymbolId], implementation.Semantics!.ImplementedMemberIds.ToArray());
    }

    [Fact]
    public void Override_ReferencesTheOverriddenLocalMember()
    {
        var result = Enrich("namespace Acme; public class Base { public virtual string Run() => \"base\"; } public sealed class Probe : Base { public override string Run() => \"probe\"; }");

        var baseMethod = result.Symbols.Single(symbol => symbol.SymbolKind == "method" && symbol.SymbolId.Value.Contains("Base.Run", StringComparison.Ordinal));
        var overrideMethod = result.Symbols.Single(symbol => symbol.SymbolKind == "method" && symbol.SymbolId.Value.Contains("Probe.Run", StringComparison.Ordinal));
        Assert.Equal(baseMethod.SymbolId, overrideMethod.Semantics!.OverriddenMemberId);
    }

    [Fact]
    public void Attributes_AreResolvedToFullyQualifiedAttributeTypes()
    {
        var result = Enrich("namespace Acme; [System.Obsolete] public sealed class Probe { }");

        var type = result.Symbols.Single(static symbol => symbol.SymbolKind == "class");
        Assert.Equal(["global::System.ObsoleteAttribute"], type.Attributes.ToArray());
        Assert.Equal(FactResolution.Exact, type.Header.Resolution);
    }

    [Fact]
    public void MethodSignature_UsesResolvedRelevantTypeReferences()
    {
        var result = Enrich("namespace Acme; public sealed class Probe { public System.Threading.Tasks.Task<string> Run(int value) => throw null!; }");

        var method = result.Symbols.Single(static symbol => symbol.SymbolKind == "method");
        Assert.Equal(
            ["global::System.Int32", "global::System.Threading.Tasks.Task<global::System.String>"],
            method.RelevantTypeReferences.ToArray());
    }

    [Fact]
    public void NullDocumentationId_UsesCanonicalFallbackWithoutSourceLocation()
    {
        var first = Enrich(
            "namespace Acme; public sealed class Probe { public bool TryRun<T>(ref string value) => true; }",
            new NullDocumentationIdProvider());
        var second = Enrich(
            "namespace Acme; public sealed class Probe { public bool TryRun<T>(ref string value) => true; }",
            new NullDocumentationIdProvider());

        var firstMethod = first.Symbols.Single(static symbol => symbol.SymbolKind == "method");
        var secondMethod = second.Symbols.Single(static symbol => symbol.SymbolKind == "method");
        Assert.Equal(firstMethod.SymbolId, secondMethod.SymbolId);
        Assert.Contains("sig1", firstMethod.SymbolId.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("line", firstMethod.SymbolId.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("span", firstMethod.SymbolId.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ErrorTypeSymbol_RetainsSyntacticIdentityAndCannotBecomeExact()
    {
        var result = Enrich("namespace Acme; public sealed class Probe { public MissingType Value = null!; }");

        var field = result.Symbols.Single(static symbol => symbol.SymbolKind == "field");
        Assert.StartsWith("id1:syntactic-symbol", field.SymbolId.Value, StringComparison.Ordinal);
        Assert.Equal(FactResolution.Unresolved, field.Header.Resolution);
        Assert.True(field.ContainsErrorSymbol);
        Assert.Equal("C2M-BIND-002", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(FactResolution.Partial, result.Document.Header.Resolution);
    }

    [Fact]
    public void MissingSemanticModel_RetainsAllSyntaxFactsAndScopesTheDiagnosticToTheDocument()
    {
        const string source = "namespace Acme; public sealed class Probe { public void Run() { } }";
        var input = Compile(source);
        var diagnostic = AnalysisDiagnostic.Create(
            "C2M-COMP-004",
            Csharp2Md.Core.Facts.Metadata.DiagnosticSeverity.Warning,
            DiagnosticStage.Compilation,
            input.Extraction.Document.DocumentId.ToFactId(),
            "Roslyn returned no semantic model for the document.");
        var unavailable = input.Binding with
        {
            SemanticModel = null,
            Status = SemanticBindingStatus.Unavailable,
            Diagnostics = [diagnostic],
        };

        var result = new SymbolFactEnricher().Enrich(
            input.Extraction.Document,
            input.Extraction.Symbols,
            unavailable,
            TargetId,
            CancellationToken.None);

        Assert.Equal(input.Extraction.Symbols.Select(static symbol => symbol.SymbolId), result.Symbols.Select(static symbol => symbol.SymbolId));
        Assert.All(result.Symbols, static symbol => Assert.Equal(FactResolution.Syntactic, symbol.Header.Resolution));
        Assert.Equal(input.Extraction.Document.DocumentId.ToFactId(), Assert.Single(result.Diagnostics).ScopeId);
    }

    [Fact]
    public void DeclaredSymbolFailure_DegradesOnlyItsFactAndRetainsOtherExactSymbols()
    {
        var result = Enrich(
            "namespace Acme; public sealed class Probe { public void Bad() { } public void Good() { } }",
            declaredSymbols: new SelectivelyThrowingDeclaredSymbolProvider());

        var bad = result.Symbols.Single(symbol => symbol.SymbolKind == "method" && symbol.SymbolId.Value.Contains("Bad", StringComparison.Ordinal));
        var good = result.Symbols.Single(symbol => symbol.SymbolKind == "method" && symbol.SymbolId.Value.Contains("Good", StringComparison.Ordinal));
        Assert.Equal(FactResolution.Syntactic, bad.Header.Resolution);
        Assert.Equal(FactResolution.Exact, good.Header.Resolution);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("C2M-BIND-003", diagnostic.Code);
        Assert.Equal(bad.SymbolId.ToFactId(), diagnostic.ScopeId);
        Assert.Equal(FactResolution.Partial, result.Document.Header.Resolution);
    }

    [Fact]
    public void ConditionalCompilation_EnrichesOnlyTheActiveDeclaration()
    {
        var result = Enrich("#if ENABLED\npublic sealed class Enabled { }\n#else\npublic sealed class Disabled { }\n#endif");

        var symbol = Assert.Single(result.Symbols);
        Assert.Contains("Disabled", symbol.SymbolId.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Symbols, candidate => candidate.SymbolId.Value.Contains("Enabled", StringComparison.Ordinal));
        Assert.Equal(FactResolution.Exact, symbol.Header.Resolution);
    }

    [Fact]
    public void SemanticRelationships_SerializeThroughSchemaVersionSixContract()
    {
        var result = Enrich("namespace Acme; public interface IProbe { void Run(); } public sealed class Probe : IProbe { public void Run() { } }");
        var fragment = new ValidatedFactFragment(
            [result.Document, .. result.Symbols],
            result.Diagnostics);

        var mapped = FactualJsonMapper.Map(fragment);

        Assert.Equal(6, mapped.SchemaVersion);
        var implementation = mapped.Symbols.Single(symbol => symbol.SymbolId.Contains("M%3AAcme.Probe.Run", StringComparison.Ordinal));
        Assert.NotNull(implementation.Semantics);
        Assert.Single(implementation.Semantics.ImplementedMemberIds);
        Assert.Null(implementation.Semantics.OverriddenMemberId);
    }

    [Fact]
    public void BoundSymbol_ReportsExactResolutionAndTheRoslynShapeOfEveryIdentityField()
    {
        var result = Enrich("""
            namespace Acme.Payments;
            public sealed class PaymentsService
            {
                public string AuthorizePayment(string orderId, decimal amount) => orderId;
            }
            """);

        var type = result.Symbols.Single(static symbol => symbol.SymbolKind == "class");
        var method = result.Symbols.Single(static symbol => symbol.SymbolKind == "method");

        Assert.Equal(FactResolution.Exact, method.Header.Resolution);
        Assert.Equal("AuthorizePayment", method.Name);
        Assert.Equal("Acme.Payments", method.Namespace);
        Assert.Equal("global::Acme.Payments.PaymentsService", method.ContainingType);
        Assert.Equal("global::Acme.Payments.PaymentsService.AuthorizePayment", method.FullyQualifiedName);
        Assert.Equal(["global::System.String", "global::System.Decimal"], method.ParameterTypes.ToArray());
        Assert.Equal("global::Acme.Payments.PaymentsService", type.FullyQualifiedName);
        Assert.Null(type.ContainingType);
    }

    [Fact]
    public void BoundSymbol_IdentityFieldsAreTheResolvedShapeNotTheSyntaxOnlyGuess()
    {
        const string source = """
            namespace Acme.Payments;
            using Alias = System.String;
            public sealed class PaymentsService
            {
                public void Take(Alias aliased, int count) { }
            }
            """;

        var syntaxOnly = SyntaxFactExtractor.Extract(ProjectId, "Probe.cs", source).Symbols
            .Single(static symbol => symbol.Name == "Take");
        var enriched = Enrich(source).Symbols.Single(static symbol => symbol.Name == "Take");

        // The syntax-only path can only echo the source spelling of a user-defined using alias;
        // semantic binding resolves it to the real type. Both paths agree on the predefined keyword.
        Assert.Equal(["global::Alias", "global::System.Int32"], syntaxOnly.ParameterTypes.ToArray());
        Assert.Equal(["global::System.String", "global::System.Int32"], enriched.ParameterTypes.ToArray());
        Assert.Equal(FactResolution.Exact, enriched.Header.Resolution);
    }

    [Fact]
    public void BoundPredefinedTypeParameter_NormalizesToItsSystemMetadataNameNotTheCSharpKeyword()
    {
        var result = Enrich("namespace Acme; public class Api { public void Run(int count, string name) { } }");

        var method = result.Symbols.Single(static symbol => symbol.SymbolKind == "method");

        Assert.Equal(["global::System.Int32", "global::System.String"], method.ParameterTypes.ToArray());
        Assert.DoesNotContain("global::int", method.ParameterTypes);
        Assert.DoesNotContain("global::string", method.ParameterTypes);
    }

    [Fact]
    public void BoundSymbol_ParameterTypesMatchTheSyntaxOnlyPathForPredefinedKeywordSpellings()
    {
        const string source = "namespace Acme; public class Api { public void Run(int count, string name) { } }";

        var syntaxOnly = SyntaxFactExtractor.Extract(ProjectId, "Probe.cs", source).Symbols
            .Single(static symbol => symbol.Name == "Run");
        var enriched = Enrich(source).Symbols.Single(static symbol => symbol.Name == "Run");

        Assert.Equal(syntaxOnly.ParameterTypes.ToArray(), enriched.ParameterTypes.ToArray());
        Assert.Equal(syntaxOnly.FullyQualifiedName, enriched.FullyQualifiedName);
        Assert.Equal(syntaxOnly.ContainingType, enriched.ContainingType);
    }

    [Fact]
    public void BoundGenericTypeAndMethod_ReportTheirRoslynArity()
    {
        var result = Enrich("namespace Acme; public class Box<T> { public T Map<TValue, TOther>(TValue value) => default!; }");

        var type = result.Symbols.Single(static symbol => symbol.SymbolKind == "class");
        var method = result.Symbols.Single(static symbol => symbol.SymbolKind == "method");

        Assert.Equal(1, type.Arity);
        Assert.Equal(2, method.Arity);
        Assert.Equal("global::Acme.Box", type.FullyQualifiedName);
    }

    [Fact]
    public void BoundMember_ContainingSymbolIdIsItsContainingTypesResolvedIdentity()
    {
        var result = Enrich("namespace Acme; public class Api { public void Run() { } }");

        var type = result.Symbols.Single(static symbol => symbol.SymbolKind == "class");
        var method = result.Symbols.Single(static symbol => symbol.SymbolKind == "method");

        Assert.Equal(type.SymbolId, method.ContainingSymbolId);
        Assert.Contains("T%3AAcme.Api", type.SymbolId.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundMemberWhoseContainerIsNotInTheEnrichmentBatch_KeepsTheSyntaxOnlyContainingSymbolId()
    {
        const string source = "namespace Acme; public class Api { public void Run() { } }";

        var syntaxOnly = SyntaxFactExtractor.Extract(ProjectId, "Probe.cs", source).Symbols
            .Single(static symbol => symbol.Name == "Run");
        var method = Enrich(source, declaredSymbols: new MembersOnlyDeclaredSymbolProvider()).Symbols
            .Single(static symbol => symbol.Name == "Run");

        Assert.Equal(FactResolution.Exact, method.Header.Resolution);
        Assert.NotNull(syntaxOnly.ContainingSymbolId);
        Assert.Equal(syntaxOnly.ContainingSymbolId, method.ContainingSymbolId);
    }

    [Fact]
    public void ErrorBearingBinding_KeepsEverySyntaxOnlyIdentityFieldValue()
    {
        const string source = "namespace Acme; public class Api { public Missing Run(Missing value) => value; }";

        var syntaxOnly = SyntaxFactExtractor.Extract(ProjectId, "Probe.cs", source).Symbols
            .Single(static symbol => symbol.Name == "Run");
        var method = Enrich(source).Symbols.Single(static symbol => symbol.Name == "Run");

        Assert.True(method.ContainsErrorSymbol);
        Assert.NotEqual(FactResolution.Exact, method.Header.Resolution);
        Assert.Equal(syntaxOnly.Name, method.Name);
        Assert.Equal(syntaxOnly.FullyQualifiedName, method.FullyQualifiedName);
        Assert.Equal(syntaxOnly.Namespace, method.Namespace);
        Assert.Equal(syntaxOnly.ContainingType, method.ContainingType);
        Assert.Equal(syntaxOnly.ContainingSymbolId, method.ContainingSymbolId);
        Assert.Equal(syntaxOnly.Arity, method.Arity);
        Assert.Equal(syntaxOnly.ParameterTypes.ToArray(), method.ParameterTypes.ToArray());
    }

    [Fact]
    public void BoundSymbol_SignatureKeepsTheSyntaxOnlyOriginalSpelling()
    {
        const string source = "namespace Acme; public class Api { public void Run(int count) { } }";

        var syntaxOnly = SyntaxFactExtractor.Extract(ProjectId, "Probe.cs", source).Symbols
            .Single(static symbol => symbol.Name == "Run");
        var method = Enrich(source).Symbols.Single(static symbol => symbol.Name == "Run");

        Assert.Equal(syntaxOnly.Signature, method.Signature);
        Assert.Contains("int", method.Signature, StringComparison.Ordinal);
    }

    /// <summary>
    /// Binds members but never type declarations, so a member's containing type is absent from the
    /// enrichment batch and <c>ContainingSymbolId</c> has to fall back to its syntax-only value.
    /// </summary>
    private sealed class MembersOnlyDeclaredSymbolProvider : IDeclaredSymbolProvider
    {
        public ISymbol? GetDeclaredSymbol(
            SemanticModel semanticModel,
            MemberDeclarationSyntax declaration,
            CancellationToken cancellationToken) => declaration is BaseTypeDeclarationSyntax or BaseNamespaceDeclarationSyntax
                ? null
                : semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
    }

    private static SymbolFactEnrichmentResult Enrich(
        string source,
        ISymbolDocumentationIdProvider? documentationIds = null,
        IDeclaredSymbolProvider? declaredSymbols = null)
    {
        var input = Compile(source);
        return new SymbolFactEnricher(documentationIds, declaredSymbols).Enrich(
            input.Extraction.Document,
            input.Extraction.Symbols,
            input.Binding,
            TargetId,
            CancellationToken.None);
    }

    private static CompilationInput Compile(string source)
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "Probe.cs", source);
        var target = new EvaluatedTarget(
            TargetId,
            "net10.0",
            Succeeded: true,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["TargetFramework"] = "net10.0",
                ["OutputType"] = "Library",
                ["DefineConstants"] = string.Empty,
                ["LangVersion"] = "preview",
                ["Nullable"] = "enable",
            }.ToImmutableDictionary(StringComparer.Ordinal),
            new Dictionary<string, ImmutableArray<EvaluatedItem>>(StringComparer.Ordinal)
            {
                ["Reference"] = [],
                ["Analyzer"] = [],
            }.ToImmutableDictionary(StringComparer.Ordinal),
            []);
        var compilation = new SemanticCompilationAdapter().CreateCompilation(
            new SemanticCompilationRequest(
                target,
                "Probe",
                [new SemanticSourceDocument(extraction.Document.DocumentId, "Probe.cs", source)]),
            CancellationToken.None);
        return new CompilationInput(extraction, Assert.Single(compilation.Documents));
    }

    private sealed record CompilationInput(
        SyntaxFactExtraction Extraction,
        SemanticDocumentBinding Binding);

    private sealed class NullDocumentationIdProvider : ISymbolDocumentationIdProvider
    {
        public string? GetDocumentationCommentId(ISymbol symbol) => null;
    }

    private sealed class SelectivelyThrowingDeclaredSymbolProvider : IDeclaredSymbolProvider
    {
        public ISymbol? GetDeclaredSymbol(
            SemanticModel semanticModel,
            MemberDeclarationSyntax declaration,
            CancellationToken cancellationToken)
        {
            if (declaration is MethodDeclarationSyntax { Identifier.ValueText: "Bad" })
            {
                throw new InvalidOperationException("controlled binding failure");
            }

            return declaration switch
            {
                BaseFieldDeclarationSyntax { Declaration.Variables.Count: 1 } field =>
                    semanticModel.GetDeclaredSymbol(field.Declaration.Variables[0], cancellationToken),
                BaseFieldDeclarationSyntax => null,
                _ => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            };
        }
    }
}
