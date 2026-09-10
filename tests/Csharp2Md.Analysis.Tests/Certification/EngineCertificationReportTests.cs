using Csharp2Md.Analysis.Tests.Fixtures;

namespace Csharp2Md.Analysis.Tests.Certification;

/// <summary>
/// GCPC-081: proves the committed <c>artifacts/verifications/engine-certification.md</c> is not stale
/// -- it is regenerated from a real, freshly measured run and compared byte-for-byte against what is
/// checked into the repository.
/// </summary>
public sealed class EngineCertificationReportTests
{
    [Fact]
    [Trait("Requirement", "GCPC-081")]
    public async Task Generate_FromAFreshMeasurement_MatchesTheCommittedReportExactly()
    {
        var entries = ImmutableArray.CreateBuilder<(AreaCertificationResult Result, AreaCertificationVerdict Verdict)>();
        foreach (var area in LabeledCorpusReader.AllAreas())
        {
            var result = await EngineCertificationRunner.RunAsync(area);
            entries.Add((result, EngineThresholds.Evaluate(result)));
        }

        var generated = EngineCertificationReportGenerator.Generate(entries.ToImmutable());

        var reportPath = Path.Combine(AnalysisTestPaths.RepoRoot, "artifacts", "verifications", "engine-certification.md");
        Assert.True(File.Exists(reportPath), $"Expected the versioned report at '{reportPath}'.");
        var committed = File.ReadAllText(reportPath);

        Assert.Equal(committed, generated);
    }
}
