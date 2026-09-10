namespace Csharp2Md.Analysis.Tests.Certification;

/// <summary>
/// GCPC-078, GCPC-079: the normative labeled-corpus thresholds from
/// <c>docs/architecture/quality-and-security.md</c>'s "Engine gates" table, applied to a real
/// <see cref="AreaCertificationResult"/>.
/// </summary>
internal enum CertificationVerdict
{
    Pass,
    Fail,
}

internal sealed record ThresholdRequirement(double PrecisionThreshold, double RecallThreshold);

internal sealed record AreaCertificationVerdict(
    CertifiedArea Area,
    CertificationVerdict Verdict,
    double MeasuredPrecision,
    double MeasuredRecall,
    double PrecisionThreshold,
    double RecallThreshold,
    ImmutableArray<LabeledCorpusEntry> FailingItems)
{
    /// <summary>GCPC-079: names the area, the measured value alongside the threshold, and every failing item.</summary>
    public string Explain() =>
        $"{Area}: precision {MeasuredPrecision:P2} (threshold {PrecisionThreshold:P0}), " +
        $"recall {MeasuredRecall:P2} (threshold {RecallThreshold:P0})" +
        (FailingItems.IsEmpty
            ? string.Empty
            : $" -- failing items: {string.Join(", ", FailingItems.Select(static item => item.Id))}");
}

internal static class EngineThresholds
{
    /// <summary>docs/architecture/quality-and-security.md's "Engine gates" table, verbatim.</summary>
    internal static readonly ImmutableDictionary<CertifiedArea, ThresholdRequirement> Normative =
        new Dictionary<CertifiedArea, ThresholdRequirement>
        {
            [CertifiedArea.EntryPoint] = new(0.99, 0.95),
            [CertifiedArea.LinkedCall] = new(0.99, 0.90),
            [CertifiedArea.Contract] = new(0.99, 0.95),
            [CertifiedArea.Persistence] = new(0.99, 0.90),
        }.ToImmutableDictionary();

    public static AreaCertificationVerdict Evaluate(AreaCertificationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var threshold = Normative[result.Area];
        var passes = result.Precision >= threshold.PrecisionThreshold && result.Recall >= threshold.RecallThreshold;
        return new AreaCertificationVerdict(
            result.Area,
            passes ? CertificationVerdict.Pass : CertificationVerdict.Fail,
            result.Precision,
            result.Recall,
            threshold.PrecisionThreshold,
            threshold.RecallThreshold,
            passes ? [] : result.FailingItems);
    }
}

public sealed class EngineThresholdTests
{
    public static IEnumerable<object[]> Areas() =>
        LabeledCorpusReader.AllAreas().Select(area => new object[] { area });

    [Theory]
    [MemberData(nameof(Areas))]
    [Trait("Requirement", "GCPC-078")]
    public async Task Evaluate_EveryCertifiedArea_MeetsItsNormativeThreshold(CertifiedArea area)
    {
        var result = await EngineCertificationRunner.RunAsync(area);
        var verdict = EngineThresholds.Evaluate(result);

        var threshold = EngineThresholds.Normative[area];
        Assert.True(
            verdict.MeasuredPrecision >= threshold.PrecisionThreshold,
            $"{area} precision {verdict.MeasuredPrecision:P2} fell below the {threshold.PrecisionThreshold:P0} threshold.");
        Assert.True(
            verdict.MeasuredRecall >= threshold.RecallThreshold,
            $"{area} recall {verdict.MeasuredRecall:P2} fell below the {threshold.RecallThreshold:P0} threshold.");
        Assert.Equal(CertificationVerdict.Pass, verdict.Verdict);
        Assert.Empty(verdict.FailingItems);
    }

    [Fact]
    [Trait("Requirement", "GCPC-079")]
    public async Task Evaluate_FlippedEntryPointLabel_FailsCertificationAndNamesTheFlippedItem()
    {
        // GCPC-079's own proof requirement: flip one ground-truth label's expected value (never the
        // classifier output the runner observed) and confirm certification reacts -- if it didn't, the
        // measurement would be reading the classifier's own output as if it were ground truth instead
        // of comparing against an independent corpus.
        var real = await EngineCertificationRunner.RunAsync(CertifiedArea.EntryPoint);
        var target = Assert.Single(real.Measurements, m => m.Entry.Id == "entry-getwidget");
        Assert.Equal(ExpectedState.Present, target.Entry.Expected);
        Assert.Equal(ExpectedState.Present, target.Actual);

        var flippedEntry = target.Entry with { Expected = ExpectedState.Absent };
        var flipped = real with
        {
            Measurements = real.Measurements
                .Select(m => m.Entry.Id == "entry-getwidget" ? new LabelMeasurement(flippedEntry, m.Actual) : m)
                .ToImmutableArray(),
        };

        var verdict = EngineThresholds.Evaluate(flipped);

        Assert.Equal(CertificationVerdict.Fail, verdict.Verdict);
        Assert.Contains(verdict.FailingItems, item => item.Id == "entry-getwidget");
        Assert.True(
            verdict.MeasuredPrecision < EngineThresholds.Normative[CertifiedArea.EntryPoint].PrecisionThreshold,
            $"Flipping one of two positives to a false positive should have dropped precision below threshold; measured {verdict.MeasuredPrecision:P2}.");
    }

    [Fact]
    [Trait("Requirement", "GCPC-079")]
    public async Task Explain_AFailingArea_NamesTheAreaTheMeasuredValueTheThresholdAndTheFailingItems()
    {
        var real = await EngineCertificationRunner.RunAsync(CertifiedArea.EntryPoint);
        var flippedEntry = Assert.Single(real.Measurements, m => m.Entry.Id == "entry-getwidget").Entry
            with
        { Expected = ExpectedState.Absent };
        var flipped = real with
        {
            Measurements = real.Measurements
                .Select(m => m.Entry.Id == "entry-getwidget" ? new LabelMeasurement(flippedEntry, m.Actual) : m)
                .ToImmutableArray(),
        };

        var verdict = EngineThresholds.Evaluate(flipped);
        var message = verdict.Explain();

        Assert.Contains("EntryPoint", message, StringComparison.Ordinal);
        Assert.Contains(verdict.MeasuredPrecision.ToString("P2"), message, StringComparison.Ordinal);
        Assert.Contains(EngineThresholds.Normative[CertifiedArea.EntryPoint].PrecisionThreshold.ToString("P0"), message, StringComparison.Ordinal);
        Assert.Contains("entry-getwidget", message, StringComparison.Ordinal);
    }
}
