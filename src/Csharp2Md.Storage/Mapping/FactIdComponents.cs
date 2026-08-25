using System.Globalization;
using System.Text;

namespace Csharp2Md.Storage.Mapping;

internal static class FactIdComponents
{
    public static Dictionary<string, string> Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var body = StripPrefix(value);
        var components = new Dictionary<string, string>(StringComparer.Ordinal);
        if (body.Length == 0)
        {
            return components;
        }

        foreach (var part in body.Split(';'))
        {
            var separator = part.IndexOf('=');
            if (separator < 0)
            {
                throw new InvalidOperationException($"Fact id component '{part}' is missing '='.");
            }

            var key = part[..separator];
            var raw = part[(separator + 1)..];
            if (raw == "-")
            {
                continue;
            }

            components[key] = PercentDecode(raw);
        }

        return components;
    }

    public static string TypeOf(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.StartsWith("id1:", StringComparison.Ordinal))
        {
            var start = 4;
            var end = value.IndexOf(';', start);
            return end < 0 ? value[start..] : value[start..end];
        }

        throw new InvalidOperationException($"Value '{value}' is not a canonical fact id.");
    }

    private static string StripPrefix(string value)
    {
        if (value.StartsWith("sig1;", StringComparison.Ordinal))
        {
            return value[5..];
        }

        if (value.StartsWith("id1:", StringComparison.Ordinal))
        {
            var semicolon = value.IndexOf(';');
            return semicolon < 0 ? string.Empty : value[(semicolon + 1)..];
        }

        throw new InvalidOperationException($"Value '{value}' is not a canonical fact or signature id.");
    }

    private static string PercentDecode(string value)
    {
        var bytes = new List<byte>(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '%' && i + 2 < value.Length)
            {
                bytes.Add(byte.Parse(value.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                i += 2;
            }
            else
            {
                bytes.Add((byte)value[i]);
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
