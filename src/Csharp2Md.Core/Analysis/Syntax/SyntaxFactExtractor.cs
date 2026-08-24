using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Semantics;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Analysis.Syntax;

internal sealed record SyntacticRelationCandidate(
    FactId OwnerId,
    string RelationKind,
    string ObservedTarget,
    FactResolution ShapeConfidence,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn)
{
    /// <summary>The receiver expression's own source text, populated only for a <c>calls</c> candidate.</summary>
    public string? ReceiverText { get; init; }

    /// <summary>The receiver's declared type name, when <see cref="DeclaredReceiverTypeName"/> could read one.</summary>
    public string? ReceiverTypeText { get; init; }

    /// <summary>The invoked member's name, populated only for a <c>calls</c> candidate.</summary>
    public string? MemberName { get; init; }

    public int? ArgumentCount { get; init; }

    /// <summary>Simple type names, one per argument; an entry is <c>null</c> when its type syntax cannot be read.</summary>
    public ImmutableArray<string?> ArgumentTypes { get; init; } = [];

    /// <summary>The enclosing document's namespace, captured once per document and shared by every candidate.</summary>
    public string? Namespace { get; init; }

    /// <summary>The enclosing document's using directives, captured once per document and shared by every candidate.</summary>
    public ImmutableArray<string> Imports { get; init; } = [];
}

internal sealed record SyntaxFactExtraction(
    DocumentFact Document,
    ImmutableArray<SymbolFact> Symbols,
    ImmutableArray<SyntacticRelationCandidate> RelationCandidates,
    ImmutableDictionary<SymbolFactId, ImmutableArray<string>> XmlProse,
    ImmutableArray<RawDatabaseClaim> DatabaseClaims,
    ImmutableArray<AnalysisDiagnostic> DatabaseDiagnostics);

internal static class SyntaxFactExtractor
{
    private static readonly FactProvenance Provenance = new("csharp2md.syntax", "1");

    /// <param name="analyzers">
    /// The data access analyzers to run over this document. Production passes nothing and gets the
    /// registered set; tests supply their own to exercise the handoff.
    /// </param>
    public static SyntaxFactExtraction Extract(
        ProjectFactId projectId,
        string relativePath,
        string source,
        IEnumerable<IDataAccessAnalyzer>? analyzers = null)
    {
        var document = SourceSectionExtractor.Extract(projectId, relativePath, source);
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var symbols = new List<SymbolFact>();
        var candidates = new List<SyntacticRelationCandidate>();
        var prose = ImmutableDictionary.CreateBuilder<SymbolFactId, ImmutableArray<string>>();
        var ownerByDeclaration = new Dictionary<MemberDeclarationSyntax, SymbolFactId>();
        var declarationsBySymbolId = new Dictionary<SymbolFactId, MemberDeclarationSyntax>();
        var methodSymbolsByName = new Dictionary<string, SymbolFactId>(StringComparer.Ordinal);
        var documentTypeNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var declaration in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            if (declaration is GlobalStatementSyntax)
            {
                continue;
            }

            var kind = DeclarationKind(declaration);
            var signature = DeclarationSignature(declaration);
            var symbolId = SymbolFactId.CreateSyntactic(projectId, relativePath, kind, signature);
            var name = DeclarationName(declaration);
            var enclosingNamespace = EnclosingNamespace(declaration);
            var enclosingTypeNames = EnclosingTypeNames(declaration);
            var fact = new SymbolFact(
                Header(symbolId.ToFactId()),
                symbolId,
                document.DocumentId,
                kind,
                declaration.ContainsDiagnostics,
                [],
                AttributeNames(declaration),
                ReferencedTypes(declaration),
                Semantics: null,
                name,
                TypeNameNormalizer.Normalize(QualifiedName(enclosingNamespace, enclosingTypeNames, name)),
                enclosingNamespace,
                enclosingTypeNames.IsEmpty
                    ? null
                    : TypeNameNormalizer.Normalize(QualifiedName(enclosingNamespace, enclosingTypeNames, null)),
                EnclosingDeclarationId(declaration, ownerByDeclaration),
                signature,
                DeclarationArity(declaration),
                DeclarationParameterTypes(declaration));
            symbols.Add(fact);
            ownerByDeclaration[declaration] = symbolId;
            declarationsBySymbolId[symbolId] = declaration;
            if (declaration is MethodDeclarationSyntax method)
            {
                methodSymbolsByName.TryAdd(method.Identifier.ValueText, symbolId);
            }

            if (kind is "class" or "struct" or "interface" or "record" or "record-struct" or "enum" or "delegate")
            {
                documentTypeNames.Add(DeclarationName(declaration));
            }

            var documentation = XmlDocProse.Extract(declaration).ToImmutableArray();
            if (!documentation.IsEmpty)
            {
                prose[symbolId] = documentation;
            }

            if (declaration is BaseTypeDeclarationSyntax { BaseList: { } baseList })
            {
                var typeParameterNames = declaration is TypeDeclarationSyntax typeDeclaration
                    ? typeDeclaration.TypeParameterList?.Parameters
                        .Select(static parameter => parameter.Identifier.ValueText)
                        .ToImmutableHashSet(StringComparer.Ordinal) ?? ImmutableHashSet<string>.Empty
                    : ImmutableHashSet<string>.Empty;
                var isInterfaceLikeDeclaration = IsInterfaceLikeBaseListOwner(declaration);

                candidates.AddRange(baseList.Types.Select((type, index) =>
                {
                    var span = root.SyntaxTree.GetLineSpan(type.Type.Span);
                    var (relationKind, resolution) = ClassifyBaseListEntry(
                        type.Type, index, isInterfaceLikeDeclaration, typeParameterNames);
                    return new SyntacticRelationCandidate(
                        symbolId.ToFactId(),
                        relationKind,
                        NormalizeNode(type.Type),
                        resolution,
                        span.StartLinePosition.Line + 1,
                        span.StartLinePosition.Character + 1,
                        span.EndLinePosition.Line + 1,
                        span.EndLinePosition.Character + 1);
                }));
            }
        }

        // Pass one of database access discovery rides this walk: here, and only here, the enclosing
        // member of every node is already known. The collector owns what a claim is; this file only
        // hands it the document and that map.
        var dataAccess = DataAccessCollector.Collect(
            DataAccessContext.Create(document.DocumentId, relativePath, root, ownerByDeclaration),
            analyzers ?? DataAccessCollector.RegisteredAnalyzers);

        var consumedObjectCreations = new HashSet<ObjectCreationExpressionSyntax>();

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var ownerId = ResolveOwner(invocation, ownerByDeclaration, document.DocumentId.ToFactId());

            if (IsMessagingShapedMemberName(invocation))
            {
                // Per spec.md's Edge Case, a Publish/Subscribe-named call that yields no extractable
                // target still claims the invocation - it emits no relation at all, never falls
                // through to a "calls"/"http-call" guess.
                candidates.AddRange(ClassifyMessagingInvocation(
                    invocation, root.SyntaxTree, ownerId, methodSymbolsByName, consumedObjectCreations));
                continue;
            }

            if (ClassifyHttpInvocation(invocation, root.SyntaxTree, ownerId) is { } httpCandidate)
            {
                candidates.Add(httpCandidate);
                continue;
            }

            if (ClassifyCallsInvocation(invocation, root.SyntaxTree, ownerId) is { } callsCandidate)
            {
                candidates.Add(callsCandidate);
            }
        }

        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (consumedObjectCreations.Contains(creation))
            {
                continue;
            }

            var simpleName = SimpleTypeName(creation.Type);
            if (RelationNoiseFilter.IsLikelyFrameworkType(simpleName))
            {
                continue;
            }

            var ownerId = ResolveOwner(creation, ownerByDeclaration, document.DocumentId.ToFactId());
            candidates.Add(MakeCandidate(ownerId, "creates", simpleName, FactResolution.Syntactic, creation, root.SyntaxTree));
        }

        var claimedTargetsByOwner = candidates
            .Where(static candidate => candidate.RelationKind is "calls" or "creates" or "inherits" or "implements")
            .GroupBy(static candidate => candidate.OwnerId)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static candidate => candidate.ObservedTarget).ToHashSet(StringComparer.Ordinal));

        foreach (var symbol in symbols)
        {
            foreach (var referenced in symbol.RelevantTypeReferences)
            {
                var simpleName = SimpleNameFromTypeText(referenced);
                if (PrimitiveOrInfrastructureTypeNames.Contains(simpleName)
                    || RelationNoiseFilter.IsLikelyFrameworkType(simpleName)
                    || documentTypeNames.Contains(simpleName))
                {
                    continue;
                }

                var ownerId = symbol.SymbolId.ToFactId();
                if (claimedTargetsByOwner.TryGetValue(ownerId, out var claimed) && claimed.Contains(simpleName))
                {
                    continue;
                }

                var declaration = declarationsBySymbolId[symbol.SymbolId];
                candidates.Add(MakeCandidate(ownerId, "references", simpleName, FactResolution.Syntactic, declaration, root.SyntaxTree));
            }
        }

        var canonicalSymbols = symbols
            .OrderBy(static symbol => symbol.SymbolId.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        // RELR-04: captured once here, not once per candidate, then shared by every candidate below -
        // SymbolIndexStrategy/ReceiverTypeStrategy use them as contextual hints, never as a filter.
        var documentNamespace = DocumentNamespace(root);
        var documentImports = DocumentImports(root);
        var canonicalCandidates = candidates
            .Distinct()
            .Select(candidate => candidate with { Namespace = documentNamespace, Imports = documentImports })
            .OrderBy(static candidate => candidate.OwnerId.Value, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.RelationKind, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.ObservedTarget, StringComparer.Ordinal)
            .ToImmutableArray();
        var completedDocument = document with
        {
            SymbolIds = canonicalSymbols.Select(static symbol => symbol.SymbolId).ToImmutableArray(),
        };

        return new SyntaxFactExtraction(
            completedDocument,
            canonicalSymbols,
            canonicalCandidates,
            prose.ToImmutable(),
            dataAccess.Claims,
            dataAccess.Diagnostics);
    }

    internal static string DeclarationKind(MemberDeclarationSyntax declaration) => declaration switch
    {
        BaseNamespaceDeclarationSyntax => "namespace",
        ClassDeclarationSyntax => "class",
        StructDeclarationSyntax => "struct",
        InterfaceDeclarationSyntax => "interface",
        RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) => "record-struct",
        RecordDeclarationSyntax => "record",
        EnumDeclarationSyntax => "enum",
        DelegateDeclarationSyntax => "delegate",
        MethodDeclarationSyntax => "method",
        ConstructorDeclarationSyntax => "constructor",
        DestructorDeclarationSyntax => "destructor",
        PropertyDeclarationSyntax => "property",
        IndexerDeclarationSyntax => "indexer",
        EventDeclarationSyntax or EventFieldDeclarationSyntax => "event",
        FieldDeclarationSyntax => "field",
        OperatorDeclarationSyntax => "operator",
        ConversionOperatorDeclarationSyntax => "conversion-operator",
        EnumMemberDeclarationSyntax => "enum-member",
        _ => "member",
    };

    internal static string DeclarationSignature(MemberDeclarationSyntax declaration)
    {
        var tokens = new List<string>();
        var parenthesisDepth = 0;
        var bracketDepth = 0;
        var angleDepth = 0;

        foreach (var token in declaration.DescendantTokens(descendIntoTrivia: false))
        {
            var kind = token.Kind();
            if (parenthesisDepth == 0 && bracketDepth == 0 && angleDepth == 0
                && kind is SyntaxKind.OpenBraceToken or SyntaxKind.EqualsGreaterThanToken or SyntaxKind.SemicolonToken)
            {
                break;
            }

            if (token.Text.Length == 0)
            {
                continue;
            }

            tokens.Add(IsCredentialLiteral(kind, token.Text) ? RedactedLiteralText : token.Text);
            parenthesisDepth += kind switch
            {
                SyntaxKind.OpenParenToken => 1,
                SyntaxKind.CloseParenToken => -1,
                _ => 0,
            };
            bracketDepth += kind switch
            {
                SyntaxKind.OpenBracketToken => 1,
                SyntaxKind.CloseBracketToken => -1,
                _ => 0,
            };
            angleDepth += kind switch
            {
                SyntaxKind.LessThanToken => 1,
                SyntaxKind.GreaterThanToken => -1,
                _ => 0,
            };
        }

        var container = declaration.Ancestors()
            .OfType<MemberDeclarationSyntax>()
            .Where(static ancestor => ancestor is not GlobalStatementSyntax)
            .Reverse()
            .Select(static ancestor => $"{DeclarationKind(ancestor)}:{DeclarationName(ancestor)}");
        var prefix = string.Join('/', container);
        var rawHeader = string.Join(' ', tokens);
        var sanitizedHeader = SanitizeCanonicalText(rawHeader);
        return prefix.Length == 0 ? sanitizedHeader : $"{prefix}/{sanitizedHeader}";
    }

    /// <summary>DAD-15: the placeholder a credential-shaped literal token is replaced with.</summary>
    private const string RedactedLiteralText = "\"<redacted>\"";

    /// <summary>
    /// DAD-15: whether this token is a string literal whose own text assigns a credential, e.g.
    /// <c>"...Password=hunter2;"</c>. The signature must never carry the value verbatim, since it and
    /// the symbol id derived from it are facts.
    /// </summary>
    private static bool IsCredentialLiteral(SyntaxKind kind, string text) =>
        kind is SyntaxKind.StringLiteralToken or SyntaxKind.Utf8StringLiteralToken
        && CredentialText.Carries(text);

    private static string SanitizeCanonicalText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var normalized = value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal)
            .Replace("  ", " ", StringComparison.Ordinal);

        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized.Trim();
    }

    private static string DeclarationName(MemberDeclarationSyntax declaration) => declaration switch
    {
        BaseNamespaceDeclarationSyntax @namespace => @namespace.Name.ToString(),
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        DestructorDeclarationSyntax destructor => destructor.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        EventDeclarationSyntax @event => @event.Identifier.ValueText,
        EnumMemberDeclarationSyntax member => member.Identifier.ValueText,
        FieldDeclarationSyntax field => string.Join(',', field.Declaration.Variables.Select(static variable => variable.Identifier.ValueText)),
        EventFieldDeclarationSyntax field => string.Join(',', field.Declaration.Variables.Select(static variable => variable.Identifier.ValueText)),
        IndexerDeclarationSyntax => "this",
        OperatorDeclarationSyntax @operator => @operator.OperatorToken.ValueText,
        ConversionOperatorDeclarationSyntax conversion => conversion.Type.ToString(),
        _ => declaration.Kind().ToString(),
    };

    /// <summary>
    /// The dotted namespace a declaration sits in, from its ancestor namespace declarations
    /// (outer-first), or <c>null</c> when it sits in the global namespace.
    /// </summary>
    private static string? EnclosingNamespace(MemberDeclarationSyntax declaration)
    {
        var names = declaration.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Reverse()
            .Select(static ancestor => ancestor.Name.ToString());
        var joined = string.Join('.', names);
        return joined.Length == 0 ? null : joined;
    }

    /// <summary>
    /// The document's own namespace (RELR-04), from its first namespace declaration - file-scoped
    /// (<c>namespace A;</c>) and block-scoped (<c>namespace A { }</c>) both derive from the same
    /// <see cref="BaseNamespaceDeclarationSyntax"/> base - or <c>null</c> for a document with none.
    /// </summary>
    private static string? DocumentNamespace(SyntaxNode root) =>
        root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault() is { } declaration
            ? declaration.Name.ToString()
            : null;

    /// <summary>
    /// Every plain namespace import in the document (RELR-04) - <c>using static</c> directives and
    /// aliases are excluded, since neither names an importable namespace a symbol lookup can use as a
    /// hint. A document with none yields an empty array, never <c>null</c>.
    /// </summary>
    private static ImmutableArray<string> DocumentImports(SyntaxNode root) =>
        root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Where(static directive => directive.Alias is null && !directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
            .Select(static directive => directive.Name?.ToString())
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

    /// <summary>
    /// The names of the type declarations a declaration is nested inside, outer-first - the chain a
    /// member's <c>ContainingType</c> is built from.
    /// </summary>
    private static ImmutableArray<string> EnclosingTypeNames(MemberDeclarationSyntax declaration) =>
        declaration.Ancestors()
            .OfType<BaseTypeDeclarationSyntax>()
            .Reverse()
            .Select(DeclarationName)
            .ToImmutableArray();

    private static string QualifiedName(
        string? enclosingNamespace,
        ImmutableArray<string> enclosingTypeNames,
        string? name)
    {
        IEnumerable<string> segments = enclosingTypeNames;
        if (enclosingNamespace is not null)
        {
            segments = segments.Prepend(enclosingNamespace);
        }

        if (name is not null)
        {
            segments = segments.Append(name);
        }

        return string.Join('.', segments);
    }

    /// <summary>
    /// The nearest enclosing declaration's own <see cref="SymbolFactId"/>, or <c>null</c> for a
    /// declaration with no enclosing declaration at all. Reuses the same <c>ownerByDeclaration</c>
    /// map the relation-candidate pass builds; pre-order traversal guarantees an ancestor is already
    /// in it by the time its descendants are visited.
    /// </summary>
    private static SymbolFactId? EnclosingDeclarationId(
        MemberDeclarationSyntax declaration,
        IReadOnlyDictionary<MemberDeclarationSyntax, SymbolFactId> ownerByDeclaration) =>
        declaration.Ancestors()
            .OfType<MemberDeclarationSyntax>()
            .FirstOrDefault(static ancestor => ancestor is not GlobalStatementSyntax) is { } enclosing
            && ownerByDeclaration.TryGetValue(enclosing, out var symbolId)
                ? symbolId
                : null;

    private static int DeclarationArity(MemberDeclarationSyntax declaration) => declaration switch
    {
        TypeDeclarationSyntax type => type.TypeParameterList?.Parameters.Count ?? 0,
        DelegateDeclarationSyntax @delegate => @delegate.TypeParameterList?.Parameters.Count ?? 0,
        MethodDeclarationSyntax method => method.TypeParameterList?.Parameters.Count ?? 0,
        _ => 0,
    };

    private static ImmutableArray<string> DeclarationParameterTypes(MemberDeclarationSyntax declaration)
    {
        var parameters = declaration switch
        {
            BaseMethodDeclarationSyntax method => method.ParameterList.Parameters,
            DelegateDeclarationSyntax @delegate => @delegate.ParameterList.Parameters,
            _ => default,
        };

        return parameters
            .Select(static parameter => parameter.Type)
            .OfType<TypeSyntax>()
            .Select(static type => TypeNameNormalizer.Normalize(NormalizeNode(type)))
            .ToImmutableArray();
    }

    private static ImmutableArray<string> AttributeNames(MemberDeclarationSyntax declaration) =>
        declaration.AttributeLists
            .SelectMany(static list => list.Attributes)
            .Select(static attribute => NormalizeNode(attribute.Name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

    private static ImmutableArray<string> ReferencedTypes(MemberDeclarationSyntax declaration)
    {
        IEnumerable<TypeSyntax> types = declaration switch
        {
            MethodDeclarationSyntax method => method.ParameterList.Parameters
                .Select(static parameter => parameter.Type)
                .OfType<TypeSyntax>()
                .Prepend(method.ReturnType),
            PropertyDeclarationSyntax property => [property.Type],
            FieldDeclarationSyntax field => [field.Declaration.Type],
            EventFieldDeclarationSyntax eventField => [eventField.Declaration.Type],
            EventDeclarationSyntax eventDeclaration => [eventDeclaration.Type],
            DelegateDeclarationSyntax @delegate => @delegate.ParameterList.Parameters
                .Select(static parameter => parameter.Type)
                .OfType<TypeSyntax>()
                .Prepend(@delegate.ReturnType),
            _ => [],
        };

        return types
            .Select(NormalizeNode)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static string NormalizeNode(SyntaxNode node) =>
        node.WithoutTrivia().NormalizeWhitespace(indentation: " ", eol: " ", elasticTrivia: false).ToFullString();

    private static readonly ImmutableHashSet<string> PublishMemberNames =
        ImmutableHashSet.Create(StringComparer.Ordinal, "Publish", "PublishAsync");

    private static readonly ImmutableHashSet<string> SubscribeMemberNames =
        ImmutableHashSet.Create(StringComparer.Ordinal, "Subscribe", "SubscribeAsync");

    /// <summary>
    /// Recognizes <c>Publish</c>/<c>PublishAsync</c> and <c>Subscribe</c>/<c>SubscribeAsync</c>
    /// invocation shapes by member name and argument shape alone (no <see cref="SemanticModel"/>),
    /// per the Assumptions table in spec.md: an explicit generic type argument is read directly when
    /// present; otherwise (publish only) the first argument's object-creation-expression type name is
    /// used; if neither syntactic form yields a name, no relation is emitted for that call. A
    /// <c>Subscribe&lt;T&gt;</c> whose single argument is a bare identifier naming an existing method
    /// in this document additionally yields a <c>handles</c> candidate owned by that method.
    /// </summary>
    private static bool IsMessagingShapedMemberName(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax memberAccess
        && (PublishMemberNames.Contains(memberAccess.Name.Identifier.ValueText)
            || SubscribeMemberNames.Contains(memberAccess.Name.Identifier.ValueText));

    private static IEnumerable<SyntacticRelationCandidate> ClassifyMessagingInvocation(
        InvocationExpressionSyntax invocation,
        SyntaxTree tree,
        FactId ownerId,
        IReadOnlyDictionary<string, SymbolFactId> methodSymbolsByName,
        HashSet<ObjectCreationExpressionSyntax> consumedObjectCreations)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            yield break;
        }

        var memberName = memberAccess.Name.Identifier.ValueText;
        var explicitTypeArgument = memberAccess.Name is GenericNameSyntax { TypeArgumentList.Arguments: [var typeArgument] }
            ? typeArgument
            : null;

        if (PublishMemberNames.Contains(memberName))
        {
            string? targetSimpleName = null;
            if (explicitTypeArgument is not null)
            {
                targetSimpleName = SimpleTypeName(explicitTypeArgument);
            }
            else if (invocation.ArgumentList.Arguments is [{ Expression: ObjectCreationExpressionSyntax creation }, ..])
            {
                targetSimpleName = SimpleTypeName(creation.Type);
                consumedObjectCreations.Add(creation);
            }

            if (targetSimpleName is not null)
            {
                yield return MakeCandidate(ownerId, "publishes", targetSimpleName, FactResolution.Syntactic, invocation, tree);
            }

            yield break;
        }

        if (SubscribeMemberNames.Contains(memberName) && explicitTypeArgument is not null)
        {
            var targetSimpleName = SimpleTypeName(explicitTypeArgument);
            yield return MakeCandidate(ownerId, "subscribes", targetSimpleName, FactResolution.Syntactic, invocation, tree);

            if (invocation.ArgumentList.Arguments is [{ Expression: IdentifierNameSyntax handlerIdentifier }, ..]
                && methodSymbolsByName.TryGetValue(handlerIdentifier.Identifier.ValueText, out var handlerSymbolId))
            {
                yield return MakeCandidate(handlerSymbolId.ToFactId(), "handles", targetSimpleName, FactResolution.Syntactic, invocation, tree);
            }
        }
    }

    private static readonly (string Prefix, string HttpMethod)[] HttpVerbPrefixes =
    [
        ("Get", "GET"),
        ("Post", "POST"),
        ("Put", "PUT"),
        ("Delete", "DELETE"),
        ("Patch", "PATCH"),
    ];

    /// <summary>
    /// Recognizes <c>CreateClient("name")</c> (any receiver - the factory itself is not
    /// syntactically confirmable without a <see cref="SemanticModel"/>) and HTTP-verb-shaped
    /// invocations (<c>GetAsync</c>, <c>PostAsJsonAsync</c>, etc. - any member name that starts with
    /// a verb prefix and continues with another capitalized word) on a receiver that is either
    /// syntactically typed as <c>HttpClient</c> (a locally-declared/parameter type name literally
    /// <c>HttpClient</c>, <see cref="FactResolution.Syntactic"/>) or, conservatively, any receiver
    /// when no type information is syntactically available (<see cref="FactResolution.Unresolved"/>).
    /// A receiver whose type IS syntactically known and is NOT <c>HttpClient</c> is not a false
    /// positive.
    /// </summary>
    private static SyntacticRelationCandidate? ClassifyHttpInvocation(
        InvocationExpressionSyntax invocation, SyntaxTree tree, FactId ownerId)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        var memberName = memberAccess.Name.Identifier.ValueText;
        if (memberName == "CreateClient"
            && invocation.ArgumentList.Arguments is [{ Expression: LiteralExpressionSyntax { Token.Value: string clientName } }])
        {
            return MakeCandidate(ownerId, "http-client", clientName, FactResolution.Syntactic, invocation, tree);
        }

        var httpMethod = HttpVerbPrefixes.FirstOrDefault(entry =>
                memberName.Length > entry.Prefix.Length
                && memberName.StartsWith(entry.Prefix, StringComparison.Ordinal)
                && char.IsUpper(memberName[entry.Prefix.Length]))
            .HttpMethod;
        if (httpMethod is null)
        {
            return null;
        }

        var declaredReceiverType = DeclaredReceiverTypeName(memberAccess.Expression);
        if (declaredReceiverType is not null && !string.Equals(declaredReceiverType, "HttpClient", StringComparison.Ordinal))
        {
            return null;
        }

        var resolution = declaredReceiverType is "HttpClient" ? FactResolution.Syntactic : FactResolution.Unresolved;
        var routeArgument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        var route = routeArgument switch
        {
            LiteralExpressionSyntax { Token.Value: string routeLiteral } => routeLiteral,
            { } expression => NormalizeNode(expression),
            null => "<missing>",
        };

        return MakeCandidate(ownerId, "http-call", $"http_method={httpMethod}|route={route}", resolution, invocation, tree);
    }

    /// <summary>
    /// A receiver's syntactic type name when it is a bare identifier bound to a method or constructor
    /// parameter, a non-<c>var</c> local variable or declaration-pattern variable (e.g. <c>is PaymentClient
    /// client</c>) declared in the same enclosing member, or a primary-constructor parameter, field or
    /// property declared directly on the enclosing type (RELR-07). Returns <c>null</c> (not determinable)
    /// for anything else. This is a deliberate, permanent limit, not a gap to close later: a
    /// <c>var</c>-declared local's type is not written anywhere in the syntax, so no amount of syntax-only
    /// extension can read it - only a <see cref="SemanticModel"/> binding could, which pass two does not have.
    /// </summary>
    private static string? DeclaredReceiverTypeName(ExpressionSyntax receiver)
    {
        if (receiver is not IdentifierNameSyntax identifier)
        {
            return null;
        }

        var name = identifier.Identifier.ValueText;
        var enclosingMember = receiver.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        if (enclosingMember is null)
        {
            return null;
        }

        if (enclosingMember is BaseMethodDeclarationSyntax method)
        {
            var parameter = method.ParameterList.Parameters
                .FirstOrDefault(parameter => parameter.Identifier.ValueText == name);
            if (parameter?.Type is { } parameterType)
            {
                return SimpleTypeName(parameterType);
            }
        }

        var declarator = enclosingMember.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(candidate => candidate.Identifier.ValueText == name);
        if (declarator?.Parent is VariableDeclarationSyntax { Type: { } declaredType }
            && declaredType is not IdentifierNameSyntax { Identifier.ValueText: "var" })
        {
            return SimpleTypeName(declaredType);
        }

        var pattern = enclosingMember.DescendantNodes()
            .OfType<DeclarationPatternSyntax>()
            .FirstOrDefault(candidate =>
                candidate.Designation is SingleVariableDesignationSyntax { Identifier.ValueText: var designationName }
                && designationName == name);
        if (pattern is not null)
        {
            return SimpleTypeName(pattern.Type);
        }

        return DeclaredMemberTypeName(receiver, name);
    }

    /// <summary>
    /// A primary-constructor parameter, field, or property declared directly on the receiver's
    /// enclosing type (RELR-07). Only direct members are searched - never a nested type's own
    /// same-named member, and never an inherited member, which syntax alone cannot see. A field's type
    /// genuinely cannot be <c>var</c> in valid C#, but the check mirrors the local-variable rule anyway
    /// rather than assuming well-formed input.
    /// </summary>
    private static string? DeclaredMemberTypeName(SyntaxNode receiver, string name)
    {
        if (receiver.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault() is not { } enclosingType)
        {
            return null;
        }

        var primaryParameter = enclosingType.ParameterList?.Parameters
            .FirstOrDefault(parameter => parameter.Identifier.ValueText == name);
        if (primaryParameter?.Type is { } primaryParameterType)
        {
            return SimpleTypeName(primaryParameterType);
        }

        foreach (var member in enclosingType.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax { Declaration.Type: { } fieldType } field
                    when field.Declaration.Variables.Any(variable => variable.Identifier.ValueText == name):
                    return fieldType is IdentifierNameSyntax { Identifier.ValueText: "var" } ? null : SimpleTypeName(fieldType);

                case PropertyDeclarationSyntax { Identifier.ValueText: var propertyName, Type: { } propertyType }
                    when propertyName == name:
                    return propertyType is IdentifierNameSyntax { Identifier.ValueText: "var" } ? null : SimpleTypeName(propertyType);
            }
        }

        return null;
    }

    /// <summary>
    /// A syntax node's owning candidate identity: the nearest enclosing member's own
    /// <see cref="SymbolFactId"/> (the same identity space assigned per <see cref="MemberDeclarationSyntax"/>
    /// in the main declaration loop), falling back to the document itself for code with no enclosing
    /// member (e.g. top-level-statement <c>Program.cs</c> bodies).
    /// </summary>
    private static FactId ResolveOwner(
        SyntaxNode node,
        IReadOnlyDictionary<MemberDeclarationSyntax, SymbolFactId> ownerByDeclaration,
        FactId documentFallback) =>
        node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault() is { } enclosing
            && ownerByDeclaration.TryGetValue(enclosing, out var symbolId)
            ? symbolId.ToFactId()
            : documentFallback;

    /// <summary>
    /// Fallback "calls" classification for a member-access invocation not already claimed by
    /// messaging/HTTP: the receiver's syntactic type - a locally-declared/parameter type when known,
    /// else the receiver's own identifier text (covers a static-type receiver like
    /// <c>Guid.NewGuid()</c>, which reads identically to a variable receiver in syntax alone) - must
    /// not be on the shared <see cref="RelationNoiseFilter"/> denylist (RELC-12/RELC-16).
    /// </summary>
    private static SyntacticRelationCandidate? ClassifyCallsInvocation(
        InvocationExpressionSyntax invocation, SyntaxTree tree, FactId ownerId)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        var declaredReceiverType = DeclaredReceiverTypeName(memberAccess.Expression);
        var receiverTypeName = declaredReceiverType
            ?? (memberAccess.Expression is IdentifierNameSyntax identifier ? identifier.Identifier.ValueText : null);
        if (receiverTypeName is not null && RelationNoiseFilter.IsLikelyFrameworkType(receiverTypeName))
        {
            return null;
        }

        var memberName = memberAccess.Name.Identifier.ValueText;
        var targetText = $"{NormalizeNode(memberAccess.Expression)}.{memberName}";
        return MakeCandidate(ownerId, "calls", targetText, FactResolution.Syntactic, invocation, tree) with
        {
            ReceiverText = NormalizeNode(memberAccess.Expression),
            ReceiverTypeText = declaredReceiverType,
            MemberName = memberName,
            ArgumentCount = invocation.ArgumentList.Arguments.Count,
            ArgumentTypes = ArgumentTypesFor(invocation.ArgumentList.Arguments),
        };
    }

    /// <summary>
    /// One simple type name per argument, read from the argument expression's own syntax alone (no
    /// <see cref="SemanticModel"/>): a literal's implied type, an object-creation's or cast's or
    /// <c>default(...)</c>'s named type. An argument whose type cannot be read this way (e.g. a bare
    /// identifier) yields <c>null</c> rather than a guess.
    /// </summary>
    private static ImmutableArray<string?> ArgumentTypesFor(SeparatedSyntaxList<ArgumentSyntax> arguments) =>
        arguments.Select(static argument => ArgumentTypeName(argument.Expression)).ToImmutableArray();

    private static string? ArgumentTypeName(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal => LiteralTypeName(literal),
        ObjectCreationExpressionSyntax creation => SimpleTypeName(creation.Type),
        CastExpressionSyntax cast => SimpleTypeName(cast.Type),
        DefaultExpressionSyntax defaultExpression => SimpleTypeName(defaultExpression.Type),
        _ => null,
    };

    private static string? LiteralTypeName(LiteralExpressionSyntax literal) => literal.Kind() switch
    {
        SyntaxKind.StringLiteralExpression or SyntaxKind.Utf8StringLiteralExpression => "string",
        SyntaxKind.CharacterLiteralExpression => "char",
        SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression => "bool",
        SyntaxKind.NumericLiteralExpression => NumericLiteralTypeName(literal.Token.Text),
        _ => null,
    };

    /// <summary>
    /// A numeric literal's implied type from its own suffix/shape - the same rule the C# language
    /// applies, read from the token text rather than a bound type.
    /// </summary>
    private static string NumericLiteralTypeName(string text)
    {
        if (text.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        {
            return "decimal";
        }

        if (text.EndsWith("f", StringComparison.OrdinalIgnoreCase))
        {
            return "float";
        }

        if (text.EndsWith("d", StringComparison.OrdinalIgnoreCase))
        {
            return "double";
        }

        var isHexOrBinary = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("0b", StringComparison.OrdinalIgnoreCase);
        if (!isHexOrBinary && text.Contains('.', StringComparison.Ordinal))
        {
            return "double";
        }

        if (text.EndsWith("ul", StringComparison.OrdinalIgnoreCase) || text.EndsWith("lu", StringComparison.OrdinalIgnoreCase))
        {
            return "ulong";
        }

        if (text.EndsWith("u", StringComparison.OrdinalIgnoreCase))
        {
            return "uint";
        }

        if (text.EndsWith("l", StringComparison.OrdinalIgnoreCase))
        {
            return "long";
        }

        return "int";
    }

    private static SyntacticRelationCandidate MakeCandidate(
        FactId ownerId,
        string relationKind,
        string observedTarget,
        FactResolution resolution,
        SyntaxNode node,
        SyntaxTree tree)
    {
        var span = tree.GetLineSpan(node.Span);
        return new SyntacticRelationCandidate(
            ownerId,
            relationKind,
            observedTarget,
            resolution,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            span.EndLinePosition.Line + 1,
            span.EndLinePosition.Character + 1);
    }

    /// <summary>
    /// Every base-list entry on an interface/struct/record-struct declaration is necessarily an
    /// interface (single inheritance means a class base list is the only place "inherits" can occur).
    /// </summary>
    private static bool IsInterfaceLikeBaseListOwner(MemberDeclarationSyntax declaration) => declaration switch
    {
        InterfaceDeclarationSyntax => true,
        StructDeclarationSyntax => true,
        RecordDeclarationSyntax record => record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword),
        _ => false,
    };

    /// <summary>
    /// Classifies one base-list entry per design.md's rule: interface-like owners and every
    /// non-first entry are always "implements" (certain, <see cref="FactResolution.Syntactic"/>); a
    /// class/record's first entry is "inherits" unless it looks like an interface name
    /// (<see cref="FactResolution.Heuristic"/> - a guess, not a certainty); an entry naming the
    /// declaring type's own generic type parameter can't be classified even heuristically and
    /// defaults to "implements"/<see cref="FactResolution.Unresolved"/> rather than guessing "inherits".
    /// </summary>
    private static (string RelationKind, FactResolution Resolution) ClassifyBaseListEntry(
        TypeSyntax type,
        int index,
        bool isInterfaceLikeDeclaration,
        ImmutableHashSet<string> typeParameterNames)
    {
        var simpleName = SimpleTypeName(type);
        if (typeParameterNames.Contains(simpleName))
        {
            return ("implements", FactResolution.Unresolved);
        }

        if (isInterfaceLikeDeclaration || index > 0)
        {
            return ("implements", FactResolution.Syntactic);
        }

        return LooksLikeInterfaceName(simpleName)
            ? ("implements", FactResolution.Heuristic)
            : ("inherits", FactResolution.Syntactic);
    }

    private static bool LooksLikeInterfaceName(string simpleName) =>
        simpleName.Length >= 2 && simpleName[0] == 'I' && char.IsUpper(simpleName[1]);

    private static string SimpleTypeName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => SimpleTypeName(qualified.Right),
        AliasQualifiedNameSyntax alias => SimpleTypeName(alias.Name),
        GenericNameSyntax generic => generic.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => NormalizeNode(type),
    };

    /// <summary>
    /// Primitive keyword types and other ubiquitous infrastructure types that would otherwise pass
    /// <see cref="RelationNoiseFilter.IsLikelyFrameworkType"/> (whose denylist targets BCL/collection
    /// shapes, not primitives) but are meaningless as a <c>references</c> target - e.g. a
    /// <c>CancellationToken</c> parameter naming no application component.
    /// </summary>
    private static readonly ImmutableHashSet<string> PrimitiveOrInfrastructureTypeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint", "long", "ulong",
        "short", "ushort", "string", "object", "void", "dynamic", "CancellationToken");

    /// <summary>
    /// Derives a bare simple name from an already-normalized <see cref="SymbolFact.RelevantTypeReferences"/>
    /// text (e.g. "List&lt;PaymentAuthorizer&gt;" -&gt; "List", "Payments.PaymentsBase" -&gt; "PaymentsBase"):
    /// drop any generic argument list, then take the last dotted segment.
    /// </summary>
    private static string SimpleNameFromTypeText(string text)
    {
        var genericStart = text.IndexOf('<', StringComparison.Ordinal);
        var withoutGenerics = genericStart >= 0 ? text[..genericStart] : text;
        var lastDot = withoutGenerics.LastIndexOf('.');
        return lastDot >= 0 ? withoutGenerics[(lastDot + 1)..] : withoutGenerics;
    }

    private static FactHeader Header(FactId id) =>
        FactHeader.Create(id, FactKind.Symbol, FactResolution.Syntactic, [Provenance]);
}
