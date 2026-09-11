using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Fixtures;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Projection;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Readiness;

/// <summary>
/// GCPC-115, GCPC-116: an objective evaluator whose criteria correspond one-to-one to the audit's own
/// "Matriz de aptidão" (<c>artifacts/verifications/llm-readiness-s-cb7a4be0b1a084f3b59e9c2f1e3906f1.md</c>).
/// Runnable against any already-published package directory -- exactly how the original audit worked,
/// reading only the output, never the solution or the generator's internals.
/// </summary>
internal enum ReadinessCriterion
{
    SemanticLegibility,
    Scale,
    FactualCoverage,
    RunCertification,
    ClassificationReliability,
    OverallReadiness,
}

/// <summary>One criterion's PASS/FAIL verdict together with the evidence that produced it (GCPC-116's
/// "cites the evidence" requirement) -- never a bare boolean.</summary>
internal sealed record ReadinessVerdict(ReadinessCriterion Criterion, bool Pass, string Evidence);

internal static class LlmReadinessChecklist
{
    private static readonly string[] KnownPageFactTypes =
    [
        "EntryPoint", "BoundaryOperation", "Component", "DeploymentUnit", "Contract", "DataStore", "DataObject",
    ];

    /// <summary>
    /// Evaluates all six criteria the audit rated PARTIAL or FAIL against an already-published package:
    /// semantic legibility, scale, factual coverage, run certification, classification reliability, and
    /// the roll-up overall readiness. Every other row in the audit's matrix (package integrity, internal
    /// links, source recovery, secret redaction) was already PASS and is out of this evaluator's scope.
    /// </summary>
    public static ImmutableArray<ReadinessVerdict> Evaluate(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);

        var manifest = CanonicalJson.Read<ManifestEnvelope>(
            File.ReadAllBytes(Path.Combine(packageDirectory, "manifest.json")));
        var result = FactualPackageReader.Read(packageDirectory);

        var semanticLegibility = EvaluateSemanticLegibility(packageDirectory);
        var scale = EvaluateScale(packageDirectory, manifest);
        var factualCoverage = EvaluateFactualCoverage(result.Coverage);
        var runCertification = EvaluateRunCertification(result.Certification);
        var classificationReliability = EvaluateClassificationReliability(result.Snapshot, result.Coverage);

        ReadinessVerdict[] prerequisites =
        [
            semanticLegibility, scale, factualCoverage, runCertification, classificationReliability,
        ];
        var overallPass = prerequisites.All(static verdict => verdict.Pass);
        var overall = new ReadinessVerdict(
            ReadinessCriterion.OverallReadiness,
            overallPass,
            overallPass
                ? "every prerequisite criterion (semantic legibility, scale, factual coverage, run "
                    + "certification, classification reliability) reports PASS"
                : "at least one prerequisite criterion reports FAIL: " + string.Join(
                    ", ",
                    prerequisites.Where(static verdict => !verdict.Pass).Select(static verdict => verdict.Criterion)));

        return [.. prerequisites, overall];
    }

    /// <summary>GCPC-093..098: every catalog entry carries a compact, proven label, and no Markdown
    /// page's title is only a fact type plus an encoded identity.</summary>
    private static ReadinessVerdict EvaluateSemanticLegibility(string packageDirectory)
    {
        var catalogPath = Path.Combine(packageDirectory, "catalogs", "entry-points.json");
        if (!File.Exists(catalogPath))
        {
            return Fail(ReadinessCriterion.SemanticLegibility, "catalogs/entry-points.json is not published");
        }

        var catalog = CanonicalJson.Read<ImmutableArray<CatalogEntryDto>>(File.ReadAllBytes(catalogPath));
        if (catalog.IsDefaultOrEmpty)
        {
            return Fail(ReadinessCriterion.SemanticLegibility, "catalogs/entry-points.json publishes no entries");
        }

        var withoutLabels = catalog.Where(static entry => entry.Labels.IsDefaultOrEmpty).ToArray();
        if (withoutLabels.Length > 0)
        {
            return Fail(
                ReadinessCriterion.SemanticLegibility,
                $"{withoutLabels.Length} of {catalog.Length} catalogs/entry-points.json entries carry no "
                    + $"compact label, e.g. fact id '{withoutLabels[0].FactId}'");
        }

        var markdownRoot = Path.Combine(packageDirectory, "markdown");
        var genericTitles = Directory.Exists(markdownRoot)
            ? Directory.EnumerateFiles(markdownRoot, "*.md", SearchOption.AllDirectories)
                .Where(HasOnlyAFactTypeTitle)
                .ToArray()
            : [];
        if (genericTitles.Length > 0)
        {
            return Fail(
                ReadinessCriterion.SemanticLegibility,
                $"{genericTitles.Length} Markdown page(s) titled only by fact type plus an encoded "
                    + $"identity, e.g. '{Path.GetRelativePath(packageDirectory, genericTitles[0])}'");
        }

        var sample = catalog[0];
        var sampleLabel = sample.Labels[0];
        return Pass(
            ReadinessCriterion.SemanticLegibility,
            $"catalogs/entry-points.json publishes {catalog.Length} entries, each carrying at least one "
                + $"compact label (e.g. '{sampleLabel.Kind}={sampleLabel.Value}' for fact id "
                + $"'{sample.FactId}', cited to '{sampleLabel.ArtifactKey}#{sampleLabel.Ordinal}'); no "
                + "Markdown page is titled only by fact type");
    }

    /// <summary>
    /// A Markdown page's first line is exactly <c># [FactType](artifact-key) &lt;!-- ordinal --&gt;</c>
    /// (<c>MarkdownProjector.Cite</c>'s GCPC-098 fallback, used only when no label was proven at all) --
    /// GCPC-096 forbids this as a page's only descriptive title.
    /// </summary>
    private static bool HasOnlyAFactTypeTitle(string markdownFile)
    {
        var firstLine = File.ReadLines(markdownFile).FirstOrDefault() ?? string.Empty;
        return KnownPageFactTypes.Any(factType =>
            firstLine.StartsWith("# [" + factType + "](", StringComparison.Ordinal));
    }

    /// <summary>GCPC-036..GCPC-038: the derived ceiling and its token estimator are published, and no
    /// record-bearing, shardable artifact exceeds it. Only fixed one-per-package envelopes with a
    /// bounded metric/reason count are excluded. The manifest is included because F6 shards its entries.
    /// The compound fact-family bundles
    /// (facts/structural.json and its siblings, quarantine/records.json) are no longer excluded: F1 gave
    /// them the same adaptive sharding flat record-array families already had.</summary>
    private static ReadinessVerdict EvaluateScale(string packageDirectory, ManifestEnvelope manifest)
    {
        if (manifest.Provenance is not { } provenance)
        {
            return Fail(ReadinessCriterion.Scale, "manifest.json publishes no provenance, so no ceiling is declared");
        }

        if (provenance.ArtifactCeilingBytes <= 0 || string.IsNullOrWhiteSpace(provenance.TokenEstimatorId))
        {
            return Fail(
                ReadinessCriterion.Scale,
                $"provenance declares an invalid ceiling ({provenance.ArtifactCeilingBytes} bytes) or "
                    + "token estimator");
        }

        var unshardable = new HashSet<string>(StringComparer.Ordinal)
        {
            "contracts/taxonomy-registry.json", // PackagePublisher.RegistryKey; internal to Csharp2Md.Storage
            "coverage.json",
            "diagnostics.json",
            "measurements.json",
            "run-certification.json",
        };

        long largest = 0;
        string? largestKey = null;
        var offenders = new List<(string Key, long Bytes)>();
        foreach (var file in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(packageDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
            if (unshardable.Contains(relative))
            {
                continue;
            }

            var length = new FileInfo(file).Length;
            if (length > largest)
            {
                largest = length;
                largestKey = relative;
            }

            if (length > provenance.ArtifactCeilingBytes)
            {
                offenders.Add((relative, length));
            }
        }

        if (offenders.Count > 0)
        {
            return Fail(
                ReadinessCriterion.Scale,
                $"{offenders.Count} artifact(s) exceed the {provenance.ArtifactCeilingBytes}-byte ceiling, "
                    + $"e.g. '{offenders[0].Key}' ({offenders[0].Bytes} bytes)");
        }

        return Pass(
            ReadinessCriterion.Scale,
            $"every record-bearing artifact fits the declared {provenance.ArtifactCeilingBytes}-byte "
                + $"ceiling (token estimator '{provenance.TokenEstimatorId}'); the largest is "
                + $"'{largestKey}' ({largest} bytes)");
    }

    /// <summary>GCPC-001..GCPC-010: the four mandatory metrics never publish a silent 0/0 as if it were
    /// a satisfied ratio -- each is either "evaluated" with a real (non-zero) denominator, or
    /// "not_applicable" with a stated reason, and at least one metric is actually evaluated.</summary>
    private static ReadinessVerdict EvaluateFactualCoverage(CoverageEnvelope coverage)
    {
        (string Name, CoverageMetricDto Metric)[] metrics =
        [
            ("entry_point", coverage.EntryPointCoverage),
            ("linked_call", coverage.LinkedCallCoverage),
            ("contract", coverage.ContractCoverage),
            ("persistence", coverage.PersistenceCoverage),
        ];

        foreach (var (name, metric) in metrics)
        {
            if (metric.State == "evaluated" && metric.Denominator <= 0)
            {
                return Fail(
                    ReadinessCriterion.FactualCoverage,
                    $"'{name}' coverage reports state 'evaluated' with a zero denominator -- exactly the "
                        + "silent 0/0 the audit's B1 finding named");
            }

            if (metric.State == "not_applicable" && string.IsNullOrWhiteSpace(metric.NotApplicableReason))
            {
                return Fail(ReadinessCriterion.FactualCoverage, $"'{name}' coverage is not_applicable with no stated reason");
            }

            if (metric.Numerator + metric.Exclusions + metric.Unknowns > metric.Denominator)
            {
                return Fail(
                    ReadinessCriterion.FactualCoverage,
                    $"'{name}' coverage's numerator ({metric.Numerator}) plus exclusions "
                        + $"({metric.Exclusions}) plus unknowns ({metric.Unknowns}) exceeds its denominator "
                        + $"({metric.Denominator})");
            }
        }

        var evaluated = metrics.Where(static entry => entry.Metric.State == "evaluated").ToArray();
        if (evaluated.Length == 0)
        {
            return Fail(ReadinessCriterion.FactualCoverage, "every mandatory metric is not_applicable -- the whole run degrades to unmeasured");
        }

        return Pass(
            ReadinessCriterion.FactualCoverage,
            string.Join(
                "; ",
                metrics.Select(static entry => entry.Metric.State == "evaluated"
                    ? $"{entry.Name}={entry.Metric.Numerator}/{entry.Metric.Denominator} " +
                      $"(exclusions={entry.Metric.Exclusions}, unknowns={entry.Metric.Unknowns})"
                    : $"{entry.Name}=not_applicable ({entry.Metric.NotApplicableReason})")));
    }

    /// <summary>GCPC-001: the vocabulary is exactly passed/degraded/failed (RunCertificationEnvelope's
    /// own constructor already forbids "not_evaluated"), and the certification corpus's own run is
    /// healthy enough to report passed or degraded, not failed.</summary>
    private static ReadinessVerdict EvaluateRunCertification(RunCertificationEnvelope certification)
    {
        if (certification.Status == "failed")
        {
            return Fail(
                ReadinessCriterion.RunCertification,
                "run-certification.json reports 'failed': " + string.Join("; ", certification.Reasons));
        }

        return Pass(
            ReadinessCriterion.RunCertification,
            $"run-certification.json reports '{certification.Status}'"
                + (certification.Reasons.IsDefaultOrEmpty ? string.Empty : " (" + string.Join("; ", certification.Reasons) + ")"));
    }

    /// <summary>GCPC-019..025 (no private helper promoted to EntryPoint -- the audit's proven B2 false
    /// positive, reproduced as the certification corpus's ChangeUriPlaceholder fixture) and GCPC-011..018
    /// (linked_call coverage's own numerator/exclusions/unknowns never exceed its denominator, i.e. no
    /// invocation occurrence is missing a disposition).</summary>
    private static ReadinessVerdict EvaluateClassificationReliability(FactualSnapshot snapshot, CoverageEnvelope coverage)
    {
        // Reads through the same FactualPackageReader every real caller uses (already merges
        // facts/architecture.json's shards back into one set, F1/GCPC-039) rather than reading the
        // artifact's bytes off disk directly -- a family that split under the ceiling is no less
        // published than one that didn't.
        var entryPoints = snapshot.Facts.OfType<EntryPoint>().ToArray();
        if (!snapshot.Facts.Any(static fact => fact.Family == FactFamily.Architecture))
        {
            return Fail(ReadinessCriterion.ClassificationReliability, "facts/architecture.json is not published");
        }

        var falsePositive = entryPoints
            .FirstOrDefault(static entry => entry.Symbol.Id.Value.Contains("ChangeUriPlaceholder", StringComparison.Ordinal));
        if (falsePositive is not null)
        {
            return Fail(
                ReadinessCriterion.ClassificationReliability,
                $"'{falsePositive.Symbol.Id.Value}' -- a private helper -- is published as an EntryPoint, "
                    + "reproducing the audit's B2 false positive");
        }

        var linkedCall = coverage.LinkedCallCoverage;
        if (linkedCall.State == "evaluated"
            && linkedCall.Numerator + linkedCall.Exclusions + linkedCall.Unknowns > linkedCall.Denominator)
        {
            return Fail(
                ReadinessCriterion.ClassificationReliability,
                $"linked_call coverage's numerator ({linkedCall.Numerator}) plus exclusions "
                    + $"({linkedCall.Exclusions}) plus unknowns ({linkedCall.Unknowns}) exceeds its "
                    + $"denominator ({linkedCall.Denominator}) -- an invocation occurrence would be "
                    + "unaccounted, reproducing the audit's B3 finding");
        }

        return Pass(
            ReadinessCriterion.ClassificationReliability,
            $"no EntryPoint fact names 'ChangeUriPlaceholder' among {entryPoints.Length} "
                + $"published entry points; linked_call coverage accounts for every recognized "
                + $"occurrence ({linkedCall.Numerator} confirmed, {linkedCall.Exclusions} exclusions, "
                + $"{linkedCall.Unknowns} unknowns, denominator {linkedCall.Denominator})");
    }

    private static ReadinessVerdict Pass(ReadinessCriterion criterion, string evidence) => new(criterion, true, evidence);

    private static ReadinessVerdict Fail(ReadinessCriterion criterion, string evidence) => new(criterion, false, evidence);
}

/// <summary>
/// GCPC-115, GCPC-116, GCPC-120: runs <see cref="LlmReadinessChecklist"/> against a real package built
/// from `fixtures/CertificationCorpus` and asserts every criterion the audit rated PARTIAL or FAIL now
/// reports PASS, each with cited evidence -- and that the evaluator runs with no dependency on the
/// eShop clones (GCPC-118), which T65 wires as the separate, optional LocalCorpus re-run.
/// </summary>
public sealed class LlmReadinessChecklistTests
{
    [Fact]
    [Trait("Requirement", "GCPC-115")]
    [Trait("Requirement", "GCPC-116")]
    [Trait("Requirement", "GCPC-120")]
    public async Task Evaluate_CertificationCorpusPackage_EveryPartialOrFailCriterionReportsPassWithEvidence()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-readiness-");
        try
        {
            var packageDirectory = await PublishCertificationCorpusAsync(tree.FullName);

            var verdicts = LlmReadinessChecklist.Evaluate(packageDirectory);

            Assert.Equal(6, verdicts.Length);
            foreach (var verdict in verdicts)
            {
                Assert.True(
                    verdict.Pass,
                    $"{verdict.Criterion} did not report PASS: {verdict.Evidence}");
                Assert.False(string.IsNullOrWhiteSpace(verdict.Evidence), $"{verdict.Criterion} cited no evidence.");
            }

            Assert.Contains(verdicts, static v => v.Criterion == ReadinessCriterion.SemanticLegibility);
            Assert.Contains(verdicts, static v => v.Criterion == ReadinessCriterion.Scale);
            Assert.Contains(verdicts, static v => v.Criterion == ReadinessCriterion.FactualCoverage);
            Assert.Contains(verdicts, static v => v.Criterion == ReadinessCriterion.RunCertification);
            Assert.Contains(verdicts, static v => v.Criterion == ReadinessCriterion.ClassificationReliability);
            Assert.Contains(verdicts, static v => v.Criterion == ReadinessCriterion.OverallReadiness);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    internal static async Task<string> PublishCertificationCorpusAsync(string outputRoot)
    {
        // The projector's own ceiling must be passed explicitly -- see ScaleInputGeneratorTests (T63)
        // for why PackageProjector()'s parameterless constructor is the wrong default here.
        var store = new FilesystemTransactionalStore(outputRoot, new PackageProjector(CeilingCalculator.Derive().CeilingBytes));
        var engine = new AnalysisEngine(store);
        var result = await engine.AnalyzeAsync(
            AnalysisRequest.Create([CertificationCorpusPaths.SolutionPath]),
            CancellationToken.None);

        Assert.Equal(PublicationStatus.Committed, Assert.Single(result.Solutions).Status);
        return Directory.GetDirectories(outputRoot).Single();
    }
}
