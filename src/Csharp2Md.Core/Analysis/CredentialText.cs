namespace Csharp2Md.Core.Analysis;

/// <summary>
/// DAD-15: the one notion of "this text carries a credential" the analysis stage shares.
/// <c>SqlTextAnalyzer</c> uses it to withhold a statement's text from a relation detail, and
/// <c>SyntaxFactExtractor</c> uses it to mask a literal out of a symbol signature and the symbol id
/// derived from it. One predicate keeps the two guards from drifting apart.
/// </summary>
internal static class CredentialText
{
    /// <summary>
    /// Connection-string keys whose value, when it is a literal rather than a parameter, is a
    /// credential.
    /// </summary>
    private static readonly string[] CredentialKeys =
        ["password", "pwd", "accountkey", "sharedaccesskey", "accesstoken"];

    /// <summary>
    /// Whether the text assigns a literal value to a credential key. <c>Password=hunter2</c> does;
    /// <c>SET Password = @password</c> does not, because a parameter reference carries no secret.
    /// </summary>
    public static bool Carries(ReadOnlySpan<char> text)
    {
        foreach (var key in CredentialKeys)
        {
            var rest = text;
            while (rest.IndexOf(key, StringComparison.OrdinalIgnoreCase) is var index and >= 0)
            {
                rest = rest[(index + key.Length)..];
                if (AssignsLiteralValue(rest))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Whether what follows a credential key is <c>= &lt;value&gt;</c> with a value that is not a
    /// parameter reference.
    /// </summary>
    private static bool AssignsLiteralValue(ReadOnlySpan<char> rest)
    {
        var cursor = SkipWhitespace(rest, 0);
        if (cursor >= rest.Length || rest[cursor] != '=')
        {
            return false;
        }

        cursor = SkipWhitespace(rest, cursor + 1);
        return cursor < rest.Length && rest[cursor] is not ('@' or ':');
    }

    private static int SkipWhitespace(ReadOnlySpan<char> text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }
}
