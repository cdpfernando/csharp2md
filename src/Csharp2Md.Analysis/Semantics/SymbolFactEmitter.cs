using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using DomainDocument = Csharp2Md.Domain.Facts.Document;
using DomainDocumentId = Csharp2Md.Domain.Literals.DocumentId;
using DomainProjectId = Csharp2Md.Domain.Identity.ProjectId;

namespace Csharp2Md.Analysis.Semantics;

internal static class SymbolFactEmitter
{
    private static readonly SymbolDisplayFormat Qualified = SymbolDisplayFormat.FullyQualifiedFormat;

    internal static void Emit(
        BoundSolution boundSolution,
        SnapshotAccumulator accumulator,
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(boundSolution);
        ArgumentNullException.ThrowIfNull(accumulator);
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        var documents = accumulator.ToSnapshot().Facts.OfType<DomainDocument>().ToArray();
        var root = AuthorizedRoot.ForSolution(solutionPath);
        foreach (var compilation in boundSolution.Compilations)
        {
            foreach (var tree in compilation.SyntaxTrees)
            {
                var projectId = MatchOwningProject(tree.FilePath, documents, root);
                if (projectId is null)
                {
                    continue;
                }

                var model = compilation.GetSemanticModel(tree);
                foreach (var node in tree.GetRoot(cancellationToken).DescendantNodesAndSelf())
                {
                    foreach (var (symbol, metadataName) in DeclaredSymbols(model, node, cancellationToken))
                    {
                        var fact = TryCreate(
                            symbol,
                            projectId.Value,
                            metadataName,
                            node,
                            documents,
                            root,
                            cancellationToken);
                        if (fact is not null)
                        {
                            accumulator.AddFact(fact);
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<(ISymbol Symbol, string MetadataName)> DeclaredSymbols(
        SemanticModel model,
        SyntaxNode node,
        CancellationToken cancellationToken)
    {
        switch (node)
        {
            case BaseTypeDeclarationSyntax:
            case DelegateDeclarationSyntax:
            case BaseMethodDeclarationSyntax when node is not DestructorDeclarationSyntax:
            case LocalFunctionStatementSyntax:
            case BasePropertyDeclarationSyntax:
            case EnumMemberDeclarationSyntax:
            case VariableDeclaratorSyntax when node.Parent?.Parent is FieldDeclarationSyntax or EventFieldDeclarationSyntax:
                {
                    var declared = model.GetDeclaredSymbol(node, cancellationToken);
                    if (declared is not null)
                    {
                        yield return (declared, declared.MetadataName);
                    }

                    if (node is TypeDeclarationSyntax { ParameterList: not null }
                        && declared is INamedTypeSymbol named)
                    {
                        foreach (var constructor in named.InstanceConstructors)
                        {
                            if (!constructor.IsImplicitlyDeclared)
                            {
                                yield return (constructor, constructor.MetadataName);
                            }
                        }
                    }

                    foreach (var lambda in AssignedLambdas(model, node, cancellationToken))
                    {
                        yield return lambda;
                    }

                    yield break;
                }
        }
    }

    private static IEnumerable<(ISymbol Symbol, string MetadataName)> AssignedLambdas(
        SemanticModel model,
        SyntaxNode node,
        CancellationToken cancellationToken)
    {
        var body = node switch
        {
            BaseMethodDeclarationSyntax method => (SyntaxNode?)method.Body ?? method.ExpressionBody,
            LocalFunctionStatementSyntax local => (SyntaxNode?)local.Body ?? local.ExpressionBody,
            _ => null,
        };
        if (body is null)
        {
            yield break;
        }

        var operation = model.GetOperation(body, cancellationToken);
        if (operation is null)
        {
            yield break;
        }

        foreach (var current in operation.DescendantsAndSelf())
        {
            if (current is not IAnonymousFunctionOperation function
                || AssignedLambdaName(function.Syntax) is not { } name)
            {
                continue;
            }

            yield return (function.Symbol, name);
        }
    }

    private static string? AssignedLambdaName(SyntaxNode syntax) => syntax.Parent switch
    {
        EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => declarator.Identifier.ValueText,
        EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Identifier.ValueText,
        _ => null,
    };

    internal static CanonicalSymbolSignature? TrySignature(ISymbol symbol, string? metadataName = null)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        var name = metadataName ?? symbol.MetadataName;
        if (symbol.Kind is not (SymbolKind.NamedType or SymbolKind.Method or SymbolKind.Property
            or SymbolKind.Field or SymbolKind.Event)
            || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return CanonicalSymbolSignature.Create(
            symbol.Kind.ToString().ToLowerInvariant(),
            Container(symbol),
            name,
            Arity(symbol),
            TypeDisplay(symbol),
            Parameters(symbol),
            TypeArguments(symbol));
    }

    private static Symbol? TryCreate(
        ISymbol symbol,
        DomainProjectId projectId,
        string metadataName,
        SyntaxNode node,
        IReadOnlyList<DomainDocument> documents,
        string authorizedRoot,
        CancellationToken cancellationToken)
    {
        var signature = TrySignature(symbol, metadataName);
        return signature is null
            ? null
            : Symbol.Create(
                signature.Value,
                projectId,
                Facets(symbol),
                TryLocator(symbol, node, documents, authorizedRoot, cancellationToken));
    }

    private static DeclarationLocator? TryLocator(
        ISymbol symbol,
        SyntaxNode node,
        IReadOnlyList<DomainDocument> documents,
        string authorizedRoot,
        CancellationToken cancellationToken)
    {
        var candidates = new List<(DomainDocument Document, SourceSpan Span)>();
        var references = symbol.DeclaringSyntaxReferences;
        if (references.Length == 0)
        {
            AddLocatorCandidate(node, documents, authorizedRoot, candidates);
        }
        else
        {
            foreach (var reference in references)
            {
                AddLocatorCandidate(reference.GetSyntax(cancellationToken), documents, authorizedRoot, candidates);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var winning = candidates
            .OrderBy(candidate => candidate.Document.Reference.Id.Value, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Span.StartLine)
            .ThenBy(candidate => candidate.Span.StartColumn)
            .First();

        var absolute = Path.GetFullPath(Path.Combine(
            authorizedRoot,
            winning.Document.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        return new DeclarationLocator(
            DomainDocumentId.Create(winning.Document.Reference.Id.Value),
            winning.Document.RelativePath,
            winning.Span,
            ObservationMaterializer.HashFileBytes(absolute));
    }

    private static void AddLocatorCandidate(
        SyntaxNode node,
        IReadOnlyList<DomainDocument> documents,
        string authorizedRoot,
        List<(DomainDocument Document, SourceSpan Span)> candidates)
    {
        var document = FindDocument(node.SyntaxTree.FilePath, documents, authorizedRoot);
        if (document is null)
        {
            return;
        }

        var evidence = ObservationMaterializer.CreateLocator(document, node);
        candidates.Add((document, evidence.Span));
    }

    private static SymbolFacetSet Facets(ISymbol symbol)
    {
        if (symbol is not IMethodSymbol method)
        {
            return SymbolFacetSet.Create([]);
        }

        var facets = new List<SymbolFacet> { SymbolFacet.Callable };
        if (method.IsAbstract || method.ContainingType?.TypeKind is TypeKind.Interface)
        {
            facets.Add(SymbolFacet.Abstract);
        }

        if (IsExternallyReachable(method))
        {
            facets.Add(SymbolFacet.ExternallyReachable);
        }

        return SymbolFacetSet.Create(facets);
    }

    /// <summary>
    /// Walks <paramref name="symbol"/>'s own accessibility and every enclosing <see cref="ISymbol.ContainingType"/>
    /// up to the namespace-scoped declaration. Roslyn's <see cref="ISymbol.DeclaredAccessibility"/> only reports
    /// the symbol's own declared modifier, not whether the declaration is actually reachable from outside the
    /// assembly once containing types are accounted for (a public method on a private nested type is not
    /// reachable). <see cref="Accessibility.Public"/>, <see cref="Accessibility.Protected"/> and
    /// <see cref="Accessibility.ProtectedOrInternal"/> at every level keep the walk alive; anything else
    /// (private, internal, private protected, or not applicable) closes it.
    /// </summary>
    private static bool IsExternallyReachable(ISymbol symbol)
    {
        for (ISymbol? current = symbol; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    continue;
                default:
                    return false;
            }
        }

        return true;
    }

    private static string Container(ISymbol symbol)
    {
        if (symbol.ContainingType is { } type)
        {
            return type.ToDisplayString(Qualified);
        }

        if (symbol.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            return ns.ToDisplayString(Qualified);
        }

        return "global";
    }

    private static int Arity(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol named => named.Arity,
        IMethodSymbol method => method.TypeParameters.Length,
        _ => 0,
    };

    private static string TypeDisplay(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.ReturnType.ToDisplayString(Qualified),
        IPropertySymbol property => property.Type.ToDisplayString(Qualified),
        IFieldSymbol field => field.Type.ToDisplayString(Qualified),
        IEventSymbol ev => ev.Type.ToDisplayString(Qualified),
        _ => symbol.ToDisplayString(Qualified),
    };

    private static IEnumerable<SymbolParameterSignature> Parameters(ISymbol symbol)
    {
        var parameters = symbol switch
        {
            IMethodSymbol method => method.Parameters,
            IPropertySymbol property => property.Parameters,
            _ => [],
        };

        return parameters.Select(static parameter => new SymbolParameterSignature(
            parameter.Type.ToDisplayString(Qualified),
            parameter.RefKind switch
            {
                RefKind.Ref => SymbolParameterModifier.Ref,
                RefKind.Out => SymbolParameterModifier.Out,
                RefKind.In => SymbolParameterModifier.In,
                _ => SymbolParameterModifier.None,
            }));
    }

    private static IEnumerable<string> TypeArguments(ISymbol symbol)
    {
        ImmutableArray<ITypeSymbol> arguments = symbol switch
        {
            INamedTypeSymbol named => named.TypeArguments,
            IMethodSymbol method => method.TypeArguments,
            _ => [],
        };

        return arguments.Select(static argument => argument.ToDisplayString(Qualified));
    }

    private static DomainProjectId? MatchOwningProject(
        string? treePath,
        IReadOnlyList<DomainDocument> documents,
        string authorizedRoot)
    {
        var document = FindDocument(treePath, documents, authorizedRoot);
        if (document is not null)
        {
            return document.OwningProject;
        }

        if (string.IsNullOrEmpty(treePath))
        {
            return null;
        }

        var relative = AuthorizedRoot.ToLogicalPath(authorizedRoot, treePath);
        DomainProjectId? best = null;
        var bestLength = -1;
        foreach (var candidate in documents)
        {
            var separator = candidate.RelativePath.LastIndexOf('/');
            var directory = separator < 0 ? string.Empty : candidate.RelativePath[..separator];
            if (directory.Length == 0)
            {
                continue;
            }

            if ((relative.Equals(directory, StringComparison.OrdinalIgnoreCase)
                    || relative.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase))
                && directory.Length > bestLength)
            {
                best = candidate.OwningProject;
                bestLength = directory.Length;
            }
        }

        return best;
    }

    private static DomainDocument? FindDocument(
        string? treePath,
        IReadOnlyList<DomainDocument> documents,
        string authorizedRoot)
    {
        if (string.IsNullOrEmpty(treePath))
        {
            return null;
        }

        var relative = AuthorizedRoot.ToLogicalPath(authorizedRoot, treePath);
        foreach (var document in documents)
        {
            if (string.Equals(document.RelativePath, relative, StringComparison.OrdinalIgnoreCase))
            {
                return document;
            }
        }

        return null;
    }
}
