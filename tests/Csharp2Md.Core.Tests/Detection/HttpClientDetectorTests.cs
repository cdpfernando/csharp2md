using Csharp2Md.Core;
using Csharp2Md.Core.Configuration;
using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Tests.Detection;

public sealed class HttpClientDetectorTests
{
    private static IReadOnlyList<DependencySignal> Detect(
        string body,
        ConfigIndex? index = null,
        bool withSemantics = false)
    {
        var source = $$"""
            using System;
            using System.Net.Http;
            using System.Threading.Tasks;

            namespace Acme.Orders;

            public interface IClientFactory { HttpClient CreateClient(string name); }

            public sealed class Gateway
            {
                private readonly IClientFactory _factory = null!;
                private readonly HttpClient _http = null!;

                public async Task Run()
                {
            {{body}}
                }
            }

            """;

        var context = DetectionSource.DocumentContext(source, index, withSemantics);
        return new HttpClientDetector().Detect(context).ToList();
    }

    // P2-01: awaited / result consumed => the caller blocks.
    [Fact]
    public void Detect_AwaitedTypedHttpClientCall_IsClassifiedAsBlockingSynchronous()
    {
        var signals = Detect("            await _http.GetAsync(\"https://payments.local/api\");", withSemantics: true);

        var signal = Assert.Single(signals);
        Assert.Equal(CommunicationType.SincronoBloqueante, signal.Communication);
        Assert.Equal(DependencyKind.Http, signal.Kind);
        Assert.Equal("https://payments.local/api", signal.RawTarget);
    }

    // P2-01: not awaited / result discarded => fire and forget.
    [Fact]
    public void Detect_UnawaitedTypedHttpClientCall_IsClassifiedAsFireAndForget()
    {
        var signals = Detect("            _http.GetAsync(\"https://payments.local/api\");", withSemantics: true);

        Assert.Equal(CommunicationType.AssincronoFireAndForget, Assert.Single(signals).Communication);
    }

    [Fact]
    public void Detect_DiscardAssignedHttpCall_IsClassifiedAsFireAndForget()
    {
        var signals = Detect("            _ = _http.GetAsync(\"https://payments.local/api\");", withSemantics: true);

        Assert.Equal(CommunicationType.AssincronoFireAndForget, Assert.Single(signals).Communication);
    }

    [Fact]
    public void Detect_BlockingOnResult_IsClassifiedAsBlockingSynchronousDespiteNotBeingAwaited()
    {
        var signals = Detect("            var response = _http.GetAsync(\"https://payments.local/api\").Result;", withSemantics: true);

        Assert.Equal(CommunicationType.SincronoBloqueante, Assert.Single(signals).Communication);
    }

    [Fact]
    public void Detect_AwaitedThroughConfigureAwait_IsClassifiedAsBlockingSynchronous()
    {
        var signals = Detect(
            "            await _http.GetAsync(\"https://payments.local/api\").ConfigureAwait(false);",
            withSemantics: true);

        Assert.Equal(CommunicationType.SincronoBloqueante, Assert.Single(signals).Communication);
    }

    // P2-07: the logical name resolves to a literal address in config.
    [Fact]
    public void Detect_NamedClientResolvingToALiteralAddress_IsMarkedHardCoded()
    {
        var signals = Detect(
            "            var client = _factory.CreateClient(\"PaymentService\");",
            DetectionTestContext.ConfigIndexWith(("PaymentService", "https://payments.internal:8443")));

        var signal = Assert.Single(signals);
        Assert.Equal("PaymentService", signal.RawTarget);
        Assert.Equal(ResolutionKind.HardCoded, signal.Resolution);
    }

    // P2-08: the logical name resolves through an environment variable.
    [Fact]
    public void Detect_NamedClientResolvingThroughAnEnvironmentVariable_IsMarkedDynamic()
    {
        var signals = Detect(
            "            var client = _factory.CreateClient(\"NotificationService\");",
            DetectionTestContext.ConfigIndexWith(("NotificationService", "${NOTIFICATION_URL}")));

        Assert.Equal(ResolutionKind.Dynamic, Assert.Single(signals).Resolution);
    }

    // P2-09: an unresolvable name still produces a signal, with the raw name preserved.
    [Fact]
    public void Detect_NamedClientAbsentFromConfig_StillProducesASignalMarkedUnresolved()
    {
        var signals = Detect("            var client = _factory.CreateClient(\"ShippingService\");");

        var signal = Assert.Single(signals);
        Assert.Equal(ResolutionKind.Unresolved, signal.Resolution);
        Assert.Equal("ShippingService", signal.RawTarget);
    }

    // Recorded decision: catalog correlation belongs to the graph builder, not to a detector.
    [Fact]
    public void Detect_EmittedSignal_LeavesTargetServiceUncorrelated()
    {
        var signals = Detect(
            "            var client = _factory.CreateClient(\"PaymentService\");",
            DetectionTestContext.ConfigIndexWith(("PaymentService", "https://payments.internal:8443")));

        Assert.Null(Assert.Single(signals).TargetService);
    }

    [Fact]
    public void Detect_RecordsTheSourceServiceAndTheCallSiteLocation()
    {
        var signals = Detect("            var client = _factory.CreateClient(\"ShippingService\");");

        var signal = Assert.Single(signals);
        Assert.Equal(new ServiceName("Acme.Orders"), signal.SourceService);
        Assert.Equal("Orders/Gateway.cs", signal.Location.FilePath);
        Assert.Equal(16, signal.Location.Line);
    }

    // Detectors degrade with the renderer: the factory path is syntactic and keeps working.
    [Fact]
    public void Detect_WithNullSemanticModel_StillDetectsNamedClients()
    {
        var signals = Detect(
            "            var client = _factory.CreateClient(\"PaymentService\");",
            DetectionTestContext.ConfigIndexWith(("PaymentService", "https://payments.internal:8443")));

        var signal = Assert.Single(signals);
        Assert.Equal("PaymentService", signal.RawTarget);
        Assert.Equal(ResolutionKind.HardCoded, signal.Resolution);
    }

    // Without a model the typed-client path cannot prove the receiver is an HttpClient, so it
    // reports nothing rather than guessing from an identifier name.
    [Fact]
    public void Detect_WithNullSemanticModel_ReportsNoTypedClientCalls()
    {
        Assert.Empty(Detect("            await _http.GetAsync(\"https://payments.local/api\");"));
    }

    [Fact]
    public void Detect_CallChainedDirectlyOffCreateClient_ProducesOneSignalNotTwo()
    {
        var signals = Detect(
            "            await _factory.CreateClient(\"PaymentService\").GetAsync(\"/orders\");",
            withSemantics: true);

        Assert.Equal("PaymentService", Assert.Single(signals).RawTarget);
    }

    [Fact]
    public void Detect_DocumentWithNoHttpUsage_ProducesNoSignals()
    {
        Assert.Empty(Detect("            Console.WriteLine(\"nothing to see\");", withSemantics: true));
    }
}
