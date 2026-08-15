using Csharp2Md.Core;
using Csharp2Md.Core.Configuration;

namespace Csharp2Md.Core.Tests.Configuration;

public sealed class ServiceNameResolverTests
{
    private static ConfigIndex EmptyIndex => new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void Resolve_LiteralAddressInAppSettings_ReturnsHardCoded()
    {
        var index = EmptyIndex with
        {
            LogicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PaymentService"] = "https://payments.internal.acme.local:8443",
            },
        };

        var result = ServiceNameResolver.Resolve("PaymentService", index);

        Assert.Equal(ResolutionKind.HardCoded, result.Kind);
    }

    [Fact]
    public void Resolve_DollarBraceEnvVarReference_ReturnsDynamic()
    {
        var index = EmptyIndex with
        {
            LogicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["NotificationService"] = "${NOTIFICATION_SERVICE_URL}",
            },
        };

        var result = ServiceNameResolver.Resolve("NotificationService", index);

        Assert.Equal(ResolutionKind.Dynamic, result.Kind);
    }

    [Fact]
    public void Resolve_PercentEnvVarReference_ReturnsDynamic()
    {
        var index = EmptyIndex with
        {
            LogicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyService"] = "%LEGACY_SERVICE_URL%",
            },
        };

        var result = ServiceNameResolver.Resolve("LegacyService", index);

        Assert.Equal(ResolutionKind.Dynamic, result.Kind);
    }

    [Fact]
    public void Resolve_DockerComposeServiceName_ReturnsDynamic()
    {
        var index = EmptyIndex with
        {
            DockerComposeServiceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "payment-service" },
        };

        var result = ServiceNameResolver.Resolve("payment-service", index);

        Assert.Equal(ResolutionKind.Dynamic, result.Kind);
    }

    [Fact]
    public void Resolve_NoMatchAnywhere_ReturnsUnresolvedWithRawNamePreserved()
    {
        var result = ServiceNameResolver.Resolve("ShippingService", EmptyIndex);

        Assert.Equal(ResolutionKind.Unresolved, result.Kind);
        Assert.Equal("ShippingService", result.LogicalName);
    }

    [Fact]
    public void Resolve_CaseInsensitiveNameLookup_StillResolves()
    {
        var index = EmptyIndex with
        {
            LogicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PaymentService"] = "https://payments.local",
            },
        };

        var result = ServiceNameResolver.Resolve("paymentservice", index);

        Assert.Equal(ResolutionKind.HardCoded, result.Kind);
    }

    [Fact]
    public void Resolve_AppSettingsEntryTakesPrecedenceOverDockerCompose()
    {
        var index = new ConfigIndex(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Shared"] = "https://shared.local" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Shared" });

        var result = ServiceNameResolver.Resolve("Shared", index);

        Assert.Equal(ResolutionKind.HardCoded, result.Kind);
    }
}
