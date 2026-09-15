using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

public sealed class PublicationResilienceBuilderTests
{
    [Fact]
    [Trait("Requirement", "APR-36")]
    public async Task AnalyzeAsync_BuilderConstructor_PublishesDocumentScopedStructuralContains()
    {
        var solutionPath = PublicationResiliencePaths.SolutionPath;
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");
        Assert.True(
            File.Exists(Path.Combine(PublicationResiliencePaths.RootPath, "Publication.Lib", "BuilderConstructor.cs")),
            "Expected the versioned builder constructor source.");

        var store = new InMemoryTransactionalStore(new PackageProjector(CeilingCalculator.Derive().CeilingBytes));
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        Assert.Equal(PublicationStatus.Committed, Assert.Single(result.Solutions).Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));

        var structural = ShardedFactsReader.Read<StructuralFactsShard>(
            publication.ArtifactsInPublicationOrder, "facts/structural.json");
        var constructor = Assert.Single(
            structural.Symbols,
            symbol => symbol.CanonicalSymbolSignature.Contains("metadata=.ctor", StringComparison.Ordinal)
                && symbol.CanonicalSymbolSignature.Contains("OrderBuilder", StringComparison.Ordinal));

        var observations = ReadObservations(publication);
        var owned = observations
            .Where(observation => observation.Identity.Owner.Id == constructor.Identity.Id)
            .ToArray();
        Assert.NotEmpty(owned);
        Assert.All(
            owned,
            observation => Assert.True(
                observation.Identity.Kind is "invocation" or "data-access",
                observation.Identity.Kind));

        var documentRelative = owned[0].Locator.RelativePath;
        Assert.Contains("BuilderConstructor.cs", documentRelative, StringComparison.Ordinal);
        Assert.Contains(
            observations,
            observation => string.Equals(observation.Locator.RelativePath, documentRelative, StringComparison.OrdinalIgnoreCase)
                && observation.Identity.Kind is not "invocation" and not "data-access");

        var relation = Assert.Single(
            ReadRelations(publication, "relations/confirmed/contains.json"),
            candidate => candidate.Target.Id == constructor.Identity.Id);
        Assert.Equal("Document", relation.Source.FactType);
        Assert.Contains(structural.Documents, document => document.Identity.Id == relation.Source.Id);
        Assert.NotEmpty(relation.DerivedFrom);
        Assert.All(
            relation.DerivedFrom,
            identity => Assert.False(
                identity.Kind is "invocation" or "data-access",
                identity.Kind));
    }

    private static ObservationDto[] ReadObservations(CommittedPublication publication) =>
        publication.ArtifactsInPublicationOrder
            .Where(static artifact => artifact.CanonicalKey.StartsWith("observations/", StringComparison.Ordinal))
            .SelectMany(static artifact => CanonicalJson.Read<ImmutableArray<ObservationDto>>(artifact.Payload.AsSpan()))
            .ToArray();

    private static ImmutableArray<ConfirmedRelationDto> ReadRelations(
        CommittedPublication publication,
        string canonicalKey)
    {
        var stem = canonicalKey.EndsWith(".json", StringComparison.Ordinal)
            ? canonicalKey[..^".json".Length]
            : canonicalKey;
        var records = ImmutableArray.CreateBuilder<ConfirmedRelationDto>();
        foreach (var fragment in publication.ArtifactsInPublicationOrder
            .Where(artifact => artifact.CanonicalKey == canonicalKey
                || (artifact.CanonicalKey.StartsWith(stem + ".", StringComparison.Ordinal)
                    && artifact.CanonicalKey.EndsWith(".json", StringComparison.Ordinal)))
            .OrderBy(static artifact => artifact.CanonicalKey, StringComparer.Ordinal))
        {
            records.AddRange(CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(fragment.Payload.AsSpan()));
        }

        return records.ToImmutable();
    }
}
