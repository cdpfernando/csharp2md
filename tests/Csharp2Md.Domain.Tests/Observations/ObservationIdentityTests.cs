using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Domain.Tests.Observations;

public sealed class ObservationIdentityTests
{
    private static FactReference Owner(string name = "value") =>
        new(FactIdGrammar.Create("widget", ("name", name)), "widget");

    [Fact]
    [Trait("Requirement", "TAX-35")]
    public void Constructor_SameOwnerKindPayloadAndOrdinal_ProducesEqualIdentities()
    {
        var owner = Owner();
        var payload = NormalizedPayload.Create([]);

        var first = new ObservationIdentity(owner, ObservationKind.Invocation, payload, 1);
        var second = new ObservationIdentity(owner, ObservationKind.Invocation, payload, 1);

        Assert.Equal(first, second);
    }

    [Fact]
    [Trait("Requirement", "TAX-36")]
    public void Constructor_HasNoEvidenceLocatorParameter_SoIdentityCannotDifferByLocator()
    {
        var parameters = typeof(ObservationIdentity).GetConstructors().Single().GetParameters();

        Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(EvidenceLocator));
    }

    [Fact]
    [Trait("Requirement", "TAX-37")]
    public void Constructor_DifferingOnlyByOccurrenceOrdinal_ProducesDistinctIdentities()
    {
        var owner = Owner();
        var payload = NormalizedPayload.Create([]);

        var first = new ObservationIdentity(owner, ObservationKind.Invocation, payload, 1);
        var second = new ObservationIdentity(owner, ObservationKind.Invocation, payload, 2);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [Trait("Requirement", "TAX-35")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveOrdinal_IsRejected(int ordinal)
    {
        var owner = Owner();
        var payload = NormalizedPayload.Create([]);

        Assert.Throws<ArgumentOutOfRangeException>(() => new ObservationIdentity(owner, ObservationKind.Invocation, payload, ordinal));
    }

    [Fact]
    [Trait("Requirement", "TAX-35")]
    public void ToString_ContainsNoLocationLabelOrTimestampComponent()
    {
        var identity = new ObservationIdentity(Owner(), ObservationKind.Invocation, NormalizedPayload.Create([]), 1);

        var rendered = identity.ToString();

        Assert.DoesNotContain("Locator", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Line", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Timestamp", rendered, StringComparison.OrdinalIgnoreCase);
    }
}
