using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class TopologyIsolationTests
{
    [Fact]
    [Trait("Requirement", "CDC-10")]
    [Trait("Requirement", "CDC-13")]
    public async Task AnalyzeAsync_AcmePayments_GroupsSharedContractsIntoPaymentsWithoutASeparateComponent()
    {
        var (paymentsOutcome, paymentsPublication) = await AnalyzeAsync("Acme.Payments", "Acme.Payments.slnx");
        Assert.Equal(PublicationStatus.Committed, paymentsOutcome.Status);

        var architecture = ReadShard<ArchitectureFactsShard>(paymentsPublication, "facts/architecture.json");
        var component = Assert.Single(architecture.Components);
        Assert.Equal("Acme.Payments/Acme.Payments.csproj", component.Name);
        Assert.Equal("Acme.Payments/Acme.Payments.csproj", Assert.Single(architecture.DeploymentUnits).Name);
        Assert.DoesNotContain(
            architecture.Components,
            candidate => candidate.Name == "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");

        var structural = ReadShard<StructuralFactsShard>(paymentsPublication, "facts/structural.json");
        var contractsProject = Assert.Single(
            structural.Projects,
            project => Uri.UnescapeDataString(project.ProjectId)
                .Contains("Acme.Shared.Contracts/Acme.Shared.Contracts.csproj", StringComparison.Ordinal));
        var contractsSymbolIds = structural.Symbols
            .Where(symbol => string.Equals(symbol.OwningProject, contractsProject.ProjectId, StringComparison.Ordinal))
            .Select(symbol => symbol.Identity.Id)
            .ToHashSet(StringComparer.Ordinal);
        var belongs = ReadOptionalRelations(paymentsPublication, "relations/confirmed/belongs-to.json");
        Assert.Contains(
            belongs,
            relation => relation.Kind == "belongs-to"
                && relation.Target.Id == component.Identity.Id
                && contractsSymbolIds.Contains(relation.Source.Id));

        var (ordersOutcome, ordersPublication) = await AnalyzeAsync("Acme.Orders", "Acme.Orders.slnx");
        Assert.Equal(PublicationStatus.Committed, ordersOutcome.Status);

        var ordersArchitecture = ReadShard<ArchitectureFactsShard>(ordersPublication, "facts/architecture.json");
        Assert.Equal(3, ordersArchitecture.Components.Length);
        Assert.Contains(
            ordersArchitecture.Components,
            candidate => candidate.Name == "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");
        Assert.Equal(2, ordersArchitecture.DeploymentUnits.Length);

        var shared = Assert.Single(
            ordersArchitecture.Components,
            candidate => candidate.Name == "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");
        var included = ReadOptionalRelations(ordersPublication, "relations/confirmed/included-in.json");
        var sharedInclusions = included
            .Where(relation => relation.Kind == "included-in" && relation.Source.Id == shared.Identity.Id)
            .ToArray();
        Assert.Equal(2, sharedInclusions.Length);
        Assert.Equal(
            ordersArchitecture.DeploymentUnits.Select(unit => unit.Identity.Id).OrderBy(id => id, StringComparer.Ordinal),
            sharedInclusions.Select(relation => relation.Target.Id).OrderBy(id => id, StringComparer.Ordinal));
    }

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> AnalyzeAsync(
        string folder,
        string file)
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            folder,
            file);
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return (outcome, publication);
    }

    private static T ReadShard<T>(CommittedPublication publication, string canonicalKey)
    {
        var fragment = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == canonicalKey);
        return CanonicalJson.Read<T>(fragment.Payload.AsSpan());
    }

    private static ImmutableArray<ConfirmedRelationDto> ReadOptionalRelations(
        CommittedPublication publication,
        string canonicalKey)
    {
        var fragment = publication.ArtifactsInPublicationOrder
            .SingleOrDefault(artifact => artifact.CanonicalKey == canonicalKey);
        return fragment is null
            ? []
            : CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(fragment.Payload.AsSpan());
    }
}
