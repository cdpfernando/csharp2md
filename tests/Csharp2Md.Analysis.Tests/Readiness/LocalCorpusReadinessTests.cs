using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Analysis.Tests.Readiness;

/// <summary>
/// GCPC-118, GCPC-119: the same <see cref="LlmReadinessChecklist"/> T64 proved against the
/// certification corpus runs against a real eShop or eShopOnContainers clone when its expected solution
/// file exists, and reports a named skip -- never a silently assumed pass or a build failure -- when it
/// does not. <c>Category=LocalCorpus</c> (the standing convention this project already uses for
/// <c>Csharp2Md.Cli.Tests.LocalCorpusAnalyzeTests</c>) keeps every test in this class excluded from
/// every mandatory gate (<c>--filter "Category!=LocalCorpus"</c>); no mandatory gate depends on the
/// clone existing.
/// </summary>
public sealed class LocalCorpusReadinessTests
{
    public static TheoryData<string, string> LocalCorpora { get; } = new()
    {
        { "eShop", Path.Combine("fixtures", "eShop", "eShop.slnx") },
        { "eShopOnContainers", Path.Combine("fixtures", "eShopOnContainers", "eShopOnContainers-ServicesAndWebApps.sln") },
    };

    [Theory]
    [MemberData(nameof(LocalCorpora))]
    [Trait("Category", "LocalCorpus")]
    public async Task Evaluate_LocalCorpus_ReportsReadinessWithCitedEvidenceWhenCloneIsPresent(string name, string relativeSolution)
    {
        var solutionPath = Path.Combine(AnalysisTestPaths.RepoRoot, relativeSolution);
        if (!File.Exists(solutionPath))
        {
            // The same named-skip convention LocalCorpusAnalyzeTests already uses: the missing path is
            // named in the skip reason, never silently assumed to mean "passed" or "not applicable".
            throw new InvalidOperationException(
                string.Concat("$XunitDynamicSkip$", $"local {name} clone is not present at '{solutionPath}'."));
        }

        var tree = Directory.CreateTempSubdirectory("csharp2md-local-corpus-readiness-");
        try
        {
            // The projector's own ceiling must be passed explicitly -- see ScaleInputGeneratorTests
            // (T63) for why PackageProjector()'s parameterless constructor is the wrong default here.
            var store = new FilesystemTransactionalStore(
                tree.FullName, new PackageProjector(CeilingCalculator.Derive().CeilingBytes));
            var engine = new AnalysisEngine(store);
            var result = await engine.AnalyzeAsync(
                AnalysisRequest.Create([solutionPath]),
                CancellationToken.None);

            var outcome = Assert.Single(result.Solutions);
            Assert.Equal(PublicationStatus.Committed, outcome.Status);
            var packageDirectory = Directory.GetDirectories(tree.FullName).Single();

            var verdicts = LlmReadinessChecklist.Evaluate(packageDirectory);

            // GCPC-119: "report the result" -- every one of the six criteria is evaluated and carries
            // cited evidence, the same shape T64 asserted for the certification corpus. This is a real
            // eShop clone, not the versioned corpus, so a PASS verdict is reported, not required: D-01
            // makes the certification corpus the mandatory gate precisely because an eShop-only gate is
            // unreproducible in CI, and this optional re-run's job is to report, not to certify.
            Assert.Equal(6, verdicts.Length);
            foreach (var verdict in verdicts)
            {
                Assert.False(string.IsNullOrWhiteSpace(verdict.Evidence), $"{verdict.Criterion} cited no evidence.");
            }

            Assert.Contains(verdicts, static v => v.Criterion == ReadinessCriterion.OverallReadiness);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }
}
