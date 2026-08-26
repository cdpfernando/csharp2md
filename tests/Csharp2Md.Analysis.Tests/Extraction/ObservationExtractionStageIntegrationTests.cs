using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class ObservationExtractionStageIntegrationTests
{
    private const string FixtureSecret =
        "Server=orders-db.internal.acme.local;Database=Orders;User Id=orders_app;Password=appsettings-fixture-secret;";

    [Fact]
    [Trait("Requirement", "CDC-01")]
    [Trait("Requirement", "CDC-26")]
    [Trait("Requirement", "CDC-32")]
    public async Task ExecuteAsync_AcmeOrders_EmitsProjectMetadataAndConfigurationObservations()
    {
        var context = await AnalyzeAsync(AcmeOrdersSolutionPath());
        try
        {
            var snapshot = context.Accumulator.ToSnapshot();
            var orders = ProjectNamed(snapshot, "Acme.Orders/Acme.Orders.csproj");
            var contracts = ProjectNamed(snapshot, "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");
            var broken = ProjectNamed(snapshot, "Acme.Broken/Acme.Broken.csproj");

            var ordersKind = Assert.Single(OutputKind(snapshot, orders));
            Assert.Equal("application", PayloadValue(ordersKind, "output-kind"));
            Assert.Equal(EvidenceMethod.Configured, ordersKind.ExtractionMethod);
            var ordersRef = Assert.Single(ProjectReferences(snapshot, orders));
            Assert.Equal(
                "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj",
                PayloadValue(ordersRef, "project-reference"));

            var contractsKind = Assert.Single(OutputKind(snapshot, contracts));
            Assert.Equal("library", PayloadValue(contractsKind, "output-kind"));
            Assert.Empty(ProjectReferences(snapshot, contracts));
            Assert.Empty(ProjectMetadata(snapshot, broken));

            var payment = Assert.Single(
                ConfigurationKeys(snapshot),
                observation => PayloadValue(observation, "key") == "Services:PaymentService"
                    && observation.Locator.RelativePath == "Acme.Orders/appsettings.json");
            Assert.Equal("literal", PayloadValue(payment, "resolution"));
            Assert.Equal("https://payments.internal.acme.local:8443", PayloadValue(payment, "address"));
            Assert.Equal(EvidenceMethod.Configured, payment.ExtractionMethod);
            Assert.NotEqual(default, payment.DocumentHash);

            var notification = Assert.Single(
                ConfigurationKeys(snapshot),
                observation => PayloadValue(observation, "key") == "Services:NotificationService"
                    && observation.Locator.RelativePath == "Acme.Orders/appsettings.json");
            Assert.Equal("dynamic", PayloadValue(notification, "resolution"));

            var ordersDb = Assert.Single(
                ConfigurationKeys(snapshot),
                observation => PayloadValue(observation, "key") == "ConnectionStrings:OrdersDb");
            Assert.All(
                ordersDb.Identity.Payload.Entries,
                entry => Assert.DoesNotContain(FixtureSecret, entry.Value.Value, StringComparison.Ordinal));
            Assert.Contains(
                snapshot.Facts.OfType<Document>(),
                document => ordersDb.Identity.Owner.Equals(document.Reference)
                    && document.RelativePath == "Acme.Orders/appsettings.json");
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-01")]
    [Trait("Requirement", "CDC-32")]
    public async Task ExecuteAsync_AcmePayments_EmitsApplicationAndLibraryMetadata()
    {
        var context = await AnalyzeAsync(AcmePaymentsSolutionPath());
        try
        {
            var snapshot = context.Accumulator.ToSnapshot();
            var payments = ProjectNamed(snapshot, "Acme.Payments/Acme.Payments.csproj");
            var contracts = ProjectNamed(snapshot, "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");

            Assert.Equal("application", PayloadValue(Assert.Single(OutputKind(snapshot, payments)), "output-kind"));
            Assert.Equal(
                "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj",
                PayloadValue(Assert.Single(ProjectReferences(snapshot, payments)), "project-reference"));
            Assert.Equal("library", PayloadValue(Assert.Single(OutputKind(snapshot, contracts)), "output-kind"));
            Assert.Empty(ProjectReferences(snapshot, contracts));
            Assert.All(
                ProjectMetadata(snapshot),
                observation => Assert.Equal(EvidenceMethod.Configured, observation.ExtractionMethod));
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-01")]
    [Trait("Requirement", "CDC-26")]
    public async Task ExecuteAsync_AcmeOrders_ObservationCountIncludesBothEmitters()
    {
        var context = await BindAsync(AcmeOrdersSolutionPath());
        try
        {
            var before = context.Accumulator.ToSnapshot().Observations.Length;
            var result = await new ObservationExtractionStage().ExecuteAsync(context, CancellationToken.None);
            var snapshot = context.Accumulator.ToSnapshot();

            Assert.Equal(snapshot.Observations.Length, result.ObservationCount);
            Assert.True(result.ObservationCount > before);
            Assert.NotEmpty(ProjectMetadata(snapshot));
            Assert.NotEmpty(ConfigurationKeys(snapshot));
            Assert.True(context.BoundSolution?.IsDisposed);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-01")]
    public async Task ExecuteAsync_NullBoundSolution_EmitsNothing()
    {
        var context = new PipelineContext(new SwallowingSession(), AcmeOrdersSolutionPath());
        await new InventoryStage().ExecuteAsync(context, CancellationToken.None);

        var result = await new ObservationExtractionStage().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, result.ObservationCount);
        Assert.Equal(0, result.RelationCount);
        Assert.Empty(context.Accumulator.ToSnapshot().Observations);
        Assert.Null(context.BoundSolution);
    }

    private static async Task<PipelineContext> AnalyzeAsync(string solutionPath)
    {
        var context = await BindAsync(solutionPath);
        await new ObservationExtractionStage().ExecuteAsync(context, CancellationToken.None);
        return context;
    }

    private static async Task<PipelineContext> BindAsync(string solutionPath)
    {
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
        await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
        return context;
    }

    private static string AcmeOrdersSolutionPath() => FixtureSolution("Acme.Orders", "Acme.Orders.slnx");

    private static string AcmePaymentsSolutionPath() => FixtureSolution("Acme.Payments", "Acme.Payments.slnx");

    private static string FixtureSolution(string folder, string file)
    {
        var path = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution", folder, file);
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    private static Project ProjectNamed(Csharp2Md.Analysis.Storage.FactualSnapshot snapshot, string logicalPath) =>
        Assert.Single(
            snapshot.Facts.OfType<Project>(),
            project => string.Equals(LogicalPath(project), logicalPath, StringComparison.Ordinal));

    private static string LogicalPath(Project project)
    {
        const string marker = ";path=";
        var id = project.Id.Value;
        var start = id.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, id);
        start += marker.Length;
        var end = id.IndexOf(';', start);
        var encoded = end < 0 ? id[start..] : id[start..end];
        return Uri.UnescapeDataString(encoded);
    }

    private static Observation[] ProjectMetadata(
        Csharp2Md.Analysis.Storage.FactualSnapshot snapshot,
        Project? owner = null) =>
        snapshot.Observations
            .Where(observation =>
                observation.Identity.Kind is ObservationKind.Configuration
                && observation.Identity.Payload.Entries.Any(entry => entry.Key is "output-kind" or "project-reference")
                && (owner is null || observation.Identity.Owner.Equals(owner.Reference)))
            .ToArray();

    private static Observation[] OutputKind(
        Csharp2Md.Analysis.Storage.FactualSnapshot snapshot,
        Project owner) =>
        ProjectMetadata(snapshot, owner)
            .Where(observation => observation.Identity.Payload.Entries.Any(entry => entry.Key == "output-kind"))
            .ToArray();

    private static Observation[] ProjectReferences(
        Csharp2Md.Analysis.Storage.FactualSnapshot snapshot,
        Project owner) =>
        ProjectMetadata(snapshot, owner)
            .Where(observation => observation.Identity.Payload.Entries.Any(entry => entry.Key == "project-reference"))
            .ToArray();

    private static Observation[] ConfigurationKeys(Csharp2Md.Analysis.Storage.FactualSnapshot snapshot) =>
        snapshot.Observations
            .Where(observation =>
                observation.Identity.Kind is ObservationKind.Configuration
                && observation.Identity.Payload.Entries.Any(entry => entry.Key == "key"))
            .ToArray();

    private static string PayloadValue(Observation observation, string key) =>
        Assert.Single(observation.Identity.Payload.Entries, entry => entry.Key == key).Value.Value;
}
