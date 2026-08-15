using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Tests.Detection;

/// <summary>One test per row of the design's communication-type classification table.</summary>
public sealed class CommunicationClassifierTests
{
    [Fact]
    public void Classify_HttpCallWhoseResultIsConsumed_IsBlockingSynchronous()
    {
        Assert.Equal(
            CommunicationType.SincronoBloqueante,
            CommunicationClassifier.Classify(DependencyKind.Http, CallShape.ResultConsumed));
    }

    [Fact]
    public void Classify_HttpCallWhoseResultIsDiscarded_IsFireAndForget()
    {
        Assert.Equal(
            CommunicationType.AssincronoFireAndForget,
            CommunicationClassifier.Classify(DependencyKind.Http, CallShape.ResultDiscarded));
    }

    [Fact]
    public void Classify_UnaryGrpcCall_IsBlockingSynchronous()
    {
        Assert.Equal(
            CommunicationType.SincronoBloqueante,
            CommunicationClassifier.Classify(DependencyKind.Grpc, CallShape.Unary));
    }

    [Fact]
    public void Classify_StreamingGrpcCall_IsBidirectionalStreaming()
    {
        Assert.Equal(
            CommunicationType.StreamingBidirecional,
            CommunicationClassifier.Classify(DependencyKind.Grpc, CallShape.Streaming));
    }

    [Fact]
    public void Classify_MessagingSignal_IsPubSubEventRegardlessOfShape()
    {
        Assert.Equal(
            CommunicationType.PubSubEvento,
            CommunicationClassifier.Classify(DependencyKind.Messaging, CallShape.NotApplicable));
    }

    [Fact]
    public void Classify_DirectReference_IsCompileTimeCoupling()
    {
        Assert.Equal(
            CommunicationType.DirectReference,
            CommunicationClassifier.Classify(DependencyKind.DirectReference, CallShape.NotApplicable));
    }

    // A pair no table row covers must fail loudly rather than default to a plausible-looking type.
    [Theory]
    [InlineData(DependencyKind.Http, CallShape.Streaming)]
    [InlineData(DependencyKind.Http, CallShape.NotApplicable)]
    [InlineData(DependencyKind.Grpc, CallShape.ResultDiscarded)]
    public void Classify_PairWithNoTableRow_Throws(DependencyKind kind, CallShape shape)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CommunicationClassifier.Classify(kind, shape));
    }
}
