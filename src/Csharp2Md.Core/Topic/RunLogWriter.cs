using System.Globalization;
using System.Text;

namespace Csharp2Md.Core.Topic;

/// <summary>
/// The data <c>raw/log.md</c> (WIKI-18..22) reports. Deliberately carries no timestamp field —
/// <see cref="RunLogWriter.Write"/> takes a <see cref="TimeProvider"/> instead of
/// <c>DateTimeOffset.UtcNow</c> directly, which is the seam T20's determinism test needs.
/// </summary>
public sealed record RunLogData(
    string Invocation,
    int DocumentCount,
    int EdgeCount,
    int ServiceCount,
    IReadOnlyList<FrontmatterFailure> Failures,
    string OutputTopicPath);

/// <summary>Writes <c>raw/log.md</c>, the auditable record of one run (WIKI-18..22).</summary>
public static class RunLogWriter
{
    public const string FileName = "log.md";

    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";

    /// <summary>
    /// Writes the log unconditionally — never skipped, never made contingent on
    /// <paramref name="data"/>.Failures being empty, so a run that exits <c>1</c> because of
    /// frontmatter validation failures still leaves an auditable record (WIKI-22).
    /// </summary>
    public static string Write(string outputRoot, RunLogData data, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(outputRoot);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var rawRoot = TopicLayout.RawRoot(outputRoot);
        Directory.CreateDirectory(rawRoot);
        var path = Path.Combine(rawRoot, FileName);

        File.WriteAllText(path, Render(data, timeProvider.GetUtcNow()));

        return path;
    }

    private static string Render(RunLogData data, DateTimeOffset timestampUtc)
    {
        var builder = new StringBuilder();

        builder.Append("# Run Log\n\n");
        builder.Append("- Timestamp (UTC): ").Append(timestampUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("- Invocation: `").Append(data.Invocation).Append("`\n");
        builder.Append("- Documents generated: ").Append(data.DocumentCount).Append('\n');
        builder.Append("- Dependency edges: ").Append(data.EdgeCount).Append('\n');
        builder.Append("- Services analyzed: ").Append(data.ServiceCount).Append('\n');
        builder.Append("- Frontmatter validation failures: ").Append(data.Failures.Count).Append('\n');
        builder.Append("- Output topic path: ").Append(data.OutputTopicPath).Append("\n\n");

        // WIKI-21: only present when there is something to report — an empty run has nothing to list.
        if (data.Failures.Count > 0)
        {
            builder.Append("## Validation Failures\n\n");
            foreach (var failure in data.Failures)
            {
                builder.Append("- `").Append(failure.SourcePath).Append("`: ").Append(failure.Error).Append('\n');
            }

            builder.Append('\n');
        }

        // WIKI-20: reserved, empty in Phase 1 — Phase 2 fills these in.
        builder.Append("## Graph Resolution\n\n");
        builder.Append("## Calibration Notes\n");

        return builder.ToString();
    }
}
