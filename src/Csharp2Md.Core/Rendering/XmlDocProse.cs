using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Rendering;

/// <summary>
/// Renders a declaration's XML documentation comment as Markdown prose lines (P1-11).
/// The comment text is also kept verbatim inside the section's code fence, so nothing here
/// removes source: this is an additional human-readable view of it.
/// </summary>
internal static class XmlDocProse
{
    public static IReadOnlyList<string> Extract(SyntaxNode node)
    {
        List<string>? lines = null;

        foreach (var trivia in node.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                continue;
            }

            if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax documentation)
            {
                continue;
            }

            foreach (var element in documentation.Content.OfType<XmlElementSyntax>())
            {
                var body = Flatten(element.Content);
                if (body.Length == 0)
                {
                    continue;
                }

                var tag = element.StartTag.Name.LocalName.ValueText;
                var target = NamedTarget(element.StartTag.Attributes);

                lines ??= [];
                lines.Add(target is null ? $"**{tag}**: {body}" : $"**{tag}** `{target}`: {body}");
            }
        }

        return lines ?? (IReadOnlyList<string>)[];
    }

    private static string Flatten(IEnumerable<XmlNodeSyntax> content)
    {
        var builder = new StringBuilder();

        foreach (var node in content)
        {
            switch (node)
            {
                case XmlTextSyntax text:
                    foreach (var token in text.TextTokens)
                    {
                        builder.Append(token.ValueText);
                    }

                    break;

                case XmlEmptyElementSyntax empty:
                    builder.Append(Reference(empty.Attributes));
                    break;

                case XmlElementSyntax nested:
                    builder.Append(Flatten(nested.Content));
                    break;

                default:
                    builder.Append(node.ToString());
                    break;
            }
        }

        return CollapseWhitespace(builder.ToString());
    }

    private static string Reference(IEnumerable<XmlAttributeSyntax> attributes)
    {
        foreach (var attribute in attributes)
        {
            switch (attribute)
            {
                case XmlCrefAttributeSyntax cref:
                    return cref.Cref.ToString();
                case XmlNameAttributeSyntax name:
                    return name.Identifier.Identifier.ValueText;
            }
        }

        return string.Empty;
    }

    private static string? NamedTarget(IEnumerable<XmlAttributeSyntax> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (attribute is XmlNameAttributeSyntax name)
            {
                return name.Identifier.Identifier.ValueText;
            }
        }

        return null;
    }

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;

        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
