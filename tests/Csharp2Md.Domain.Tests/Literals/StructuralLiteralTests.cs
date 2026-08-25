using System.Reflection;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Domain.Tests.Literals;

public sealed class StructuralLiteralTests
{
    [Fact]
    [Trait("Requirement", "TAX-79")]
    public void LiteralRole_HasExactlyTheEightDocumentedRoles()
    {
        var expected = new HashSet<string>
        {
            "Route", "ProtocolName", "Channel", "SchemaName", "TableName", "FieldName", "ConfigurationKey", "ClientName",
        };
        var actual = Enum.GetNames<LiteralRole>().ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Theory]
    [Trait("Requirement", "TAX-79")]
    [InlineData(LiteralRole.Route)]
    [InlineData(LiteralRole.ProtocolName)]
    [InlineData(LiteralRole.Channel)]
    [InlineData(LiteralRole.SchemaName)]
    [InlineData(LiteralRole.TableName)]
    [InlineData(LiteralRole.FieldName)]
    [InlineData(LiteralRole.ConfigurationKey)]
    [InlineData(LiteralRole.ClientName)]
    public void Create_OneLiteralPerRole_IsAccepted(LiteralRole role)
    {
        var literal = StructuralLiteral.Create(role, "some-value", "someField");

        Assert.Equal(role, literal.Role);
        Assert.Equal("some-value", literal.Value);
    }

    [Fact]
    [Trait("Requirement", "TAX-80")]
    public void Create_UndefinedRoleReachedByCast_IsRejectedNamingTheAxis()
    {
        var undefined = (LiteralRole)99;

        var exception = Assert.Throws<ArgumentException>(() => StructuralLiteral.Create(undefined, "value", "channel"));

        Assert.Contains(nameof(LiteralRole), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-80")]
    public void Create_UndefinedRole_IsRejectedNamingTheField()
    {
        var undefined = (LiteralRole)99;

        var exception = Assert.Throws<ArgumentException>(() => StructuralLiteral.Create(undefined, "value", "channel"));

        Assert.Equal("channel", exception.ParamName);
    }

    [Theory]
    [Trait("Requirement", "TAX-80")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad  value")]
    public void Create_NonCanonicalValue_IsRejectedNamingTheField(string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => StructuralLiteral.Create(LiteralRole.Route, value, "routeField"));

        Assert.Equal("routeField", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-80")]
    public void NoPayloadCarryingPropertyOnAFactOrObservationTypeBypassesStructuralLiteral()
    {
        // Vacuously true today: no Facts/Observations record types exist yet in this batch
        // (T23-T27 only add evidence and payload primitives). This scan will start failing the
        // moment a later phase introduces a `string`-typed payload-value property that bypasses
        // StructuralLiteral, since StructuralLiteral.Create is the only sanctioned way for a
        // literal to reach a fact or observation payload (TAX-79, TAX-80).
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        string[] payloadPropertyNames = ["Value", "Literal", "PayloadValue"];

        var offending = typeof(AssemblyMarker).Assembly.GetTypes()
            .Where(type => type.Namespace is not null
                && (type.Namespace.StartsWith("Csharp2Md.Domain.Facts", StringComparison.Ordinal)
                    || type.Namespace.StartsWith("Csharp2Md.Domain.Observations", StringComparison.Ordinal)))
            .SelectMany(type => type.GetProperties(flags))
            .FirstOrDefault(property => property.PropertyType == typeof(string)
                && payloadPropertyNames.Contains(property.Name, StringComparer.Ordinal));

        Assert.True(
            offending is null,
            $"'{offending?.DeclaringType?.FullName}.{offending?.Name}' exposes a bare string payload value; it must be a StructuralLiteral.");
    }
}
