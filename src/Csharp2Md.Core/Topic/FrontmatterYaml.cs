using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Csharp2Md.Core.Topic;

/// <summary>
/// Renders the <c>---</c>-delimited frontmatter block and validates it by round-tripping the
/// content through YamlDotNet's deserializer plus a required-field check (WIKI-05, WIKI-07,
/// WIKI-12). <c>ConfigIndexer</c> is the precedent for how this project consumes YamlDotNet.
/// </summary>
internal static class FrontmatterYaml
{
    private const string Delimiter = "---";

    // Keys whose value must be a non-empty string.
    private static readonly string[] RequiredNonEmptyStringFields =
    [
        "title", "source_kind", "source_path", "domain", "topic",
        "language", "file_type", "created_by", "analysis_status",
    ];

    // Keys that must be present, but whose value legitimately may be null/empty (Phase 1 stubs and
    // an empty tags array are both valid, not failures).
    private static readonly string[] RequiredPresentFields = ["tags", "source_service"];

    // WithQuotingNecessaryStrings() + WithNewLine("\n"): both verified present in YamlDotNet
    // 18.1.0's shipped XML docs (design.md Tech Decisions). WithNewLine("\n") is what keeps output
    // byte-identical across platforms — CRLF on Windows would otherwise break that.
    private static readonly ISerializer ValueSerializer = new SerializerBuilder()
        .WithQuotingNecessaryStrings()
        .WithNewLine("\n")
        .Build();

    private static readonly IDeserializer ContentDeserializer = new DeserializerBuilder().Build();

    /// <summary>
    /// Renders the block, keys in the schema's declared order. <c>tags</c> is hand-formatted as a
    /// flow-style array rather than routed through <see cref="ValueSerializer"/>: every tag comes
    /// from <see cref="TagDeriver"/>'s fixed, safe kebab-case vocabulary, so no escaping decision is
    /// needed there. <c>source_service</c> is always the literal YAML <c>null</c> in Phase 1.
    /// </summary>
    public static string Render(Frontmatter frontmatter)
    {
        ArgumentNullException.ThrowIfNull(frontmatter);

        var lines = new List<string>
        {
            Delimiter,
            Field("title", frontmatter.Title),
            Field("source_kind", ToKebabCase(frontmatter.SourceKind.ToString())),
            Field("source_path", frontmatter.SourcePath),
            Field("domain", frontmatter.Domain),
            Field("topic", frontmatter.Topic),
            Field("language", frontmatter.Language),
            Field("file_type", ToKebabCase(frontmatter.FileType.ToString())),
            $"tags: [{string.Join(", ", frontmatter.Tags)}]",
            Field("created_by", frontmatter.CreatedBy),
            "source_service: null",
            Field("analysis_status", frontmatter.AnalysisStatus),
            Delimiter,
        };

        return string.Join("\n", lines) + "\n";
    }

    /// <summary>
    /// Round-trips <paramref name="yaml"/> through the deserializer and checks required fields.
    /// Never throws: a parse failure becomes a <see cref="FrontmatterFailure"/> naming
    /// <paramref name="sourcePath"/> and the specific error, matching WIKI-12's "report, don't crash"
    /// contract. <c>null</c> means the block is valid.
    /// </summary>
    public static FrontmatterFailure? Validate(string yaml, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentNullException.ThrowIfNull(sourcePath);

        var content = StripDelimiters(yaml);

        Dictionary<string, object>? parsed;
        try
        {
            parsed = ContentDeserializer.Deserialize<Dictionary<string, object>>(content);
        }
        catch (YamlException ex)
        {
            return new FrontmatterFailure(sourcePath, $"Frontmatter does not parse as valid YAML: {ex.Message}");
        }

        if (parsed is null)
        {
            return new FrontmatterFailure(sourcePath, "Frontmatter block is empty.");
        }

        foreach (var key in RequiredNonEmptyStringFields)
        {
            if (!parsed.TryGetValue(key, out var value) || value is not string text || text.Length == 0)
            {
                return new FrontmatterFailure(sourcePath, $"Required field '{key}' is missing or empty.");
            }
        }

        foreach (var key in RequiredPresentFields)
        {
            if (!parsed.ContainsKey(key))
            {
                return new FrontmatterFailure(sourcePath, $"Required field '{key}' is missing.");
            }
        }

        return null;
    }

    private static string Field(string key, string value) => $"{key}: {ValueSerializer.Serialize(value).TrimEnd('\n')}";

    private static string ToKebabCase(string pascalCase) =>
        string.Concat(pascalCase.Select((c, i) => i > 0 && char.IsUpper(c) ? $"-{c}" : c.ToString())).ToLowerInvariant();

    // The leading and trailing "---" lines are the block's own delimiters, not part of the YAML
    // document — a YAML stream with two "---" markers and nothing between the second and end-of-
    // stream is itself a syntax error ("Expected StreamEnd, got DocumentStart"), confirmed against
    // the actual deserializer rather than assumed. Frontmatter-processing tools universally strip
    // delimiters before parsing for the same reason; this mirrors that convention.
    private static string StripDelimiters(string yaml)
    {
        var lines = new List<string>(yaml.Replace("\r\n", "\n").Split('\n'));

        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count > 0 && lines[0].Trim() == Delimiter)
        {
            lines.RemoveAt(0);
        }

        if (lines.Count > 0 && lines[^1].Trim() == Delimiter)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join("\n", lines);
    }
}

/// <summary>
/// One document's frontmatter validation failure (WIKI-12). Public: surfaced through
/// <c>PipelineRunResult</c> to the CLI layer for reporting.
/// </summary>
public sealed record FrontmatterFailure(string SourcePath, string Error);
