using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Literals;

public readonly record struct DocumentId
{
    private readonly string? _value;

    public string Value => _value ?? throw new InvalidOperationException("An uninitialized document identity has no value.");

    public static DocumentId Create(string logicalKey)
    {
        var canonical = FactIdGrammar.RequireCanonicalText(logicalKey, nameof(logicalKey));
        if (LooksLikeAbsolutePath(canonical))
        {
            throw new ArgumentException("A document identity must be an opaque logical key, not an absolute path.", nameof(logicalKey));
        }

        return new DocumentId(canonical);
    }

    private DocumentId(string value) => _value = value;

    public override string ToString() => Value;

    private static bool LooksLikeAbsolutePath(string value) =>
        value[0] is '/' or '\\' || (value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':');
}

public readonly record struct SourceSpan
{
    public int StartLine { get; }

    public int StartColumn { get; }

    public int EndLine { get; }

    public int EndColumn { get; }

    public SourceSpan(int startLine, int startColumn, int endLine, int endColumn)
    {
        ValidatePosition(startLine, startColumn, nameof(startLine));
        ValidatePosition(endLine, endColumn, nameof(endLine));
        if ((endLine, endColumn).CompareTo((startLine, startColumn)) < 0)
        {
            throw new ArgumentException("A source span's end must not precede its start.", nameof(endLine));
        }

        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    private static void ValidatePosition(int line, int column, string parameterName)
    {
        if (line <= 0 || column <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Source span coordinates must be one-based.");
        }
    }
}

public readonly record struct DocumentHash
{
    public const int Length = 64;

    private readonly string? _value;

    public string Value => _value ?? throw new InvalidOperationException("An uninitialized document hash has no value.");

    public static DocumentHash Create(string digest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(digest, nameof(digest));
        if (digest.Length != Length || !digest.All(IsLowercaseHexDigit))
        {
            throw new ArgumentException($"A document hash must be a {Length}-character lowercase hexadecimal digest (SHA-256).", nameof(digest));
        }

        return new DocumentHash(digest);
    }

    private DocumentHash(string value) => _value = value;

    public override string ToString() => Value;

    private static bool IsLowercaseHexDigit(char c) => c is >= '0' and <= '9' or >= 'a' and <= 'f';
}
