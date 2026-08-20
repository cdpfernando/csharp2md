using System.Collections.Frozen;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.DataAccess.Sql;

/// <summary>
/// What the bounded reader proved about one SQL statement. Anything it could not prove stays
/// <c>null</c>: the reader never returns a name it did not read.
/// </summary>
internal readonly record struct SqlStatement(
    DatabaseOperation Operation,
    DatabaseObjectKind ObjectKind,
    string? Target)
{
    private readonly ImmutableArray<string> _writtenColumns;

    /// <summary>
    /// DAD-24 and DAD-25: the columns the statement proves it writes - an <c>INSERT</c> column list or
    /// an <c>UPDATE ... SET</c> assignment list. Empty whenever the list is absent or unreadable.
    /// </summary>
    public ImmutableArray<string> WrittenColumns
    {
        get => _writtenColumns.IsDefault ? [] : _writtenColumns;
        init => _writtenColumns = value;
    }
}

/// <summary>What one token of a SQL statement is, as far as this reader distinguishes.</summary>
internal enum SqlTokenKind
{
    /// <summary>An unquoted identifier or keyword.</summary>
    Word,

    /// <summary>A parameter reference such as <c>@id</c> or <c>:id</c>.</summary>
    Parameter,

    /// <summary>A quoted string or a number.</summary>
    Literal,

    /// <summary>A comparison operator.</summary>
    Operator,

    /// <summary>Anything else, one character at a time - brackets, quotes, commas, stars.</summary>
    Other,
}

/// <summary>One token of a SQL statement, carrying its source text verbatim.</summary>
internal readonly record struct SqlToken(SqlTokenKind Kind, string Text);

/// <summary>
/// The bounded SQL tokenizer. Pure, dependency-free, and deliberately narrow: it recognises the
/// statement shapes it can prove and reports everything else as unread rather than guessing.
/// </summary>
/// <remarks>
/// Grammar coverage is bounded on purpose (context.md, "SQL analysis approach"). Quoted or bracketed
/// identifiers, subquery targets, and block comments are outside it, so they yield no target instead of
/// a guessed one. Identifiers are recorded verbatim, never case-normalized, per AD-014.
/// </remarks>
internal static class SqlStatementReader
{
    /// <summary>DAD-21: the seven verbs P1 recognises, and the operation each one derives.</summary>
    /// <remarks>
    /// <c>MERGE</c> derives <see cref="DatabaseOperation.Update"/>: it is a write, and
    /// <see cref="DatabaseOperation"/> has no upsert member. The spec does not name an operation for it.
    /// </remarks>
    private static readonly FrozenDictionary<string, DatabaseOperation> OperationByVerb =
        new Dictionary<string, DatabaseOperation>(StringComparer.OrdinalIgnoreCase)
        {
            ["SELECT"] = DatabaseOperation.Read,
            ["INSERT"] = DatabaseOperation.Insert,
            ["UPDATE"] = DatabaseOperation.Update,
            ["DELETE"] = DatabaseOperation.Delete,
            ["MERGE"] = DatabaseOperation.Update,
            ["EXEC"] = DatabaseOperation.Execute,
            ["CALL"] = DatabaseOperation.Execute,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads what <paramref name="sql"/> proves. Returns <c>false</c> when its first significant token
    /// is not one of the recognised verbs, which is what keeps an ordinary sentence containing the word
    /// "update" from being read as a statement.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<char> sql, out SqlStatement statement)
    {
        statement = default;

        var tokens = Tokenize(sql);
        if (tokens is not [{ Kind: SqlTokenKind.Word } first, ..]
            || !OperationByVerb.TryGetValue(first.Text, out var operation))
        {
            return false;
        }

        var (target, afterTarget) = ReadTarget(first.Text, tokens);
        statement = new SqlStatement(operation, ObjectKindOfVerb(first.Text), target)
        {
            WrittenColumns = ReadWrittenColumns(first.Text, tokens, afterTarget),
        };
        return true;
    }

    /// <summary>
    /// DAD-24 and DAD-25: the written column list, read only from the shape each verb proves - the
    /// parenthesised list after an <c>INSERT</c> target, or the <c>SET</c> assignment list of an
    /// <c>UPDATE</c>. Every other verb writes no column list.
    /// </summary>
    private static ImmutableArray<string> ReadWrittenColumns(
        string verb, List<SqlToken> tokens, int afterTarget) =>
        verb.ToUpperInvariant() switch
        {
            "INSERT" => ReadParenthesisedColumnList(tokens, afterTarget),
            "UPDATE" => ReadAssignedColumns(tokens),
            _ => [],
        };

    /// <summary>
    /// The plain-identifier list in <c>(a, b, c)</c>. A list holding anything else, or one that never
    /// closes, yields nothing at all rather than the prefix it managed to read.
    /// </summary>
    private static ImmutableArray<string> ReadParenthesisedColumnList(List<SqlToken> tokens, int index)
    {
        if (index < 0 || index >= tokens.Count
            || tokens[index] is not { Kind: SqlTokenKind.Other, Text: "(" })
        {
            return [];
        }

        var columns = ImmutableArray.CreateBuilder<string>();
        var cursor = index + 1;
        while (cursor + 1 < tokens.Count && tokens[cursor].Kind == SqlTokenKind.Word)
        {
            columns.Add(tokens[cursor].Text);
            cursor++;
            if (tokens[cursor] is { Kind: SqlTokenKind.Other, Text: ")" })
            {
                return columns.ToImmutable();
            }

            if (tokens[cursor] is not { Kind: SqlTokenKind.Other, Text: "," })
            {
                return [];
            }

            cursor++;
        }

        return [];
    }

    /// <summary>
    /// The columns assigned by an <c>UPDATE ... SET</c> clause: an identifier followed by <c>=</c> at
    /// the start of an assignment, at parenthesis depth zero. Assigned values never qualify, and the
    /// scan stops where the clause does.
    /// </summary>
    private static ImmutableArray<string> ReadAssignedColumns(List<SqlToken> tokens)
    {
        var index = IndexAfterKeyword(tokens, "SET");
        if (index < 0)
        {
            return [];
        }

        var columns = ImmutableArray.CreateBuilder<string>();
        var depth = 0;
        var startsAssignment = true;
        for (var cursor = index; cursor < tokens.Count; cursor++)
        {
            var token = tokens[cursor];
            if (token is { Kind: SqlTokenKind.Other, Text: "(" })
            {
                depth++;
            }
            else if (token is { Kind: SqlTokenKind.Other, Text: ")" })
            {
                depth--;
            }
            else if (depth == 0 && token is { Kind: SqlTokenKind.Other, Text: "," })
            {
                startsAssignment = true;
                continue;
            }
            else if (depth == 0 && EndsAssignmentList(token))
            {
                break;
            }
            else if (depth == 0 && startsAssignment
                && token.Kind == SqlTokenKind.Word
                && cursor + 1 < tokens.Count
                && tokens[cursor + 1] is { Kind: SqlTokenKind.Operator, Text: "=" })
            {
                columns.Add(token.Text);
            }

            startsAssignment = false;
        }

        return columns.ToImmutable();
    }

    private static bool EndsAssignmentList(SqlToken token) =>
        token is { Kind: SqlTokenKind.Other, Text: ";" }
            || (token.Kind == SqlTokenKind.Word
                && token.Text.Equals("WHERE", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// DAD-23: <c>EXEC</c> and <c>CALL</c> prove the target is a procedure. Every other verb proves an
    /// access without proving what kind of object it touched.
    /// </summary>
    private static DatabaseObjectKind ObjectKindOfVerb(string verb) =>
        verb.Equals("EXEC", StringComparison.OrdinalIgnoreCase)
            || verb.Equals("CALL", StringComparison.OrdinalIgnoreCase)
            ? DatabaseObjectKind.Procedure
            : DatabaseObjectKind.Unknown;

    /// <summary>
    /// DAD-22: the target that follows each verb's anchor keyword - <c>FROM</c>, <c>INTO</c>, or the verb
    /// itself for <c>UPDATE</c>, <c>EXEC</c> and <c>CALL</c>.
    /// </summary>
    /// <remarks>
    /// <c>UPDATE</c> additionally requires a <c>SET</c> keyword before its target counts as proven.
    /// Without that anchor a message such as "Update failed for order" would mint a table named
    /// <c>failed</c>; requiring <c>SET</c> keeps the reader inside what the text proves.
    /// </remarks>
    private static (string? Name, int Next) ReadTarget(string verb, List<SqlToken> tokens)
    {
        var index = verb.ToUpperInvariant() switch
        {
            "SELECT" or "DELETE" => IndexAfterKeyword(tokens, "FROM"),
            "INSERT" or "MERGE" => IndexAfterKeyword(tokens, "INTO"),
            "UPDATE" => IndexAfterKeyword(tokens, "SET") < 0 ? -1 : 1,
            _ => 1,
        };

        return ReadQualifiedIdentifier(tokens, index);
    }

    /// <summary>The index just past the first occurrence of <paramref name="keyword"/>, or -1.</summary>
    private static int IndexAfterKeyword(List<SqlToken> tokens, string keyword)
    {
        for (var index = 1; index < tokens.Count; index++)
        {
            if (tokens[index] is { Kind: SqlTokenKind.Word } token
                && token.Text.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return index + 1;
            }
        }

        return -1;
    }

    /// <summary>
    /// A plain identifier, optionally dot-qualified, recorded verbatim, plus the index just past it.
    /// Anything else - a bracketed or quoted name, a parameter, an opening parenthesis - is unreadable
    /// and yields <c>null</c>.
    /// </summary>
    private static (string? Name, int Next) ReadQualifiedIdentifier(List<SqlToken> tokens, int index)
    {
        if (index < 0 || index >= tokens.Count || tokens[index].Kind != SqlTokenKind.Word)
        {
            return (null, index);
        }

        var name = tokens[index].Text;
        var cursor = index + 1;
        while (cursor + 1 < tokens.Count
            && tokens[cursor] is { Kind: SqlTokenKind.Other, Text: "." }
            && tokens[cursor + 1].Kind == SqlTokenKind.Word)
        {
            name = $"{name}.{tokens[cursor + 1].Text}";
            cursor += 2;
        }

        return (name, cursor);
    }

    /// <summary>
    /// Splits <paramref name="sql"/> into tokens, skipping whitespace and <c>--</c> line comments.
    /// </summary>
    private static List<SqlToken> Tokenize(ReadOnlySpan<char> sql)
    {
        var tokens = new List<SqlToken>();
        var index = 0;
        while (index < sql.Length)
        {
            var current = sql[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
            }
            else if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                while (index < sql.Length && sql[index] is not ('\n' or '\r'))
                {
                    index++;
                }
            }
            else if (current == '\'')
            {
                tokens.Add(new SqlToken(SqlTokenKind.Literal, ReadStringLiteral(sql, ref index)));
            }
            else if (char.IsAsciiDigit(current))
            {
                var start = index;
                while (index < sql.Length && (char.IsAsciiDigit(sql[index]) || sql[index] == '.'))
                {
                    index++;
                }

                tokens.Add(new SqlToken(SqlTokenKind.Literal, sql[start..index].ToString()));
            }
            else if (current is '@' or ':')
            {
                var start = index++;
                while (index < sql.Length && IsIdentifierPart(sql[index]))
                {
                    index++;
                }

                tokens.Add(new SqlToken(SqlTokenKind.Parameter, sql[start..index].ToString()));
            }
            else if (IsIdentifierStart(current))
            {
                var start = index;
                while (index < sql.Length && IsIdentifierPart(sql[index]))
                {
                    index++;
                }

                tokens.Add(new SqlToken(SqlTokenKind.Word, sql[start..index].ToString()));
            }
            else if (ReadOperator(sql, index) is { } operatorText)
            {
                tokens.Add(new SqlToken(SqlTokenKind.Operator, operatorText));
                index += operatorText.Length;
            }
            else
            {
                tokens.Add(new SqlToken(SqlTokenKind.Other, current.ToString()));
                index++;
            }
        }

        return tokens;
    }

    /// <summary>A <c>'...'</c> literal, honouring the doubled-quote escape. Unterminated runs to the end.</summary>
    private static string ReadStringLiteral(ReadOnlySpan<char> sql, ref int index)
    {
        var start = index++;
        while (index < sql.Length)
        {
            if (sql[index] != '\'')
            {
                index++;
            }
            else if (index + 1 < sql.Length && sql[index + 1] == '\'')
            {
                index += 2;
            }
            else
            {
                index++;
                break;
            }
        }

        return sql[start..index].ToString();
    }

    /// <summary>The comparison operator at <paramref name="index"/>, longest match first, or <c>null</c>.</summary>
    private static string? ReadOperator(ReadOnlySpan<char> sql, int index)
    {
        if (index + 1 < sql.Length)
        {
            var pair = sql.Slice(index, 2);
            if (pair is "<=" or ">=" or "<>" or "!=")
            {
                return pair.ToString();
            }
        }

        return sql[index] switch
        {
            '=' or '<' or '>' => sql[index].ToString(),
            _ => null,
        };
    }

    private static bool IsIdentifierStart(char value) => char.IsAsciiLetter(value) || value == '_';

    private static bool IsIdentifierPart(char value) => char.IsAsciiLetterOrDigit(value) || value == '_';
}
