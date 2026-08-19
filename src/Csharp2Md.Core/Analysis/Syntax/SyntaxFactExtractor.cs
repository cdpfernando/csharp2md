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
    int EndColumn);

internal sealed record SyntaxFactExtraction(
    DocumentFact Document,
    ImmutableArray<SymbolFact> Symbols,
    ImmutableArray<SyntacticRelationCandidate> RelationCandidates,
    ImmutableDictionary<SymbolFactId, ImmutableArray<string>> XmlProse);

internal static class SyntaxFactExtractor
{
    private static readonly FactProvenance Provenance = new("csharp2md.syntax", "1");

    public static SyntaxFactExtraction Extract(ProjectFactId projectId, string relativePath, string source)
    {
        var document = SourceSectionExtractor.Extract(projectId, relativePath, source);
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var symbols = new List<SymbolFact>();
        var candidates = new List<SyntacticRelationCandidate>();
        var prose = ImmutableDictionary.CreateBuilder<SymbolFactId, ImmutableArray<string>>();
        var ownerByDeclaration = new Dictionary<MemberDeclarationSyntax, SymbolFactId>();
        var methodSymbolsByName = new Dictionary<string, SymbolFactId>(StringComparer.Ordinal);

        foreach (var declaration in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            if (declaration is GlobalStatementSyntax)
            {
                continue;
            }

            var kind = DeclarationKind(declaration);
            var signature = DeclarationSignature(declaration);
            var symbolId = SymbolFactId.CreateSyntactic(projectId, relativePath, kind, signature);
            var fact = new SymbolFact(
                Header(symbolId.ToFactId()),
                symbolId,
                document.DocumentId,
                kind,
                declaration.ContainsDiagnostics,
                [],
                AttributeNames(declaration),
                ReferencedTypes(declaration));
            symbols.Add(fact);
            ownerByDeclaration[declaration] = symbolId;
            if (declaration is MethodDeclarationSyntax method)
            {
                methodSymbolsByName.TryAdd(method.Identifier.ValueText, symbolId);
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

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var ownerId = invocation.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault() is { } enclosing
                && ownerByDeclaration.TryGetValue(enclosing, out var enclosingSymbolId)
                ? enclosingSymbolId.ToFactId()
                : document.DocumentId.ToFactId();

            var messagingCandidates = ClassifyMessagingInvocation(invocation, root.SyntaxTree, ownerId, methodSymbolsByName).ToList();
            candidates.AddRange(messagingCandidates);
            if (messagingCandidates.Count > 0)
            {
                continue;
            }

            if (ClassifyHttpInvocation(invocation, root.SyntaxTree, ownerId) is { } httpCandidate)
            {
                candidates.Add(httpCandidate);
            }
        }

        var canonicalSymbols = symbols
            .OrderBy(static symbol => symbol.SymbolId.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        var canonicalCandidates = candidates
            .Distinct()
            .OrderBy(static candidate => candidate.OwnerId.Value, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.RelationKind, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.ObservedTarget, StringComparer.Ordinal)
            .ToImmutableArray();
        var completedDocument = document with
        {
            SymbolIds = canonicalSymbols.Select(static symbol => symbol.SymbolId).ToImmutableArray(),
        };

        return new SyntaxFactExtraction(completedDocument, canonicalSymbols, canonicalCandidates, prose.ToImmutable());
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

            tokens.Add(token.Text);
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
    private static IEnumerable<SyntacticRelationCandidate> ClassifyMessagingInvocation(
        InvocationExpressionSyntax invocation,
        SyntaxTree tree,
        FactId ownerId,
        IReadOnlyDictionary<string, SymbolFactId> methodSymbolsByName)
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
            string? targetSimpleName = explicitTypeArgument is not null
                ? SimpleTypeName(explicitTypeArgument)
                : invocation.ArgumentList.Arguments is [{ Expression: ObjectCreationExpressionSyntax creation }, ..]
                    ? SimpleTypeName(creation.Type)
                    : null;

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
    /// A receiver's syntactic type name when it is a bare identifier bound to a parameter or a
    /// non-<c>var</c> local variable declared in the same enclosing member. Returns <c>null</c> (not
    /// determinable) for anything else, including <c>var</c>-declared locals.
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

        return null;
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

    private static FactHeader Header(FactId id) =>
        FactHeader.Create(id, FactKind.Symbol, FactResolution.Syntactic, [Provenance]);
}
