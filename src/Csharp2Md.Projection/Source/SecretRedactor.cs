using System.Text;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Source;

internal static class SecretRedactor
{
    internal const string MarkerText = "[REDACTED-SECRET]";

    internal static readonly ImmutableArray<byte> Marker = [.. Encoding.ASCII.GetBytes(MarkerText)];

    internal static ImmutableArray<byte> Redact(ImmutableArray<byte> original, ImmutableArray<SourceSpanDto> spans)
    {
        if (original.IsDefault)
        {
            original = [];
        }

        if (spans.IsDefaultOrEmpty)
        {
            return original;
        }

        UTF8Encoding utf8;
        string text;
        try
        {
            utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            text = utf8.GetString(original.AsSpan());
        }
        catch (DecoderFallbackException)
        {
            return original;
        }

        var ranges = Merge(spans
            .Select(span => ToByteRange(text, utf8, span))
            .Where(static range => range.End > range.Start)
            .OrderBy(static range => range.Start));

        if (ranges.Count == 0)
        {
            return original;
        }

        var published = new List<byte>(original.Length);
        var cursor = 0;
        foreach (var range in ranges)
        {
            if (range.Start > cursor)
            {
                published.AddRange(original.Skip(cursor).Take(range.Start - cursor));
            }

            published.AddRange(Marker);
            cursor = range.End;
        }

        if (cursor < original.Length)
        {
            published.AddRange(original.Skip(cursor));
        }

        return [.. published];
    }

    internal static ImmutableArray<SourceSpanDto> SpansFor(string documentId, DiagnosticsEnvelope diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var spans = ImmutableArray.CreateBuilder<SourceSpanDto>();
        foreach (var record in diagnostics.Records)
        {
            if (!string.Equals(record.Code, "suspected-secret", StringComparison.Ordinal)
                || !string.Equals(record.IdentityOrKey, documentId, StringComparison.Ordinal)
                || !TryParseSpan(record.Message, out var span))
            {
                continue;
            }

            spans.Add(span);
        }

        return spans.ToImmutable();
    }

    private static bool TryParseSpan(string message, out SourceSpanDto span)
    {
        span = default!;
        var separator = message.IndexOf(' ');
        var coordinates = separator < 0 ? message : message[..separator];
        var dash = coordinates.IndexOf('-');
        if (dash < 0)
        {
            return false;
        }

        if (!TryParsePosition(coordinates[..dash], out var startLine, out var startColumn)
            || !TryParsePosition(coordinates[(dash + 1)..], out var endLine, out var endColumn))
        {
            return false;
        }

        span = new SourceSpanDto(startLine, startColumn, endLine, endColumn);
        return true;
    }

    private static bool TryParsePosition(string text, out int line, out int column)
    {
        line = 0;
        column = 0;
        var colon = text.IndexOf(':');
        return colon > 0
            && int.TryParse(text[..colon], out line)
            && int.TryParse(text[(colon + 1)..], out column)
            && line > 0
            && column > 0;
    }

    private static List<(int Start, int End)> Merge(IEnumerable<(int Start, int End)> ranges)
    {
        var merged = new List<(int Start, int End)>();
        foreach (var range in ranges)
        {
            if (merged.Count == 0 || range.Start > merged[^1].End)
            {
                merged.Add(range);
                continue;
            }

            merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, range.End));
        }

        return merged;
    }

    private static (int Start, int End) ToByteRange(string text, Encoding utf8, SourceSpanDto span)
    {
        var start = CharIndex(text, span.StartLine, span.StartColumn);
        var endInclusive = CharIndex(text, span.EndLine, span.EndColumn);
        if (start < 0 || endInclusive < 0 || endInclusive < start)
        {
            return (0, 0);
        }

        var endExclusive = endInclusive >= text.Length ? text.Length : endInclusive + 1;
        return (utf8.GetByteCount(text.AsSpan(0, start)), utf8.GetByteCount(text.AsSpan(0, endExclusive)));
    }

    private static int CharIndex(string text, int line, int column)
    {
        var currentLine = 1;
        var currentColumn = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (currentLine == line && currentColumn == column)
            {
                return i;
            }

            if (text[i] == '\n')
            {
                currentLine++;
                currentColumn = 1;
            }
            else if (text[i] != '\r')
            {
                currentColumn++;
            }
        }

        return currentLine == line && currentColumn == column ? text.Length : -1;
    }
}
