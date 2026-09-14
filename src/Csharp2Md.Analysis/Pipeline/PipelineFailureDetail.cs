using System.Text.RegularExpressions;
using Csharp2Md.Analysis.Extraction;

namespace Csharp2Md.Analysis.Pipeline;

internal static partial class PipelineFailureDetail
{
    public static string Create(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var root = exception.GetBaseException();
        var typeName = root.GetType().Name;
        var message = Sanitize(root.Message);
        return message.Length == 0 ? typeName : $"{typeName}: {message}";
    }

    private static string Sanitize(string message)
    {
        var line = FirstLine(message);
        if (line.Length == 0)
        {
            return "";
        }

        line = SourceExcerptPattern().Replace(line, "");
        line = RawSyntaxPattern().Replace(line, "${prefix}");
        line = SensitiveAssignmentValuePattern().Replace(line, "${key}***");
        line = BearerValuePattern().Replace(line, "${prefix}***");
        if (SecretRedactor.TryRedact(line, out var redacted))
        {
            line = redacted.Value;
        }

        line = AbsolutePathPattern().Replace(line, "${prefix}");
        line = WhitespacePattern().Replace(line, " ").Trim(' ', '\t', ':', ';', '-', ',');
        if (line.Length == 0 || line is "***" or "[REDACTED]" || LooksLikeRawSyntax(line))
        {
            return "";
        }

        return line;
    }

    private static string FirstLine(string message)
    {
        var start = 0;
        while (start < message.Length && IsLineBreak(message[start]))
        {
            start++;
        }

        var end = start;
        while (end < message.Length && !IsLineBreak(message[end]))
        {
            end++;
        }

        return message[start..end];
    }

    private static bool IsLineBreak(char value) =>
        value is '\r' or '\n' or '\u0085' or '\u2028' or '\u2029';

    private static bool LooksLikeRawSyntax(string message)
    {
        var syntaxCandidate = RedactedSecretPattern()
            .Replace(message, "")
            .Trim(' ', '\t', ':', ';', '-', ',');
        return syntaxCandidate.Length == 0
            || syntaxCandidate.Contains('{', StringComparison.Ordinal)
            || syntaxCandidate.Contains('}', StringComparison.Ordinal)
            || syntaxCandidate.Contains("=>", StringComparison.Ordinal)
            || MemberAccessPattern().IsMatch(syntaxCandidate)
            || UnsafeSyntaxTokenPattern().IsMatch(syntaxCandidate)
            || !SafeDiagnosticLanguagePattern().IsMatch(syntaxCandidate);
    }

    [GeneratedRegex(
        """(?<prefix>^|[^\p{L}\p{N}"'])["']?(?:[A-Za-z]:[\\/]|\\\\|/).*$""",
        RegexOptions.CultureInvariant)]
    private static partial Regex AbsolutePathPattern();

    [GeneratedRegex(
        @"(?i)\b(?:source(?:\s+text)?|raw\s+syntax|syntax|excerpt)\s*[:=]\s*.*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SourceExcerptPattern();

    [GeneratedRegex(
        @"(?i)(?<prefix>^|.*?[:;]\s)(?:(?:public|private|protected|internal|static|sealed|abstract|partial|readonly|required|async|unsafe)\s+)*(?:class|struct|interface|record|enum|namespace|using|return|throw|yield|var|const|new|await|if|else|for|foreach|while|do|switch|try|catch|finally|lock)\b.*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex RawSyntaxPattern();

    [GeneratedRegex(
        """(?<key>(?i:Password|Pwd|User ID|User Id|Data Source|Initial Catalog|token|ConnectionString)\s*=\s*)(?:"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|[^;,]*)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignmentValuePattern();

    [GeneratedRegex(
        """(?<prefix>(?i:(?:Authorization:\s*)?Bearer\s+))(?:"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|[^;,]*)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex BearerValuePattern();

    [GeneratedRegex(
        @"(?i)(?:(?:Password|Pwd|User ID|User Id|Data Source|Initial Catalog|token|ConnectionString)\s*=\s*|(?:Authorization:\s*)?Bearer\s+)\*\*\*",
        RegexOptions.CultureInvariant)]
    private static partial Regex RedactedSecretPattern();

    [GeneratedRegex(@"\b[A-Za-z_]\w*(?:\s*\?\.)?\s*\.\s*[A-Za-z_]\w*\b", RegexOptions.CultureInvariant)]
    private static partial Regex MemberAccessPattern();

    [GeneratedRegex(
        @"(?:\+\+|--|==|!=|<=|>=|\+=|-=|\*=|/=|%=|&&|\|\||\?\?|\?\.|::|[+*/%&|^~=<>\[\]()`$]|\s-\s)",
        RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeSyntaxTokenPattern();

    [GeneratedRegex(
        @"(?i)\b(?:fail(?:ed|ure)?|error|invalid|unexpected|unable|cannot|could\s+not|timeout|timed\s+out|missing|not\s+found|denied|unavailable|unsupported|malformed|corrupt(?:ed|ion)?|pars(?:e|ed|ing)|load(?:ed|ing)?|open(?:ed|ing)?|read(?:ing)?|writ(?:e|ing|ten)|bind(?:ing)?|resolv(?:e|ed|ing)|analy[sz](?:e|ed|ing)|authenticat(?:e|ed|ing|ion)|publish(?:ed|ing)?|commit(?:ted|ting)?)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex SafeDiagnosticLanguagePattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
