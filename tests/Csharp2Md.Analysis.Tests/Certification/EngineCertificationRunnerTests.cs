using Csharp2Md.Analysis.Tests.Fixtures;

namespace Csharp2Md.Analysis.Tests.Certification;

/// <summary>
/// GCPC-077, GCPC-080: proves the engine-certification runner computes precision and recall per area
/// from the T53 label files compared against real classifier output, and that no precision or recall
/// value is ever written into a run's own published package (AD-009's separation between engine
/// certification and run certification).
/// </summary>
public sealed class EngineCertificationRunnerTests
{
    [Fact]
    [Trait("Requirement", "GCPC-077")]
    public async Task RunAsync_EntryPointArea_MatchesEveryLabelAgainstRealClassifierOutput()
    {
        var result = await EngineCertificationRunner.RunAsync(CertifiedArea.EntryPoint);

        // Post-T17 fix, every entry-point label is expected to match ground truth exactly: the two
        // reachable Widgets actions are confirmed, the private helper and the unrelated concrete class
        // method are not.
        Assert.All(result.Measurements, m => Assert.True(m.IsExactMatch, $"'{m.Entry.Id}' expected {m.Entry.Expected} but classifier output was {m.Actual}."));
        Assert.Equal(2, result.TruePositives);
        Assert.Equal(0, result.FalsePositives);
        Assert.Equal(0, result.FalseNegatives);
        Assert.Equal(1.0, result.Precision);
        Assert.Equal(1.0, result.Recall);
        Assert.Empty(result.FailingItems);
    }

    [Fact]
    [Trait("Requirement", "GCPC-077")]
    public async Task RunAsync_LinkedCallArea_ResolvesTheLookalikeInterfaceDispatchAsUnresolvedNotConfirmed()
    {
        var result = await EngineCertificationRunner.RunAsync(CertifiedArea.LinkedCall);

        var lookalike = Assert.Single(result.Measurements, m => m.Entry.Id == "call-getorderstatus-iorderqueries");
        Assert.Equal(ExpectedState.Unresolved, lookalike.Actual);
        var positive = Assert.Single(result.Measurements, m => m.Entry.Id == "call-getwidget-changeuriplaceholder");
        Assert.Equal(ExpectedState.Present, positive.Actual);
        var negative = Assert.Single(result.Measurements, m => m.Entry.Id == "call-getorderstatus-ok-notfound");
        Assert.Equal(ExpectedState.Absent, negative.Actual);

        Assert.Equal(1, result.TruePositives);
        Assert.Equal(0, result.FalsePositives);
        Assert.Equal(0, result.FalseNegatives);
        Assert.Equal(1.0, result.Precision);
        Assert.Equal(1.0, result.Recall);
    }

    [Fact]
    [Trait("Requirement", "GCPC-077")]
    public async Task RunAsync_ContractArea_TheUnhandledEventIsUnresolvedAndTheSameNamedTypeIsNeverMerged()
    {
        var result = await EngineCertificationRunner.RunAsync(CertifiedArea.Contract);

        var handled = Assert.Single(result.Measurements, m => m.Entry.Id == "contract-orderplaced");
        Assert.Equal(ExpectedState.Present, handled.Actual);
        var unhandled = Assert.Single(result.Measurements, m => m.Entry.Id == "contract-ordershipped");
        Assert.Equal(ExpectedState.Unresolved, unhandled.Actual);
        var notMerged = Assert.Single(result.Measurements, m => m.Entry.Id == "contract-receipt-not-merged");
        Assert.Equal(ExpectedState.Absent, notMerged.Actual);

        Assert.Equal(1, result.TruePositives);
        Assert.Equal(0, result.FalsePositives);
        Assert.Equal(0, result.FalseNegatives);
        Assert.Equal(1.0, result.Precision);
        Assert.Equal(1.0, result.Recall);
    }

    [Fact]
    [Trait("Requirement", "GCPC-077")]
    public async Task RunAsync_PersistenceArea_TheRuntimeTableNameStaysUnresolvedAndTheConnectionGetterIsAbsent()
    {
        var result = await EngineCertificationRunner.RunAsync(CertifiedArea.Persistence);

        var interpolated = Assert.Single(result.Measurements, m => m.Entry.Id == "persist-selectallfrom");
        Assert.Equal(ExpectedState.Unresolved, interpolated.Actual);
        var connectionGetter = Assert.Single(result.Measurements, m => m.Entry.Id == "persist-connection");
        Assert.Equal(ExpectedState.Absent, connectionGetter.Actual);
        Assert.All(
            result.Measurements.Where(m => m.Entry.Kind == LabelKind.Positive),
            m => Assert.Equal(ExpectedState.Present, m.Actual));

        Assert.Equal(5, result.TruePositives);
        Assert.Equal(0, result.FalsePositives);
        Assert.Equal(0, result.FalseNegatives);
        Assert.Equal(1.0, result.Precision);
        Assert.Equal(1.0, result.Recall);
    }

    [Fact]
    [Trait("Requirement", "GCPC-080")]
    public async Task AnalyzeAsync_CertificationCorpus_PublishesNoPrecisionOrRecallValueInAnyArtifact()
    {
        var publication = await EngineCertificationRunner.AnalyzeAsync(
            CertificationCorpusPaths.SolutionPath,
            CancellationToken.None);

        foreach (var artifact in publication.ArtifactsInPublicationOrder)
        {
            var text = System.Text.Encoding.UTF8.GetString(artifact.Payload.AsSpan());
            Assert.DoesNotContain("precision", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("recall", text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
