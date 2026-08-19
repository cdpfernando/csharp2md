using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Csharp2Md.Core.Analysis.Syntax;

internal static class SourceSectionExtractor
{
    private static readonly FactProvenance Provenance = new("csharp2md.syntax", "1");

    public static DocumentFact Extract(ProjectFactId projectId, string relativePath, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(source);

        var documentId = DocumentFactId.Create(projectId, relativePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var root = (CompilationUnitSyntax)syntaxTree.GetRoot();
        var partition = new Partition(documentId, syntaxTree.GetText());

        var firstMemberStart = root.Members.Count > 0
            ? root.Members[0].FullSpan.Start
            : root.EndOfFileToken.FullSpan.Start;
        partition.Emit(firstMemberStart, "preamble");

        foreach (var member in root.Members)
        {
            EmitMember(member, partition);
        }

        partition.Emit(source.Length, "trailing");
        var sections = partition.Sections;
        return new DocumentFact(
            Header(documentId.ToFactId(), FactKind.Document),
            documentId,
            projectId,
            relativePath,
            sections,
            []);
    }

    private static void EmitMember(MemberDeclarationSyntax member, Partition partition)
    {
        switch (member)
        {
            case BaseNamespaceDeclarationSyntax declaration:
                EmitNamespace(declaration, partition);
                break;
            case EnumDeclarationSyntax declaration:
                EmitEnum(declaration, partition);
                break;
            case TypeDeclarationSyntax declaration:
                EmitType(declaration, partition);
                break;
            default:
                partition.Emit(member.FullSpan.Start, "additional-source");
                partition.Emit(member.FullSpan.End, MemberKind(member));
                break;
        }
    }

    private static void EmitNamespace(BaseNamespaceDeclarationSyntax declaration, Partition partition)
    {
        partition.Emit(declaration.FullSpan.Start, "additional-source");
        var interiorEnd = declaration is NamespaceDeclarationSyntax block
            ? block.CloseBraceToken.FullSpan.Start
            : declaration.FullSpan.End;
        var headerEnd = declaration.Members.Count > 0 ? declaration.Members[0].FullSpan.Start : interiorEnd;
        partition.Emit(headerEnd, "namespace");

        foreach (var member in declaration.Members)
        {
            EmitMember(member, partition);
        }

        partition.Emit(interiorEnd, "additional-source");
        partition.Emit(declaration.FullSpan.End, "namespace-end");
    }

    private static void EmitType(TypeDeclarationSyntax declaration, Partition partition)
    {
        partition.Emit(declaration.FullSpan.Start, "additional-source");
        var kind = declaration.Keyword.ValueText.Length == 0 ? "type" : declaration.Keyword.ValueText;
        if (declaration.OpenBraceToken.IsKind(SyntaxKind.None))
        {
            partition.Emit(declaration.FullSpan.End, kind);
            return;
        }

        var interiorEnd = declaration.CloseBraceToken.FullSpan.Start;
        var headerEnd = declaration.Members.Count > 0 ? declaration.Members[0].FullSpan.Start : interiorEnd;
        partition.Emit(headerEnd, kind);
        foreach (var member in declaration.Members)
        {
            EmitMember(member, partition);
        }

        partition.Emit(interiorEnd, "additional-source");
        partition.Emit(declaration.FullSpan.End, $"{kind}-end");
    }

    private static void EmitEnum(EnumDeclarationSyntax declaration, Partition partition)
    {
        partition.Emit(declaration.FullSpan.Start, "additional-source");
        var interiorEnd = declaration.CloseBraceToken.FullSpan.Start;
        var headerEnd = declaration.Members.Count > 0 ? declaration.Members[0].FullSpan.Start : interiorEnd;
        partition.Emit(headerEnd, "enum");

        for (var index = 0; index < declaration.Members.Count; index++)
        {
            var end = index + 1 < declaration.Members.Count
                ? declaration.Members[index + 1].FullSpan.Start
                : interiorEnd;
            partition.Emit(end, "enum-member");
        }

        partition.Emit(declaration.FullSpan.End, "enum-end");
    }

    private static string MemberKind(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax => "method",
        ConstructorDeclarationSyntax => "constructor",
        DestructorDeclarationSyntax => "destructor",
        PropertyDeclarationSyntax => "property",
        IndexerDeclarationSyntax => "indexer",
        EventDeclarationSyntax or EventFieldDeclarationSyntax => "event",
        FieldDeclarationSyntax => "field",
        DelegateDeclarationSyntax => "delegate",
        OperatorDeclarationSyntax => "operator",
        ConversionOperatorDeclarationSyntax => "conversion-operator",
        GlobalStatementSyntax => "top-level-statement",
        _ => "member",
    };

    private static FactHeader Header(FactId id, FactKind kind) =>
        FactHeader.Create(id, kind, FactResolution.Syntactic, [Provenance]);

    private sealed class Partition(DocumentFactId documentId, SourceText text)
    {
        private readonly Dictionary<string, int> _ordinals = new(StringComparer.Ordinal);
        private readonly List<SourceSectionFact> _sections = [];
        private int _cursor;

        public ImmutableArray<SourceSectionFact> Sections => _sections.ToImmutableArray();

        public void Emit(int end, string kind)
        {
            if (end <= _cursor)
            {
                return;
            }

            var ordinal = _ordinals.TryGetValue(kind, out var prior) ? prior + 1 : 1;
            _ordinals[kind] = ordinal;
            var span = TextSpan.FromBounds(_cursor, end);
            var id = FactIdGrammar.Create(
                "source-section",
                ("document", documentId.Value),
                ("kind", kind),
                ("ordinal", ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            _sections.Add(new SourceSectionFact(
                Header(id, FactKind.SourceSection),
                documentId,
                kind,
                ordinal,
                span.Start,
                span.Length,
                text.ToString(span)));
            _cursor = end;
        }
    }
}
