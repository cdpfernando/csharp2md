using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Retrieval;

/// <summary>
/// Reads artifact bytes by canonical key from one of the two places a package can be measured from
/// (design.md "Two adapters"): the fragments an `analyze` run just staged, or a package already published
/// to disk (AD-025's `validate` path). <see cref="RetrievalScenarioRunner"/> is written against this
/// interface alone, so the same walk and the same measurements apply to both.
/// </summary>
public interface IArtifactSource
{
    bool TryRead(string artifactKey, out ImmutableArray<byte> bytes);
}

/// <summary>Reads from the staged fragment set an `analyze` run is about to publish (or just published).</summary>
public sealed class StagedFragmentArtifactSource : IArtifactSource
{
    private readonly Dictionary<string, StagedFragment> _fragments;

    public StagedFragmentArtifactSource(ImmutableArray<StagedFragment> fragments)
    {
        _fragments = fragments.ToDictionary(static fragment => fragment.CanonicalKey, StringComparer.Ordinal);
    }

    public bool TryRead(string artifactKey, out ImmutableArray<byte> bytes)
    {
        if (!_fragments.TryGetValue(artifactKey, out var fragment))
        {
            bytes = default;
            return false;
        }

        bytes = fragment.IsDeferred ? fragment.ReadPayload() : fragment.Payload;
        return true;
    }
}

/// <summary>
/// Reads directly from a published package directory on disk. Construction re-hydrates the package
/// through <see cref="FactualPackageReader"/> (AD-025), so an already-corrupt or unpublished package is
/// rejected before a single scenario runs, exactly as `validate` requires it to be; each individual
/// artifact is then read straight from its file, since a published package's files on disk ARE its
/// canonical bytes -- there is no second, separate representation to reconstruct or disagree with them.
/// </summary>
public sealed class PackageDirectoryArtifactSource : IArtifactSource
{
    private readonly string _packageDirectory;

    public PackageDirectoryArtifactSource(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        _ = FactualPackageReader.Read(packageDirectory);
        _packageDirectory = packageDirectory;
    }

    public bool TryRead(string artifactKey, out ImmutableArray<byte> bytes)
    {
        var path = Path.Combine(_packageDirectory, artifactKey.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            bytes = default;
            return false;
        }

        bytes = File.ReadAllBytes(path).ToImmutableArray();
        return true;
    }
}

/// <summary>
/// One documented retrieval path, executed and measured (GCPC-052). <see cref="Exercised"/> is
/// <see langword="false"/> when the guide itself states the scenario has no instance in this package (the
/// edge case in spec.md: "not exercised", never "passed"). <see cref="Reached"/> is only meaningful when
/// <see cref="Exercised"/> is <see langword="true"/>: it is <see langword="false"/> when a named artifact
/// failed to resolve through the source (GCPC-054), in which case <see cref="FailureReason"/> names it.
/// </summary>
public sealed record ScenarioResult(
    string Name,
    bool Exercised,
    bool Reached,
    string? FailureReason,
    int FilesRead,
    int Hops,
    long Bytes,
    double Tokens,
    int RelevantFacts,
    int NoiseRecordsRead)
{
    /// <summary>The declared per-scenario budget (GCPC-053): 25 file reads, 100,000 estimated tokens.</summary>
    public bool WithinBudget =>
        FilesRead <= CeilingCalculator.DefaultMaxFileReadsPerScenario
        && Tokens <= CeilingCalculator.DefaultReadingBudgetTokens;

    public MeasurementRecordDto ToMeasurementRecord() =>
        new(
            "retrieval-scenario:" + Name,
            Timestamp: null,
            DurationMilliseconds: null,
            Exercised,
            Reached,
            FailureReason,
            FilesRead,
            Hops,
            Bytes,
            Tokens,
            RelevantFacts,
            NoiseRecordsRead);
}

public sealed record ScenarioReport(ImmutableArray<ScenarioResult> Scenarios)
{
    /// <summary>Every exercised scenario reached its endpoint; an unexercised scenario never fails the run.</summary>
    public bool Passed => Scenarios.All(static scenario => !scenario.Exercised || scenario.Reached);

    /// <summary>The published measurement records for this report (GCPC-052), one per scenario, destined for `measurements.json`.</summary>
    public ImmutableArray<MeasurementRecordDto> ToMeasurementRecords() =>
        [.. Scenarios.Select(static scenario => scenario.ToMeasurementRecord())];
}

/// <summary>
/// Executes and measures every scenario `retrieval.md` documents (GCPC-052..GCPC-054): a scenario is the
/// set of artifact keys one of the guide's own bullet lines names, in the order the guide states them --
/// the confirmed-relation bullets from "## 3. Follow a confirmed relation", the disposition bullets from
/// "## 4. Follow an unproven disposition", and the catalog bullets from "## 1. Locate an identity" taken
/// together as one scenario. No parsing beyond the guide's own published bullets is required:
/// <c>RetrievalGuideProjector</c> already guarantees every artifact key it names exists in this
/// publication (GCPC-055), so a resolution failure here means <paramref name="source"/> itself disagrees
/// with the guide it was handed, not that the guide is wrong.
/// </summary>
public static class RetrievalScenarioRunner
{
    private const string LocateHeading = "## 1. Locate an identity";
    private const string RelationsHeading = "## 3. Follow a confirmed relation";
    private const string DispositionsHeading = "## 4. Follow an unproven disposition";

    private static readonly Regex RelationBulletPattern = new(
        @"^- `(?<kind>[a-z-]+)` -- (?<body>.*)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex DispositionBulletPattern = new(
        @"^- (?<label>[a-z ]+): (?<body>.*)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex ArtifactKeyPattern = new("`([^`]+)`", RegexOptions.Compiled);

    public static ScenarioReport Run(IArtifactSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.TryRead("retrieval.md", out var guideBytes))
        {
            throw new PublicationRejectedException("retrieval-guide-missing", "retrieval.md");
        }

        var text = Encoding.UTF8.GetString(guideBytes.AsSpan());
        var scenarios = ImmutableArray.CreateBuilder<ScenarioResult>();

        scenarios.Add(ExecuteScenario("locate-an-identity", Keys(Section(text, LocateHeading)), source));

        foreach (Match match in RelationBulletPattern.Matches(Section(text, RelationsHeading)))
        {
            scenarios.Add(ExecuteScenario(
                "relation:" + match.Groups["kind"].Value,
                Keys(match.Groups["body"].Value),
                source));
        }

        foreach (Match match in DispositionBulletPattern.Matches(Section(text, DispositionsHeading)))
        {
            scenarios.Add(ExecuteScenario(
                "disposition:" + match.Groups["label"].Value.Trim(),
                Keys(match.Groups["body"].Value),
                source));
        }

        return new ScenarioReport(scenarios.ToImmutable());
    }

    private static ScenarioResult ExecuteScenario(string name, ImmutableArray<string> path, IArtifactSource source)
    {
        if (path.IsDefaultOrEmpty)
        {
            return new ScenarioResult(name, Exercised: false, Reached: false, FailureReason: null, 0, 0, 0, 0, 0, 0);
        }

        long bytes = 0;
        var relevantFacts = 0;
        foreach (var key in path)
        {
            if (!source.TryRead(key, out var artifactBytes))
            {
                return new ScenarioResult(
                    name,
                    Exercised: true,
                    Reached: false,
                    FailureReason: "artifact '" + key + "' did not resolve",
                    FilesRead: path.Length,
                    Hops: path.Length,
                    Bytes: bytes,
                    Tokens: CeilingCalculator.EstimateTokens(bytes),
                    RelevantFacts: relevantFacts,
                    NoiseRecordsRead: 0);
            }

            bytes += artifactBytes.Length;
            relevantFacts += CountRecords(artifactBytes);
        }

        return new ScenarioResult(
            name,
            Exercised: true,
            Reached: true,
            FailureReason: null,
            FilesRead: path.Length,
            Hops: path.Length,
            Bytes: bytes,
            Tokens: CeilingCalculator.EstimateTokens(bytes),
            RelevantFacts: relevantFacts,
            NoiseRecordsRead: 0);
    }

    private static int CountRecords(ImmutableArray<byte> bytes)
    {
        try
        {
            return JsonNode.Parse(bytes.AsSpan()) is JsonArray array ? array.Count : 1;
        }
        catch (System.Text.Json.JsonException)
        {
            return 0;
        }
    }

    private static string Section(string text, string heading)
    {
        var start = text.IndexOf(heading, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var next = text.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);
        return next < 0 ? text[start..] : text[start..(next + 1)];
    }

    private static ImmutableArray<string> Keys(string text) =>
        [.. ArtifactKeyPattern.Matches(text)
            .Select(static match => match.Groups[1].Value)
            .Where(static candidate => candidate.Contains('/', StringComparison.Ordinal)
                && (candidate.EndsWith(".json", StringComparison.Ordinal)
                    || candidate.EndsWith(".md", StringComparison.Ordinal)))];
}
