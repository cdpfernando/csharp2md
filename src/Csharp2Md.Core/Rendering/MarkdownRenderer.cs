using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Csharp2Md.Core.Rendering;

/// <summary>
/// Renders one C# document to structural Markdown: a section per type, a subsection per member,
/// bodies verbatim (P1-12).
/// </summary>
/// <remarks>
/// The renderer partitions the source file into contiguous, non-overlapping spans and emits one
/// section per span, so every byte of the file lands in exactly one section (AD-002's fidelity
/// invariant). Roslyn's <c>FullSpan</c> boundaries make that partition exact: sibling nodes in a
/// syntax list are contiguous in full-span terms, and every trivia byte belongs to exactly one
/// token. Any span the walk does not claim is still emitted, under a neutral title, rather than
/// dropped.
/// </remarks>
public sealed class MarkdownRenderer
{
    public RenderedDocument Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var root = (CompilationUnitSyntax)context.SyntaxTree.GetRoot();
        var text = context.SyntaxTree.GetText();
        var partition = new Partition(text);

        var firstMemberStart = root.Members.Count > 0
            ? root.Members[0].FullSpan.Start
            : root.EndOfFileToken.FullSpan.Start;

        // Usings, extern aliases, file-level attributes and any header comment.
        partition.Emit(firstMemberStart, "Preamble", 0);

        foreach (var member in root.Members)
        {
            EmitMember(member, 0, partition);
        }

        partition.Emit(text.Length, "Trailing", 0);

        return new RenderedDocument(
            context.RelativePath,
            DeclaredNamespace(root),
            IndexLink(context.RelativePath),
            partition.Sections);
    }

    internal static string DeclaredNamespace(CompilationUnitSyntax root) =>
        root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault() is { } declaration
            ? declaration.Name.ToString()
            : "(global)";

    internal static string IndexLink(string relativePath)
    {
        var depth = relativePath.Count(c => c is '/' or '\\');
        return depth == 0 ? "./index.md" : string.Concat(Enumerable.Repeat("../", depth)) + "index.md";
    }

    private static void EmitMember(MemberDeclarationSyntax member, int depth, Partition partition)
    {
        switch (member)
        {
            case BaseNamespaceDeclarationSyntax declaration:
                EmitNamespace(declaration, depth, partition);
                return;

            case EnumDeclarationSyntax declaration:
                EmitEnum(declaration, depth, partition);
                return;

            case TypeDeclarationSyntax declaration:
                EmitType(declaration, depth, partition);
                return;

            default:
                partition.Emit(member.FullSpan.Start, UnclaimedTitle, depth);
                partition.Emit(member.FullSpan.End, MemberTitle(member), depth, XmlDocProse.Extract(member));
                return;
        }
    }

    private static void EmitNamespace(BaseNamespaceDeclarationSyntax declaration, int depth, Partition partition)
    {
        partition.Emit(declaration.FullSpan.Start, UnclaimedTitle, depth);

        var name = declaration.Name.ToString();
        var interiorEnd = declaration is NamespaceDeclarationSyntax block
            ? block.CloseBraceToken.FullSpan.Start
            : declaration.FullSpan.End;
        var headerEnd = declaration.Members.Count > 0 ? declaration.Members[0].FullSpan.Start : interiorEnd;

        // The header section carries the declaration itself plus any namespace-scoped usings.
        partition.Emit(headerEnd, $"Namespace `{name}`", depth, XmlDocProse.Extract(declaration));

        foreach (var member in declaration.Members)
        {
            EmitMember(member, depth + 1, partition);
        }

        partition.Emit(interiorEnd, UnclaimedTitle, depth + 1);
        partition.Emit(declaration.FullSpan.End, $"End of namespace `{name}`", depth);
    }

    private static void EmitType(TypeDeclarationSyntax declaration, int depth, Partition partition)
    {
        partition.Emit(declaration.FullSpan.Start, UnclaimedTitle, depth);

        var title = $"`{TypeKeyword(declaration)} {declaration.Identifier.ValueText}`";

        // A bodyless declaration such as `public sealed record Foo(int X);` has no braces at all.
        if (declaration.OpenBraceToken.IsKind(SyntaxKind.None))
        {
            partition.Emit(declaration.FullSpan.End, title, depth, XmlDocProse.Extract(declaration));
            return;
        }

        var members = declaration.Members;
        var interiorEnd = declaration.CloseBraceToken.FullSpan.Start;
        var headerEnd = members.Count > 0 ? members[0].FullSpan.Start : interiorEnd;

        partition.Emit(headerEnd, title, depth, XmlDocProse.Extract(declaration));

        foreach (var member in members)
        {
            EmitMember(member, depth + 1, partition);
        }

        partition.Emit(interiorEnd, UnclaimedTitle, depth + 1);
        partition.Emit(declaration.FullSpan.End, $"End of {title}", depth);
    }

    // Enum members are a separated list: the commas between them are separator tokens that belong
    // to no member's full span. Each member therefore extends to the next member's start, which
    // absorbs its separator instead of stranding it in an unclaimed section.
    private static void EmitEnum(EnumDeclarationSyntax declaration, int depth, Partition partition)
    {
        partition.Emit(declaration.FullSpan.Start, UnclaimedTitle, depth);

        var title = $"`enum {declaration.Identifier.ValueText}`";
        var members = declaration.Members;
        var interiorEnd = declaration.CloseBraceToken.FullSpan.Start;
        var headerEnd = members.Count > 0 ? members[0].FullSpan.Start : interiorEnd;

        partition.Emit(headerEnd, title, depth, XmlDocProse.Extract(declaration));

        for (var i = 0; i < members.Count; i++)
        {
            var end = i + 1 < members.Count ? members[i + 1].FullSpan.Start : interiorEnd;
            partition.Emit(end, MemberTitle(members[i]), depth + 1, XmlDocProse.Extract(members[i]));
        }

        partition.Emit(declaration.FullSpan.End, $"End of {title}", depth);
    }

    private const string UnclaimedTitle = "Additional source";

    // Enums never reach here: EmitMember routes them to EmitEnum first.
    private static string TypeKeyword(TypeDeclarationSyntax declaration) =>
        declaration is RecordDeclarationSyntax record && !record.ClassOrStructKeyword.IsKind(SyntaxKind.None)
            ? $"record {record.ClassOrStructKeyword.ValueText}"
            : declaration.Keyword.ValueText;

    private static string MemberTitle(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m => $"Method `{m.Identifier.ValueText}`",
        ConstructorDeclarationSyntax c => $"Constructor `{c.Identifier.ValueText}`",
        DestructorDeclarationSyntax d => $"Finalizer `{d.Identifier.ValueText}`",
        PropertyDeclarationSyntax p => $"Property `{p.Identifier.ValueText}`",
        IndexerDeclarationSyntax => "Indexer `this[]`",
        EventDeclarationSyntax e => $"Event `{e.Identifier.ValueText}`",
        EventFieldDeclarationSyntax e => $"Event `{VariableNames(e.Declaration)}`",
        FieldDeclarationSyntax f => $"Field `{VariableNames(f.Declaration)}`",
        DelegateDeclarationSyntax d => $"Delegate `{d.Identifier.ValueText}`",
        OperatorDeclarationSyntax o => $"Operator `{o.OperatorToken.ValueText}`",
        ConversionOperatorDeclarationSyntax => "Conversion operator",
        EnumMemberDeclarationSyntax e => $"Enum member `{e.Identifier.ValueText}`",
        GlobalStatementSyntax => "Top-level statement",
        _ => "Member",
    };

    private static string VariableNames(VariableDeclarationSyntax declaration) =>
        string.Join(", ", declaration.Variables.Select(v => v.Identifier.ValueText));

    /// <summary>Accumulates sections while guaranteeing they stay contiguous and non-overlapping.</summary>
    private sealed class Partition(SourceText text)
    {
        private readonly List<RenderedSection> _sections = [];
        private int _cursor;

        public IReadOnlyList<RenderedSection> Sections => _sections;

        public void Emit(int end, string title, int depth, IReadOnlyList<string>? notes = null)
        {
            if (end <= _cursor)
            {
                return;
            }

            var span = TextSpan.FromBounds(_cursor, end);
            _sections.Add(new RenderedSection(title, Math.Min(6, depth + 2), span, text.ToString(span), notes ?? []));
            _cursor = end;
        }
    }
}
