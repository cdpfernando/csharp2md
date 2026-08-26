using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class RetryCanonicalBytesTests
{
    [Fact]
    [Trait("Requirement", "ROSE-53")]
    public async Task AnalyzeAsync_SameSolutionTwice_ProducesByteIdenticalCanonicalPayloads()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var first = await Publish(solutionPath);
        var second = await Publish(solutionPath);

        Assert.Equal(PublicationStatus.Committed, first.Outcome.Status);
        Assert.Equal(PublicationStatus.Committed, second.Outcome.Status);
        Assert.Equal(first.Publication.SolutionKey, second.Publication.SolutionKey);

        var firstPayloads = CanonicalPayloads(first.Publication);
        var secondPayloads = CanonicalPayloads(second.Publication);
        Assert.NotEmpty(firstPayloads);
        Assert.Contains(firstPayloads, fragment => fragment.CanonicalKey == "diagnostics.json");
        Assert.Equal(firstPayloads.Length, secondPayloads.Length);
        for (var index = 0; index < firstPayloads.Length; index++)
        {
            Assert.Equal(firstPayloads[index].CanonicalKey, secondPayloads[index].CanonicalKey);
            Assert.Equal(ArtifactRole.Payload, firstPayloads[index].Role);
            Assert.True(
                firstPayloads[index].Payload.AsSpan().SequenceEqual(secondPayloads[index].Payload.AsSpan()),
                $"Canonical payload bytes at '{firstPayloads[index].CanonicalKey}' differ across retries.");
        }
    }

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> Publish(string solutionPath)
    {
        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        var outcome = Assert.Single(result.Solutions);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return (outcome, publication);
    }

    private static ImmutableArray<StagedFragment> CanonicalPayloads(CommittedPublication publication) =>
        [.. publication.ArtifactsInPublicationOrder
            .Where(fragment => fragment.Role == ArtifactRole.Payload)
            .Where(fragment => fragment.CanonicalKey != "measurements.json")];
}
