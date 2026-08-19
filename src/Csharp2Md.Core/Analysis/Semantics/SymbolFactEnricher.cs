using Csharp2Md.Core.Analysis.Semantics.Roslyn;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Analysis.Semantics;

internal sealed record SymbolFactEnrichmentResult(
    DocumentFact Document,
    ImmutableArray<SymbolFact> Symbols,
    ImmutableArray<AnalysisDiagnostic> Diagnostics);

internal interface ISymbolDocumentationIdProvider
{
    string? GetDocumentationCommentId(ISymbol symbol);
}

internal interface IDeclaredSymbolProvider
{
    ISymbol? GetDeclaredSymbol(
        SemanticModel semanticModel,
        MemberDeclarationSyntax declaration,
        CancellationToken cancellationToken);
}

internal sealed class SymbolFactEnricher(
    ISymbolDocumentationIdProvider? documentationIds = null,
    IDeclaredSymbolProvider? declaredSymbols = null)
{
    private static readonly FactProvenance Provenance = new("csharp2md.semantic", "1");
    private readonly ISymbolDocumentationIdProvider _documentationIds =
        documentationIds ?? new RoslynDocumentationIdProvider();
    private readonly IDeclaredSymbolProvider _declaredSymbols =
        declaredSymbols ?? new RoslynDeclaredSymbolProvider();

    public SymbolFactEnrichmentResult Enrich(
        DocumentFact document,
        ImmutableArray<SymbolFact> syntacticSymbols,
        SemanticDocumentBinding binding,
        TargetFactId targetId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(binding);
        if (document.DocumentId != binding.DocumentId
            || syntacticSymbols.Any(symbol => symbol.DocumentId != document.DocumentId))
        {
            throw new ArgumentException("Document, symbols, and semantic binding must share one document identity.", nameof(binding));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (binding.SemanticModel is null)
        {
            return RetainSyntax(document, syntacticSymbols, binding.Diagnostics);
        }

        var syntaxById = syntacticSymbols.ToDictionary(static symbol => symbol.SymbolId);
        var candidates = binding.SyntaxTree.GetRoot(cancellationToken)
            .DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .Where(static declaration => declaration is not GlobalStatementSyntax)
            .Select(declaration => new Candidate(
                declaration,
                SymbolFactId.CreateSyntactic(
                    document.ProjectId,
                    document.RelativePath,
                    SyntaxFactExtractor.DeclarationKind(declaration),
                    SyntaxFactExtractor.DeclarationSignature(declaration))))
            .Where(candidate => syntaxById.ContainsKey(candidate.SyntacticId))
            .ToImmutableArray();

        var diagnostics = binding.Diagnostics.ToBuilder();
        var bound = ImmutableArray.CreateBuilder<BoundCandidate>(candidates.Length);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var symbol = _declaredSymbols.GetDeclaredSymbol(
                    binding.SemanticModel,
                    candidate.Declaration,
                    cancellationToken);
                if (symbol is null)
                {
                    var diagnostic = Diagnostic(
                        "C2M-BIND-001",
                        candidate.SyntacticId.ToFactId(),
                        "Roslyn returned no declared symbol for the syntax fact.",
                        SyntaxFactExtractor.DeclarationKind(candidate.Declaration));
                    diagnostics.Add(diagnostic);
                    bound.Add(new BoundCandidate(candidate, null, candidate.SyntacticId, diagnostic));
                    continue;
                }

                var containsError = ContainsErrorSymbol(symbol);
                var resolvedId = containsError
                    ? candidate.SyntacticId
                    : ResolvedId(targetId, syntaxById[candidate.SyntacticId], symbol);
                AnalysisDiagnostic? errorDiagnostic = null;
                if (containsError)
                {
                    errorDiagnostic = Diagnostic(
                        "C2M-BIND-002",
                        candidate.SyntacticId.ToFactId(),
                        "Semantic binding contains an error symbol; syntax identity was retained.",
                        symbol.ToDisplayString());
                    diagnostics.Add(errorDiagnostic);
                }

                bound.Add(new BoundCandidate(candidate, symbol, resolvedId, errorDiagnostic));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var diagnostic = Diagnostic(
                    "C2M-BIND-003",
                    candidate.SyntacticId.ToFactId(),
                    "Semantic binding failed for the syntax fact.",
                    exception.Message);
                diagnostics.Add(diagnostic);
                bound.Add(new BoundCandidate(candidate, null, candidate.SyntacticId, diagnostic));
            }
        }

        var enriched = syntacticSymbols
            .Select(syntactic => EnrichSymbol(syntactic, bound.ToImmutable()))
            .OrderBy(static symbol => symbol.SymbolId.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        var documentDiagnostics = binding.Diagnostics.Select(static diagnostic => diagnostic.Id)
            .Concat(diagnostics.Select(static diagnostic => diagnostic.Id));
        var completedDocument = document with
        {
            Header = Header(
                document.Header,
                document.Header.Id,
                FactResolutionAlgebra.AggregateDocument(enriched.Select(static symbol => symbol.Header.Resolution)),
                documentDiagnostics),
            SymbolIds = enriched.Select(static symbol => symbol.SymbolId).ToImmutableArray(),
        };

        return new SymbolFactEnrichmentResult(
            completedDocument,
            enriched,
            diagnostics.Distinct().Order().ToImmutableArray());
    }

    private SymbolFact EnrichSymbol(SymbolFact syntactic, ImmutableArray<BoundCandidate> candidates)
    {
        var candidate = candidates.SingleOrDefault(item => item.Candidate.SyntacticId == syntactic.SymbolId);
        if (candidate?.Symbol is not { } symbol)
        {
            return candidate?.Diagnostic is { } diagnostic
                ? syntactic with
                {
                    Header = Header(
                        syntactic.Header,
                        syntactic.Header.Id,
                        FactResolution.Syntactic,
                        [diagnostic.Id]),
                }
                : syntactic;
        }

        var containsError = ContainsErrorSymbol(symbol);
        var resolution = containsError ? FactResolution.Unresolved : FactResolution.Exact;
        var implementedMemberIds = ImplementedMembers(symbol)
            .Select(implemented => FindLocalId(implemented, candidates))
            .OfType<SymbolFactId>()
            .Distinct()
            .OrderBy(static id => id.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        var overriddenMemberId = FindLocalId(OverriddenMember(symbol), candidates);
        var baseAndInterfaceIds = BaseAndInterfaces(symbol)
            .Select(related => FindLocalId(related, candidates))
            .OfType<SymbolFactId>()
            .Distinct()
            .OrderBy(static id => id.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        var attributes = symbol.GetAttributes()
            .Select(static attribute => attribute.AttributeClass)
            .Where(static attributeClass => attributeClass is not null && attributeClass.TypeKind is not TypeKind.Error)
            .Select(static attributeClass => DisplayType(attributeClass!))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        var relevantTypes = RelevantTypes(symbol)
            .Where(static type => type.TypeKind is not TypeKind.Error)
            .Select(DisplayType)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

        return syntactic with
        {
            Header = Header(
                syntactic.Header,
                candidate.ResolvedId.ToFactId(),
                resolution,
                candidate.Diagnostic is null ? [] : [candidate.Diagnostic.Id]),
            SymbolId = candidate.ResolvedId,
            ContainsErrorSymbol = containsError,
            BaseAndInterfaceIds = baseAndInterfaceIds,
            Attributes = attributes.IsEmpty && containsError ? syntactic.Attributes : attributes,
            RelevantTypeReferences = relevantTypes.IsEmpty && containsError ? syntactic.RelevantTypeReferences : relevantTypes,
            Semantics = new SymbolSemanticDetails(implementedMemberIds, overriddenMemberId),
        };
    }

    private SymbolFactId ResolvedId(TargetFactId targetId, SymbolFact syntactic, ISymbol symbol)
    {
        var documentationId = _documentationIds.GetDocumentationCommentId(symbol);
        return !string.IsNullOrWhiteSpace(documentationId)
            ? SymbolFactId.CreateResolved(targetId, documentationId)
            : SymbolFactId.CreateFallback(targetId, CanonicalSignature(syntactic.SymbolKind, symbol));
    }

    private static CanonicalSymbolSignature CanonicalSignature(string symbolKind, ISymbol symbol)
    {
        var parameters = Parameters(symbol)
            .Select(static parameter => new SymbolParameterSignature(
                DisplayType(parameter.Type),
                ParameterModifier(parameter.RefKind)));
        var typeArguments = symbol switch
        {
            INamedTypeSymbol named => named.TypeArguments.Select(DisplayType),
            IMethodSymbol method => method.TypeArguments.Select(DisplayType),
            _ => [],
        };
        var arity = symbol switch
        {
            INamedTypeSymbol named => named.Arity,
            IMethodSymbol method => method.Arity,
            _ => 0,
        };

        return CanonicalSymbolSignature.Create(
            symbolKind,
            DisplayContainer(symbol.ContainingSymbol),
            symbol.MetadataName,
            arity,
            SymbolType(symbol),
            parameters,
            typeArguments);
    }

    private static IEnumerable<IParameterSymbol> Parameters(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.Parameters,
        IPropertySymbol property => property.Parameters,
        _ => [],
    };

    private static string SymbolType(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol type => DisplayType(type),
        IMethodSymbol method => DisplayType(method.ReturnType),
        IPropertySymbol property => DisplayType(property.Type),
        IFieldSymbol field => DisplayType(field.Type),
        IEventSymbol @event => DisplayType(@event.Type),
        INamespaceSymbol @namespace => @namespace.IsGlobalNamespace ? "global::" : $"global::{@namespace.ToDisplayString()}",
        _ => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
    };

    private static string DisplayContainer(ISymbol? symbol) => symbol switch
    {
        null => "global::",
        INamespaceSymbol { IsGlobalNamespace: true } => "global::",
        INamespaceSymbol @namespace => $"global::{@namespace.ToDisplayString()}",
        _ => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
    };

    private static string DisplayType(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol parameter => $"global::{parameter.Name}",
        IArrayTypeSymbol array => $"{DisplayType(array.ElementType)}[{new string(',', array.Rank - 1)}]",
        IPointerTypeSymbol pointer => $"{DisplayType(pointer.PointedAtType)}*",
        INamedTypeSymbol named => DisplayNamedType(named),
        _ => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
    };

    private static string DisplayNamedType(INamedTypeSymbol type)
    {
        var metadataName = type.MetadataName;
        var arityMarker = metadataName.IndexOf('`', StringComparison.Ordinal);
        var name = arityMarker < 0 ? metadataName : metadataName[..arityMarker];
        var container = type.ContainingType is { } containingType
            ? DisplayNamedType(containingType)
            : type.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
                ? $"global::{containingNamespace.ToDisplayString()}"
                : "global::";
        var separator = container.EndsWith("::", StringComparison.Ordinal) ? string.Empty : ".";
        var typeArguments = type.Arity == 0
            ? string.Empty
            : $"<{string.Join(',', type.TypeArguments.Select(DisplayType))}>";
        return $"{container}{separator}{name}{typeArguments}";
    }

    private static SymbolParameterModifier ParameterModifier(RefKind refKind) => refKind switch
    {
        RefKind.Ref => SymbolParameterModifier.Ref,
        RefKind.Out => SymbolParameterModifier.Out,
        RefKind.In or RefKind.RefReadOnlyParameter => SymbolParameterModifier.In,
        _ => SymbolParameterModifier.None,
    };

    private static IEnumerable<ITypeSymbol> BaseAndInterfaces(ISymbol symbol) => symbol is ITypeSymbol type
        ? (type.BaseType is null ? [] : new[] { type.BaseType }).Concat(type.Interfaces)
        : [];

    private static IEnumerable<ISymbol> ImplementedMembers(ISymbol symbol)
    {
        var explicitImplementations = symbol switch
        {
            IMethodSymbol method => method.ExplicitInterfaceImplementations.Cast<ISymbol>(),
            IPropertySymbol property => property.ExplicitInterfaceImplementations.Cast<ISymbol>(),
            IEventSymbol @event => @event.ExplicitInterfaceImplementations.Cast<ISymbol>(),
            _ => [],
        };
        if (symbol.ContainingType is not { } containingType)
        {
            return explicitImplementations;
        }

        var implicitImplementations = containingType.AllInterfaces
            .SelectMany(static @interface => @interface.GetMembers())
            .Where(member => SymbolEqualityComparer.Default.Equals(
                containingType.FindImplementationForInterfaceMember(member),
                symbol));
        return explicitImplementations.Concat(implicitImplementations);
    }

    private static ISymbol? OverriddenMember(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.OverriddenMethod,
        IPropertySymbol property => property.OverriddenProperty,
        IEventSymbol @event => @event.OverriddenEvent,
        _ => null,
    };

    private static SymbolFactId? FindLocalId(ISymbol? symbol, ImmutableArray<BoundCandidate> candidates)
    {
        if (symbol is null)
        {
            return null;
        }

        foreach (var candidate in candidates)
        {
            if (candidate.Symbol is not null && SymbolEqualityComparer.Default.Equals(candidate.Symbol, symbol))
            {
                return candidate.ResolvedId;
            }
        }

        return null;
    }

    private static IEnumerable<ITypeSymbol> RelevantTypes(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol type => BaseAndInterfaces(type),
        IMethodSymbol method => method.Parameters.Select(static parameter => parameter.Type).Prepend(method.ReturnType),
        IPropertySymbol property => property.Parameters.Select(static parameter => parameter.Type).Prepend(property.Type),
        IFieldSymbol field => [field.Type],
        IEventSymbol @event => [@event.Type],
        _ => [],
    };

    private static bool ContainsErrorSymbol(ISymbol symbol) =>
        symbol is ITypeSymbol type && ContainsErrorType(type)
        || RelevantTypes(symbol).Any(ContainsErrorType)
        || symbol.GetAttributes().Any(static attribute => attribute.AttributeClass is null || ContainsErrorType(attribute.AttributeClass));

    private static bool ContainsErrorType(ITypeSymbol type) => type switch
    {
        { TypeKind: TypeKind.Error } => true,
        IArrayTypeSymbol array => ContainsErrorType(array.ElementType),
        IPointerTypeSymbol pointer => ContainsErrorType(pointer.PointedAtType),
        INamedTypeSymbol named => named.TypeArguments.Any(ContainsErrorType),
        _ => false,
    };

    private static SymbolFactEnrichmentResult RetainSyntax(
        DocumentFact document,
        ImmutableArray<SymbolFact> symbols,
        ImmutableArray<AnalysisDiagnostic> diagnostics)
    {
        var diagnosticIds = diagnostics.Select(static diagnostic => diagnostic.Id).ToImmutableArray();
        var retained = symbols.Select(symbol => symbol with
        {
            Header = Header(symbol.Header, symbol.Header.Id, FactResolution.Syntactic, diagnosticIds),
        })
            .ToImmutableArray();
        return new SymbolFactEnrichmentResult(
            document with
            {
                Header = Header(document.Header, document.Header.Id, FactResolution.Syntactic, diagnosticIds),
            },
            retained,
            diagnostics.Distinct().Order().ToImmutableArray());
    }

    private static FactHeader Header(
        FactHeader source,
        FactId id,
        FactResolution resolution,
        IEnumerable<DiagnosticId> diagnostics) =>
        FactHeader.Create(
            id,
            source.Kind,
            resolution,
            source.Provenance.Append(Provenance),
            source.Evidence,
            source.DiagnosticIds.Concat(diagnostics));

    private static AnalysisDiagnostic Diagnostic(string code, FactId scopeId, string message, string detail) =>
        AnalysisDiagnostic.Create(
            code,
            Csharp2Md.Core.Facts.Metadata.DiagnosticSeverity.Warning,
            DiagnosticStage.Document,
            scopeId,
            message,
            string.IsNullOrWhiteSpace(detail) ? [] : [new DiagnosticData("detail", Sanitize(detail))]);

    private static string Sanitize(string value) =>
        string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private sealed record Candidate(MemberDeclarationSyntax Declaration, SymbolFactId SyntacticId);

    private sealed record BoundCandidate(
        Candidate Candidate,
        ISymbol? Symbol,
        SymbolFactId ResolvedId,
        AnalysisDiagnostic? Diagnostic);

    private sealed class RoslynDocumentationIdProvider : ISymbolDocumentationIdProvider
    {
        public string? GetDocumentationCommentId(ISymbol symbol) => symbol.GetDocumentationCommentId();
    }

    private sealed class RoslynDeclaredSymbolProvider : IDeclaredSymbolProvider
    {
        public ISymbol? GetDeclaredSymbol(
            SemanticModel semanticModel,
            MemberDeclarationSyntax declaration,
            CancellationToken cancellationToken) => declaration switch
            {
                BaseFieldDeclarationSyntax { Declaration.Variables.Count: 1 } field =>
                    semanticModel.GetDeclaredSymbol(field.Declaration.Variables[0], cancellationToken),
                BaseFieldDeclarationSyntax => null,
                _ => semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            };
    }
}
