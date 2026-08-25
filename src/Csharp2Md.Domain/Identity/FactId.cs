using System.Text;

namespace Csharp2Md.Domain.Identity;

public readonly record struct FactId
{
    private readonly string? _value;

    public string Type { get; }

    public string Value => _value ?? throw new InvalidOperationException("An uninitialized fact ID has no value.");

    internal FactId(string type, string value)
    {
        Type = type;
        _value = value;
    }

    public override string ToString() => Value;
}

internal static class FactIdGrammar
{
    public static FactId Create(string type, params ReadOnlySpan<(string Key, string? Value)> components)
    {
        var result = new StringBuilder("id1:").Append(type);
        foreach (var (key, value) in components)
        {
            result.Append(';').Append(key).Append('=').Append(value is null ? "-" : Encode(value, key));
        }

        return new FactId(type, result.ToString());
    }

    public static string ValidateRelativePath(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);

        if (path[0] is '/' or '\\' ||
            path.Contains('\\', StringComparison.Ordinal) ||
            (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':'))
        {
            throw new ArgumentException("The path must be relative and use forward slashes.", parameterName);
        }

        var segments = path.Split('/');
        if (segments.Any(static segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("The path must not contain empty or dot segments.", parameterName);
        }

        return path;
    }

    public static string RequireCanonicalText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            value.Contains('\r', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal) ||
            value.Contains('\t', StringComparison.Ordinal) ||
            value.Contains("  ", StringComparison.Ordinal))
        {
            throw new ArgumentException("The value must be a canonical single-line value.", parameterName);
        }

        return value;
    }

    private static string Encode(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        var bytes = Encoding.UTF8.GetBytes(value);
        var encoded = new StringBuilder(bytes.Length);
        foreach (var valueByte in bytes)
        {
            if (IsUnreserved(valueByte))
            {
                encoded.Append((char)valueByte);
            }
            else
            {
                encoded.Append('%').Append(valueByte.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return encoded.ToString();
    }

    private static bool IsUnreserved(byte value) =>
        value is >= (byte)'a' and <= (byte)'z' or
            >= (byte)'A' and <= (byte)'Z' or
            >= (byte)'0' and <= (byte)'9' or
            (byte)'-' or (byte)'.' or (byte)'_' or (byte)'~';
}
