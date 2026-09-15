using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Projection;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Analysis.Tests.Fixtures;

public sealed class PublicationResilienceSignatureTests
{
    private static readonly ManifestContext RoundTripContext = new("s-test", "PublicationResilience.slnx");

    [Fact]
    [Trait("Requirement", "APR-37")]
    public async Task AnalyzeAsync_NestedSignatures_RoundTripWithExactCanonicalIdentity()
    {
        var solutionPath = PublicationResiliencePaths.SolutionPath;
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");
        Assert.True(
            File.Exists(Path.Combine(PublicationResiliencePaths.RootPath, "Publication.Lib", "NestedSignatures.cs")),
            "Expected the versioned nested-signature source.");

        var store = new InMemoryTransactionalStore(new PackageProjector(CeilingCalculator.Derive().CeilingBytes));
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        Assert.Equal(PublicationStatus.Committed, Assert.Single(result.Solutions).Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));

        var snapshot = ReadSnapshot(publication);
        var named = AssertSymbol(snapshot, "named-tuple", static parameters =>
            parameters.Contains("(string Name, int Age)", StringComparison.Ordinal)
            && !parameters.Contains("[,,]", StringComparison.Ordinal)
            && !parameters.Contains("Dictionary<", StringComparison.Ordinal));
        var nested = AssertSymbol(snapshot, "nested-generic", static parameters =>
            parameters.Contains(
                "global::System.Collections.Generic.Dictionary<(string, int), int>",
                StringComparison.Ordinal));
        var array = AssertSymbol(snapshot, "multidimensional-array", static parameters =>
            parameters.Contains("int[,,]", StringComparison.Ordinal)
            && !parameters.Contains("Name", StringComparison.Ordinal)
            && !parameters.Contains("Dictionary<", StringComparison.Ordinal));
        var mixed = AssertSymbol(snapshot, "mixed", static parameters =>
            parameters.Contains("(string Name, int Age)", StringComparison.Ordinal)
            && parameters.Contains("int[,,]", StringComparison.Ordinal)
            && parameters.Contains("(bool Ok, byte[] Buffer)", StringComparison.Ordinal));

        AssertRoundTrip(named);
        AssertRoundTrip(nested);
        AssertRoundTrip(array);
        AssertRoundTrip(mixed);
    }

    private static Symbol AssertSymbol(FactualSnapshot snapshot, string shape, Func<string, bool> parametersMatch)
    {
        var match = Assert.Single(
            snapshot.Facts.OfType<Symbol>(),
            symbol => parametersMatch(symbol.Signature.Component("parameters") ?? string.Empty));
        Assert.False(string.IsNullOrWhiteSpace(shape));
        Assert.False(string.IsNullOrWhiteSpace(match.Signature.Value));
        return match;
    }

    private static void AssertRoundTrip(Symbol published)
    {
        var restored = DomainMapper.FromWire(
            DomainMapper.ToWire(new FactualSnapshot([published], [], [], [], [], []), RoundTripContext));
        var restoredSymbol = Assert.Single(restored.Facts.OfType<Symbol>());
        Assert.Equal(published, restoredSymbol);
        Assert.Equal(published.Signature, restoredSymbol.Signature);
        Assert.Equal(published.Signature.Value, restoredSymbol.Signature.Value);
    }

    private static FactualSnapshot ReadSnapshot(CommittedPublication publication)
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-publication-resilience-sigs-");
        try
        {
            var packageDirectory = Path.Combine(tree.FullName, "package");
            foreach (var artifact in publication.ArtifactsInPublicationOrder)
            {
                var path = Path.Combine(
                    packageDirectory,
                    artifact.CanonicalKey.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, [.. artifact.Payload]);
            }

            return FactualPackageReader.Read(packageDirectory).Snapshot;
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }
}
