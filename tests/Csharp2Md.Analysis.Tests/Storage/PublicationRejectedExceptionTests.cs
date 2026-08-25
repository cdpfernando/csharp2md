using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Tests.Storage;

public sealed class PublicationRejectedExceptionTests
{
    [Fact]
    [Trait("Requirement", "STOR-31")]
    public void ThrowCatch_ExposesGateAndDetail()
    {
        var thrown = Assert.Throws<PublicationRejectedException>(RejectIdentityCollision);

        Assert.Equal("identity-collision", thrown.Gate);
        Assert.Equal("duplicate solution identity", thrown.Detail);
    }

    private static void RejectIdentityCollision() =>
        throw new PublicationRejectedException("identity-collision", "duplicate solution identity");
}
