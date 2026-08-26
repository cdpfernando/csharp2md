using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class TopologyIntegrationTests
{
    [Fact]
    [Trait("Requirement", "CDC-09")]
    [Trait("Requirement", "CDC-11")]
    [Trait("Requirement", "CDC-19")]
    [Trait("Requirement", "CDC-20")]
    [Trait("Requirement", "CDC-21")]
    [Trait("Requirement", "CDC-35")]
    [Trait("Requirement", "CDC-36")]
    [Trait("Requirement", "CDC-37")]
    [Trait("Requirement", "CDC-38")]
    [Trait("Requirement", "CDC-39")]
    [Trait("Requirement", "CDC-43")]
    [Trait("Requirement", "CDC-44")]
    [Trait("Requirement", "CDC-45")]
    public async Task AnalyzeAsync_AcmeOrders_PublishesTheSpecifiedTopologyAndConfiguration()
    {
        var (outcome, publication) = await AnalyzeAcmeOrdersAsync();
        Assert.Equal(PublicationStatus.Committed, outcome.Status);

        var architecture = ReadShard<ArchitectureFactsShard>(publication, "facts/architecture.json");
        Assert.Equal(3, architecture.Components.Length);
        Assert.Contains(architecture.Components, component => component.Name == "Acme.Orders/Acme.Orders.csproj");
        Assert.Contains(architecture.Components, component => component.Name == "Acme.Orders.Worker/Acme.Orders.Worker.csproj");
        Assert.Contains(architecture.Components, component => component.Name == "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");
        Assert.Equal(2, architecture.DeploymentUnits.Length);

        var included = ReadOptionalRelations(publication, "relations/confirmed/included-in.json");
        var shared = Assert.Single(
            architecture.Components,
            component => component.Name == "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");
        var sharedInclusions = included
            .Where(relation => relation.Kind == "included-in" && relation.Source.Id == shared.Identity.Id)
            .ToArray();
        Assert.Equal(2, sharedInclusions.Length);
        Assert.Equal(
            architecture.DeploymentUnits.Select(unit => unit.Identity.Id).OrderBy(id => id, StringComparer.Ordinal),
            sharedInclusions.Select(relation => relation.Target.Id).OrderBy(id => id, StringComparer.Ordinal));

        var configuration = ReadShard<ConfigurationFactsShard>(publication, "facts/configuration.json");
        var ordersComponent = Assert.Single(
            architecture.Components,
            component => component.Name == "Acme.Orders/Acme.Orders.csproj");
        Assert.Contains(
            configuration.ConfigurationBindings,
            binding => binding.BoundFact.Id == ordersComponent.Identity.Id
                && binding.ConfigurationKey.Value == "ConnectionStrings:OrdersDb"
                && binding.ConfigurationKey.Role == "ConfigurationKey");
        Assert.Contains(
            configuration.ConfigurationBindings,
            binding => binding.BoundFact.Id == ordersComponent.Identity.Id
                && binding.ConfigurationKey.Value == "Services:PaymentService"
                && binding.ConfigurationKey.Role == "ConfigurationKey");

        var configuredBy = ReadOptionalRelations(publication, "relations/confirmed/configured-by.json");
        var ordersDbBinding = Assert.Single(
            configuration.ConfigurationBindings,
            binding => binding.BoundFact.Id == ordersComponent.Identity.Id
                && binding.ConfigurationKey.Value == "ConnectionStrings:OrdersDb");
        var paymentBinding = Assert.Single(
            configuration.ConfigurationBindings,
            binding => binding.BoundFact.Id == ordersComponent.Identity.Id
                && binding.ConfigurationKey.Value == "Services:PaymentService");

        Assert.Contains(
            configuredBy,
            relation => relation.Kind == "configured-by"
                && relation.Source.Id == ordersComponent.Identity.Id
                && relation.Target.Id == paymentBinding.Identity.Id
                && relation.EvidenceMethod == "Configured");
        Assert.Contains(
            configuredBy,
            relation => relation.Kind == "configured-by"
                && relation.Source.Id.Contains("ConfigureHost", StringComparison.Ordinal)
                && relation.Source.FactType == "Symbol"
                && relation.Target.Id == ordersDbBinding.Identity.Id
                && relation.EvidenceMethod == "Configured");

        var persistence = ReadShard<PersistenceFactsShard>(publication, "facts/persistence.json");
        var store = Assert.Single(persistence.DataStores, candidate => candidate.Name.Value == "OrdersDb");
        Assert.Contains(
            configuredBy,
            relation => relation.Kind == "configured-by"
                && relation.Source.Id == store.Identity.Id
                && relation.Target.Id == ordersDbBinding.Identity.Id);

        var paymentOperation = Assert.Single(
            architecture.BoundaryOperations,
            operation => operation.DestinationScope == "PaymentService"
                && operation.Direction == "outbound"
                && operation.Protocol == "http");
        Assert.Contains(
            configuredBy,
            relation => relation.Kind == "configured-by"
                && relation.Source.Id == paymentOperation.Identity.Id
                && relation.Target.Id == paymentBinding.Identity.Id);

        var notificationOperation = Assert.Single(
            architecture.BoundaryOperations,
            operation => operation.DestinationScope == "NotificationService"
                && operation.Direction == "outbound");
        var shippingOperation = Assert.Single(
            architecture.BoundaryOperations,
            operation => operation.DestinationScope == "ShippingService"
                && operation.Direction == "outbound");

        var confirmedTargets = ReadOptionalRelations(publication, "relations/confirmed/targets.json");
        var candidates = ReadOptionalArray<CandidateLinkDto>(publication, "relations/candidates.json");
        var frontiers = ReadOptionalArray<OpenFrontierDto>(publication, "relations/frontiers.json");

        Assert.Contains(
            confirmedTargets,
            relation => relation.Kind == "targets" && relation.Source.Id == paymentOperation.Identity.Id);
        Assert.DoesNotContain(
            candidates,
            link => link.Kind == "targets" && link.Source.Id == paymentOperation.Identity.Id);

        var notificationCandidate = Assert.Single(
            candidates,
            link => link.Kind == "targets" && link.Source.Id == notificationOperation.Identity.Id);
        Assert.DoesNotContain(
            confirmedTargets,
            relation => relation.Kind == "targets" && relation.Source.Id == notificationOperation.Identity.Id);
        Assert.Contains(
            frontiers,
            frontier => frontier.Cause == "FurtherContinuationObserved"
                && notificationCandidate.DerivedFrom.Any(identity =>
                    identity.Owner.Id == frontier.Occurrence.Owner.Id
                    && identity.OccurrenceOrdinal == frontier.Occurrence.OccurrenceOrdinal));

        Assert.Contains(
            confirmedTargets,
            relation => relation.Kind == "targets" && relation.Source.Id == shippingOperation.Identity.Id);
        Assert.DoesNotContain(
            candidates,
            link => link.Kind == "targets" && link.Source.Id == shippingOperation.Identity.Id);

        var diagnostics = CanonicalJson.Read<DiagnosticsEnvelope>(
            Assert.Single(
                publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == "diagnostics.json").Payload.AsSpan());
        Assert.DoesNotContain(
            diagnostics.Records,
            record => record.Code == "unsupported-document"
                && record.IdentityOrKey is not null
                && record.IdentityOrKey.Contains("appsettings.json", StringComparison.Ordinal));
    }

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> AnalyzeAcmeOrdersAsync()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
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
        string canonicalKey) =>
        ReadOptionalArray<ConfirmedRelationDto>(publication, canonicalKey);

    private static ImmutableArray<T> ReadOptionalArray<T>(CommittedPublication publication, string canonicalKey)
    {
        var fragment = publication.ArtifactsInPublicationOrder
            .SingleOrDefault(artifact => artifact.CanonicalKey == canonicalKey);
        return fragment is null
            ? []
            : CanonicalJson.Read<ImmutableArray<T>>(fragment.Payload.AsSpan());
    }
}
