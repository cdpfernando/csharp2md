using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Publication.Safety;

internal enum PublicationSafetyDisposition
{
    Retained,
    Redacted,
    Rejected,
}

internal sealed record PublicationSafetyResult(PublicationSafetyDisposition Disposition, string Value, string Reason);

internal static partial class PublicationSafetyScanner
{
    internal static PublicationSafetyResult ScanStructuredValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Scan(value, rejectEscapes: true);
    }

    internal static PublicationSafetyResult RedactCSharpSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var replacements = root.DescendantNodes().OfType<LiteralExpressionSyntax>()
            .Where(static literal => literal.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression)
            .Select(literal => (literal.Span, Result: Scan(literal.Token.ValueText, rejectEscapes: false)))
            .Where(static item => item.Result.Disposition != PublicationSafetyDisposition.Retained)
            .OrderByDescending(static item => item.Span.Start)
            .ToArray();

        var redacted = source;
        foreach (var replacement in replacements)
        {
            redacted = redacted.Remove(replacement.Span.Start, replacement.Span.Length)
                .Insert(replacement.Span.Start, "\"[REDACTED]\"");
        }

        return replacements.Length == 0
            ? new PublicationSafetyResult(PublicationSafetyDisposition.Retained, source, "retained")
            : new PublicationSafetyResult(PublicationSafetyDisposition.Redacted, redacted, "source-literal");
    }

    private static PublicationSafetyResult Scan(string value, bool rejectEscapes)
    {
        if (Secret().IsMatch(value))
        {
            return new(PublicationSafetyDisposition.Redacted, "[REDACTED]", "secret");
        }

        if (Path.IsPathRooted(value) || Unc().IsMatch(value))
        {
            return new(PublicationSafetyDisposition.Redacted, "[REDACTED]", "absolute-path");
        }

        if (rejectEscapes && (value.Contains("..", StringComparison.Ordinal) || value.Contains('\\')))
        {
            return new(PublicationSafetyDisposition.Rejected, "[REDACTED]", "path-escape");
        }

        return new(PublicationSafetyDisposition.Retained, value, "retained");
    }

    [GeneratedRegex("(?i)(password|secret|token|apikey|api_key)\\s*[:=]")] private static partial Regex Secret();
    [GeneratedRegex("^\\\\\\\\")] private static partial Regex Unc();
}
