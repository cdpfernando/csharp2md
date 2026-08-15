using Csharp2Md.Core.Configuration;
using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Tests.Detection;

public sealed class GrpcClientDetectorTests
{
    /// <summary>
    /// Stands in for the protoc-generated client and the <c>Grpc.Core</c> call types. The detector
    /// recognises them by namespace-qualified type name, so declaring them in the compilation
    /// exercises the real matching rule without taking a dependency on Grpc.Core here.
    /// </summary>
    private static IReadOnlyList<DependencySignal> Detect(string body, ConfigIndex? index = null, bool withSemantics = true)
    {
        var source = $$"""
            namespace Grpc.Core
            {
                public class ClientBase { }
                public class ClientBase<T> : ClientBase { }
                public class AsyncUnaryCall<T> { }
                public class AsyncServerStreamingCall<T> { }
                public class AsyncClientStreamingCall<TRequest, TResponse> { }
                public class AsyncDuplexStreamingCall<TRequest, TResponse> { }
            }

            namespace Acme.Contracts
            {
                public class ChargeRequest { }

                public class PaymentReply { }

                public class Payments
                {
                    public class PaymentsClient : global::Grpc.Core.ClientBase<PaymentsClient>
                    {
                        public PaymentReply Charge(ChargeRequest request) => null!;

                        public global::Grpc.Core.AsyncUnaryCall<PaymentReply> ChargeAsync(ChargeRequest request) => null!;

                        public global::Grpc.Core.AsyncServerStreamingCall<PaymentReply> Watch(ChargeRequest request) => null!;

                        public global::Grpc.Core.AsyncDuplexStreamingCall<ChargeRequest, PaymentReply> Exchange() => null!;

                        public PaymentsClient WithHost(string host) => this;
                    }
                }
            }

            namespace Acme.Orders
            {
                using Acme.Contracts;

                public class Gateway
                {
                    private readonly Payments.PaymentsClient _payments = null!;
                    private readonly ChargeRequest _request = null!;

                    public void Run()
                    {
            {{body}}
                    }
                }
            }

            """;

        return new GrpcClientDetector()
            .Detect(DetectionSource.DocumentContext(source, index, withSemantics))
            .ToList();
    }

    // P2-02: unary => request/response, the caller blocks.
    [Fact]
    public void Detect_UnaryAsyncCall_IsClassifiedAsBlockingSynchronous()
    {
        var signals = Detect("            var call = _payments.ChargeAsync(_request);");

        var signal = Assert.Single(signals);
        Assert.Equal(CommunicationType.SincronoBloqueante, signal.Communication);
        Assert.Equal(DependencyKind.Grpc, signal.Kind);
    }

    [Fact]
    public void Detect_BlockingUnaryOverloadReturningTheResponseDirectly_IsClassifiedAsBlockingSynchronous()
    {
        var signals = Detect("            var reply = _payments.Charge(_request);");

        Assert.Equal(CommunicationType.SincronoBloqueante, Assert.Single(signals).Communication);
    }

    // P2-02: duplex / streaming => bidirectional streaming.
    [Fact]
    public void Detect_DuplexStreamingCall_IsClassifiedAsBidirectionalStreaming()
    {
        var signals = Detect("            var call = _payments.Exchange();");

        Assert.Equal(CommunicationType.StreamingBidirecional, Assert.Single(signals).Communication);
    }

    [Fact]
    public void Detect_ServerStreamingCall_IsClassifiedAsBidirectionalStreaming()
    {
        var signals = Detect("            var call = _payments.Watch(_request);");

        Assert.Equal(CommunicationType.StreamingBidirecional, Assert.Single(signals).Communication);
    }

    [Fact]
    public void Detect_TargetName_IsTheProtoServiceBehindTheGeneratedClient()
    {
        var signals = Detect("            var call = _payments.ChargeAsync(_request);");

        Assert.Equal("Payments", Assert.Single(signals).RawTarget);
    }

    [Fact]
    public void Detect_TargetResolvableInConfig_IsMarkedHardCoded()
    {
        var signals = Detect(
            "            var call = _payments.ChargeAsync(_request);",
            DetectionTestContext.ConfigIndexWith(("Payments", "https://payments.internal:5001")));

        Assert.Equal(ResolutionKind.HardCoded, Assert.Single(signals).Resolution);
    }

    // P2-09: an unresolvable target is still recorded, with the raw name preserved.
    [Fact]
    public void Detect_TargetAbsentFromConfig_StillProducesASignalMarkedUnresolved()
    {
        var signals = Detect("            var call = _payments.ChargeAsync(_request);");

        var signal = Assert.Single(signals);
        Assert.Equal(ResolutionKind.Unresolved, signal.Resolution);
        Assert.Equal("Payments", signal.RawTarget);
    }

    // Same recorded decision as T15: catalog correlation belongs to the graph builder.
    [Fact]
    public void Detect_EmittedSignal_LeavesTargetServiceUncorrelated()
    {
        var signals = Detect("            var call = _payments.ChargeAsync(_request);");

        var signal = Assert.Single(signals);
        Assert.Null(signal.TargetService);
        Assert.Equal("Orders/Gateway.cs", signal.Location.FilePath);
    }

    [Fact]
    public void Detect_WithNullSemanticModel_ReportsNothingRatherThanGuessing()
    {
        Assert.Empty(Detect("            var call = _payments.ChargeAsync(_request);", withSemantics: false));
    }

    [Fact]
    public void Detect_ClientConfigurationCallThatIssuesNoRpc_ProducesNoSignal()
    {
        Assert.Empty(Detect("            var configured = _payments.WithHost(\"localhost\");"));
    }

    [Fact]
    public void Detect_CallOnSomethingThatIsNotAGrpcClient_ProducesNoSignal()
    {
        Assert.Empty(Detect("            var text = _request.ToString();"));
    }
}
