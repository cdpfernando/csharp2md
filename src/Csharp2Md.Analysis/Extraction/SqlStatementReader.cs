using Csharp2Md.Domain.Facets;

namespace Csharp2Md.Analysis.Extraction;

/// <summary>The parsed shape of a constant SQL statement, as read by <see cref="SqlStatementReader"/>.</summary>
internal readonly record struct SqlStatementFacts(DataOperationKind Operation, string Target, ImmutableArray<string> Columns);

/// <summary>
/// A bounded reader that turns a constant SQL statement into an operation kind, a target identifier
/// and a column list, or rejects it. This is a closed, deliberately small grammar - not a general SQL
/// parser - covering exactly <c>SELECT</c>, <c>INSERT</c>, <c>UPDATE</c>, <c>DELETE</c> and
/// <c>EXEC</c>/<c>EXECUTE</c>. Anything else, or a target that cannot be read as a bare identifier, is
/// reported as unreadable rather than guessed.
/// </summary>
internal static class SqlStatementReader
{
    private static readonly char[] Whitespace = [' ', '\t', '\r', '\n'];

    public static bool TryRead(string statement, out SqlStatementFacts facts)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var trimmed = statement.Trim();
        var keywordEnd = trimmed.IndexOfAny(Whitespace);
        var leadingKeyword = keywordEnd < 0 ? trimmed : trimmed[..keywordEnd];

        if (string.Equals(leadingKeyword, "SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadSelect(trimmed, out facts);
        }

        if (string.Equals(leadingKeyword, "INSERT", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadInsert(trimmed, out facts);
        }

        if (string.Equals(leadingKeyword, "UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadUpdate(trimmed, out facts);
        }

        if (string.Equals(leadingKeyword, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadDelete(trimmed, out facts);
        }

        if (string.Equals(leadingKeyword, "EXEC", StringComparison.OrdinalIgnoreCase)
            || string.Equals(leadingKeyword, "EXECUTE", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadExecute(trimmed, leadingKeyword.Length, out facts);
        }

        facts = default;
        return false;
    }

    private static bool TryReadSelect(string statement, out SqlStatementFacts facts)
    {
        var fromIndex = IndexOfKeyword(statement, "FROM", "SELECT".Length);
        if (fromIndex < 0)
        {
            facts = default;
            return false;
        }

        var selectList = statement["SELECT".Length..fromIndex].Trim();
        var remainder = statement[(fromIndex + "FROM".Length)..];
        if (!TryReadLeadingIdentifier(remainder, out var target))
        {
            facts = default;
            return false;
        }

        var columns = string.Equals(selectList, "*", StringComparison.Ordinal)
            ? ImmutableArray<string>.Empty
            : SplitList(selectList);

        facts = new SqlStatementFacts(DataOperationKind.Read, target, columns);
        return true;
    }

    private static bool TryReadInsert(string statement, out SqlStatementFacts facts)
    {
        var intoIndex = IndexOfKeyword(statement, "INTO", "INSERT".Length);
        if (intoIndex < 0)
        {
            facts = default;
            return false;
        }

        var afterInto = statement[(intoIndex + "INTO".Length)..];
        var openParen = afterInto.IndexOf('(');
        if (openParen < 0 || !TryNormalizeIdentifier(afterInto[..openParen], out var target))
        {
            facts = default;
            return false;
        }

        var closeParen = afterInto.IndexOf(')', openParen);
        if (closeParen < 0)
        {
            facts = default;
            return false;
        }

        var columns = SplitList(afterInto[(openParen + 1)..closeParen]);
        facts = new SqlStatementFacts(DataOperationKind.Insert, target, columns);
        return true;
    }

    private static bool TryReadUpdate(string statement, out SqlStatementFacts facts)
    {
        var setIndex = IndexOfKeyword(statement, "SET", "UPDATE".Length);
        if (setIndex < 0 || !TryNormalizeIdentifier(statement["UPDATE".Length..setIndex], out var target))
        {
            facts = default;
            return false;
        }

        var afterSet = statement[(setIndex + "SET".Length)..];
        var whereIndex = IndexOfKeyword(afterSet, "WHERE", 0);
        var assignments = whereIndex < 0 ? afterSet : afterSet[..whereIndex];

        var columns = assignments
            .Split(',')
            .Select(static assignment => assignment.Split('=')[0].Trim())
            .Where(static name => name.Length > 0)
            .ToImmutableArray();
        if (columns.IsEmpty)
        {
            facts = default;
            return false;
        }

        facts = new SqlStatementFacts(DataOperationKind.Update, target, columns);
        return true;
    }

    private static bool TryReadDelete(string statement, out SqlStatementFacts facts)
    {
        var fromIndex = IndexOfKeyword(statement, "FROM", "DELETE".Length);
        if (fromIndex < 0)
        {
            facts = default;
            return false;
        }

        var remainder = statement[(fromIndex + "FROM".Length)..];
        if (!TryReadLeadingIdentifier(remainder, out var target))
        {
            facts = default;
            return false;
        }

        facts = new SqlStatementFacts(DataOperationKind.Delete, target, ImmutableArray<string>.Empty);
        return true;
    }

    private static bool TryReadExecute(string statement, int keywordLength, out SqlStatementFacts facts)
    {
        var remainder = statement[keywordLength..];
        if (!TryReadLeadingIdentifier(remainder, out var target))
        {
            facts = default;
            return false;
        }

        facts = new SqlStatementFacts(DataOperationKind.Execute, target, ImmutableArray<string>.Empty);
        return true;
    }

    /// <summary>Reads the single whitespace-delimited token at the start of <paramref name="remainder"/>.</summary>
    private static bool TryReadLeadingIdentifier(string remainder, out string identifier)
    {
        var trimmed = remainder.TrimStart(Whitespace);
        var end = trimmed.IndexOfAny(Whitespace);
        var token = end < 0 ? trimmed : trimmed[..end];
        return TryNormalizeIdentifier(token, out identifier);
    }

    /// <summary>
    /// Unquotes a bracketed or double-quoted identifier (PK-19) and rejects anything that is not a
    /// bare identifier: empty text, an interpolation hole, a parameter marker, embedded whitespace or
    /// a stray parenthesis (PK-39).
    /// </summary>
    private static bool TryNormalizeIdentifier(string candidate, out string identifier)
    {
        var value = candidate.Trim();
        if (value.Length >= 2 && value[0] == '[' && value[^1] == ']')
        {
            value = value[1..^1];
        }
        else if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        if (value.Length == 0 || value.IndexOfAny(['{', '}', '@', ':', '(', ')', ' ', '\t', '\r', '\n']) >= 0)
        {
            identifier = string.Empty;
            return false;
        }

        identifier = value;
        return true;
    }

    private static ImmutableArray<string> SplitList(string list) =>
        [.. list
            .Split(',')
            .Select(static entry => entry.Trim())
            .Where(static entry => entry.Length > 0)];

    /// <summary>Finds a whole-word, case-insensitive occurrence of <paramref name="keyword"/> at or after <paramref name="startIndex"/>.</summary>
    private static int IndexOfKeyword(string text, string keyword, int startIndex)
    {
        var index = startIndex;
        while (index <= text.Length - keyword.Length)
        {
            var candidateIndex = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase);
            if (candidateIndex < 0)
            {
                return -1;
            }

            var precededByBoundary = candidateIndex == 0 || !char.IsLetterOrDigit(text[candidateIndex - 1]);
            var afterKeyword = candidateIndex + keyword.Length;
            var followedByBoundary = afterKeyword == text.Length || !char.IsLetterOrDigit(text[afterKeyword]);
            if (precededByBoundary && followedByBoundary)
            {
                return candidateIndex;
            }

            index = candidateIndex + 1;
        }

        return -1;
    }
}
