using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Analysis.Syntax;

internal sealed record SyntacticRelationCandidate(
    SymbolFactId OwnerId,
    string RelationKind,
    string ObservedTarget);

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

            var documentation = XmlDocProse.Extract(declaration).ToImmutableArray();
            if (!documentation.IsEmpty)
            {
                prose[symbolId] = documentation;
            }

            if (declaration is BaseTypeDeclarationSyntax { BaseList: { } baseList })
            {
                candidates.AddRange(baseList.Types.Select(type =>
                    new SyntacticRelationCandidate(symbolId, "base-or-interface", NormalizeNode(type.Type))));
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

    private static string DeclarationKind(MemberDeclarationSyntax declaration) => declaration switch
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

    private static string DeclarationSignature(MemberDeclarationSyntax declaration)
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

            if (token.Text.Length > 0)
            {
                tokens.Add(token.Text);
            }
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
        var header = string.Join(' ', tokens);
        return prefix.Length == 0 ? header : $"{prefix}/{header}";
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

    private static FactHeader Header(FactId id) =>
        FactHeader.Create(id, FactKind.Symbol, FactResolution.Syntactic, [Provenance]);
}
