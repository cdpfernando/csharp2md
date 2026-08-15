using Csharp2Md.Core.Configuration;
using Csharp2Md.Core.Discovery;

namespace Csharp2Md.Core.Tests.Configuration;

public sealed class ConfigIndexerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-config-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string CreateServiceRoot(string name)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static ServiceCatalog CatalogFor(params string[] roots) => new(
        roots.Select(r => new ServiceDescriptor(
            new ServiceName(Path.GetFileName(r)), r, ServiceBoundaryKind.LooseProjects, null, [], []))
            .ToList());

    [Fact]
    public void Index_AppSettingsWithServicesSection_IndexesLogicalNames()
    {
        var dir = CreateServiceRoot("Orders");
        File.WriteAllText(
            Path.Combine(dir, "appsettings.json"),
            """{ "Services": { "PaymentService": "https://payments.local:8443" } }""");

        var result = ConfigIndexer.Index(CatalogFor(dir));

        Assert.Equal("https://payments.local:8443", result.Index.LogicalNames["PaymentService"]);
    }

    [Fact]
    public void Index_MultipleAppSettingsVariants_MergesAllEntries()
    {
        var dir = CreateServiceRoot("Orders");
        File.WriteAllText(
            Path.Combine(dir, "appsettings.json"),
            """{ "Services": { "PaymentService": "https://payments.local:8443" } }""");
        File.WriteAllText(
            Path.Combine(dir, "appsettings.Development.json"),
            """{ "Services": { "NotificationService": "${NOTIFICATION_URL}" } }""");

        var result = ConfigIndexer.Index(CatalogFor(dir));

        Assert.Equal(2, result.Index.LogicalNames.Count);
        Assert.Equal("https://payments.local:8443", result.Index.LogicalNames["PaymentService"]);
        Assert.Equal("${NOTIFICATION_URL}", result.Index.LogicalNames["NotificationService"]);
    }

    [Fact]
    public void Index_DockerComposeFile_IndexesServiceNames()
    {
        var dir = CreateServiceRoot("Orders");
        File.WriteAllText(
            Path.Combine(dir, "docker-compose.yml"),
            """
            services:
              payment-service:
                image: acme/payments:latest
              orders-service:
                image: acme/orders:latest
            """);

        var result = ConfigIndexer.Index(CatalogFor(dir));

        Assert.Contains("payment-service", result.Index.DockerComposeServiceNames);
        Assert.Contains("orders-service", result.Index.DockerComposeServiceNames);
    }

    [Fact]
    public void Index_MalformedAppSettingsJson_WarnsAndContinuesWithoutThrowing()
    {
        var dir = CreateServiceRoot("Orders");
        File.WriteAllText(Path.Combine(dir, "appsettings.json"), "{ not valid json");

        var result = ConfigIndexer.Index(CatalogFor(dir));

        Assert.Empty(result.Index.LogicalNames);
        Assert.Single(result.Warnings);
        Assert.Contains("appsettings.json", result.Warnings[0]);
    }

    [Fact]
    public void Index_MalformedDockerCompose_WarnsAndContinuesWithoutThrowing()
    {
        var dir = CreateServiceRoot("Orders");
        File.WriteAllText(Path.Combine(dir, "docker-compose.yml"), "services: [this is not: valid: yaml structure");

        var result = ConfigIndexer.Index(CatalogFor(dir));

        Assert.Empty(result.Index.DockerComposeServiceNames);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Index_FilesUnderBinOrObj_AreExcluded()
    {
        var dir = CreateServiceRoot("Orders");
        var binDir = Path.Combine(dir, "bin", "Debug");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(
            Path.Combine(binDir, "appsettings.json"),
            """{ "Services": { "ShouldNotAppear": "https://ignored.local" } }""");

        var result = ConfigIndexer.Index(CatalogFor(dir));

        Assert.Empty(result.Index.LogicalNames);
    }

    [Fact]
    public void Index_MultipleServiceRootsInCatalog_CombinesEntriesAcrossRoots()
    {
        var ordersDir = CreateServiceRoot("Orders");
        var paymentsDir = CreateServiceRoot("Payments");
        File.WriteAllText(
            Path.Combine(ordersDir, "appsettings.json"),
            """{ "Services": { "PaymentService": "https://payments.local:8443" } }""");
        File.WriteAllText(
            Path.Combine(paymentsDir, "docker-compose.yml"),
            "services:\n  payments:\n    image: acme/payments:latest\n");

        var result = ConfigIndexer.Index(CatalogFor(ordersDir, paymentsDir));

        Assert.Equal("https://payments.local:8443", result.Index.LogicalNames["PaymentService"]);
        Assert.Contains("payments", result.Index.DockerComposeServiceNames);
    }
}
