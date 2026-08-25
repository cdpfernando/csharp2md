using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Domain.Tests.Proof;

public sealed class ClassifierIdentityTests
{
    [Theory]
    [Trait("Requirement", "TAX-60")]
    [InlineData("Com.Acme.Detector")]
    [InlineData("com_acme_detector")]
    [InlineData("detector")]
    [InlineData("com..detector")]
    public void Create_NonLowerAsciiReverseDnsIdentifier_IsRejected(string id) =>
        Assert.Throws<ArgumentException>(() => ClassifierIdentity.Create(id, 1));

    [Theory]
    [Trait("Requirement", "TAX-60")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveVersion_IsRejected(int version) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ClassifierIdentity.Create("com.acme.detector", version));

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_ValidReverseDnsIdentifierAndPositiveVersion_IsAccepted()
    {
        var identity = ClassifierIdentity.Create("io.csharp2md.aspnet-core", 3);

        Assert.Equal("io.csharp2md.aspnet-core", identity.Id);
        Assert.Equal(3, identity.Version);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void UninitializedIdentity_Id_ThrowsInvalidOperationException()
    {
        var uninitialized = default(ClassifierIdentity);

        Assert.Throws<InvalidOperationException>(() => uninitialized.Id);
    }
}
