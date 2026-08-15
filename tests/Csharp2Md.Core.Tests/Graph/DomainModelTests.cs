using Csharp2Md.Core;
using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Tests.Graph;

public sealed class DomainModelTests
{
    [Fact]
    public void ServiceName_NullValue_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceName(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ServiceName_EmptyOrWhitespaceValue_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => new ServiceName(value));
    }

    [Fact]
    public void ServiceName_SameValue_AreEqual()
    {
        var left = new ServiceName("Acme.Payments");
        var right = new ServiceName("Acme.Payments");

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void ServiceName_DifferentValue_AreNotEqual()
    {
        Assert.NotEqual(new ServiceName("Acme.Payments"), new ServiceName("Acme.Orders"));
    }

    // P2-12 (amended): exactly five communication types, direct-reference included.
    [Fact]
    public void CommunicationType_EnumeratesExactlyTheFiveSpecifiedValues()
    {
        var values = Enum.GetNames<CommunicationType>();

        Assert.Equal(
            new[]
            {
                nameof(CommunicationType.AssincronoFireAndForget),
                nameof(CommunicationType.DirectReference),
                nameof(CommunicationType.PubSubEvento),
                nameof(CommunicationType.SincronoBloqueante),
                nameof(CommunicationType.StreamingBidirecional),
            },
            values.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void DependencySignal_MessagingHalfEdge_CarriesRoleAndTopicWithNoTargetService()
    {
        var signal = new DependencySignal(
            SourceService: new ServiceName("Acme.Orders"),
            TargetService: null,
            RawTarget: "OrderPlaced",
            Kind: DependencyKind.Messaging,
            Communication: CommunicationType.PubSubEvento,
            Resolution: ResolutionKind.Unresolved,
            Role: MessagingRole.Publish,
            Location: new SourceLocation("Orders/OrderService.cs", 42));

        Assert.Equal(MessagingRole.Publish, signal.Role);
        Assert.Equal("OrderPlaced", signal.RawTarget);
        Assert.Null(signal.TargetService);
        Assert.Equal(CommunicationType.PubSubEvento, signal.Communication);
    }

    [Fact]
    public void DependencySignal_NonMessagingSignal_HasNoRole()
    {
        var signal = new DependencySignal(
            SourceService: new ServiceName("Acme.Orders"),
            TargetService: null,
            RawTarget: "PaymentService",
            Kind: DependencyKind.Http,
            Communication: CommunicationType.SincronoBloqueante,
            Resolution: ResolutionKind.HardCoded,
            Role: null,
            Location: new SourceLocation("Orders/PaymentGateway.cs", 17));

        Assert.Null(signal.Role);
        Assert.Equal(DependencyKind.Http, signal.Kind);
    }

    [Fact]
    public void DependencyEdge_CarriesEveryEvidenceLocation()
    {
        var edge = new DependencyEdge(
            Source: new ServiceName("Acme.Orders"),
            Target: new ServiceName("Acme.Payments"),
            Communication: CommunicationType.SincronoBloqueante,
            Resolution: ResolutionKind.HardCoded,
            Evidence:
            [
                new SourceLocation("Orders/PaymentGateway.cs", 17),
                new SourceLocation("Orders/RefundGateway.cs", 88),
            ]);

        Assert.Equal(
            new[] { "Orders/PaymentGateway.cs:17", "Orders/RefundGateway.cs:88" },
            edge.Evidence.Select(e => e.ToString()));
    }

    [Fact]
    public void SourceLocation_ToString_IsFilePathColonLine()
    {
        Assert.Equal("src/Foo.cs:12", new SourceLocation("src/Foo.cs", 12).ToString());
    }
}
