using System.Reflection;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Facts;

public sealed class ConfigurationBindingTests
{
    private static FactReference ProjectReference(string path) =>
        new(new FactId("project", $"id1:project;path={path}"), "Project");

    private static StructuralLiteral Key(string value) => StructuralLiteral.Create(LiteralRole.ConfigurationKey, value, "configurationKey");

    [Fact]
    [Trait("Requirement", "TAX-12")]
    public void ConfigurationFamily_HasExactlyOneType()
    {
        var expectedNames = FactTypeTable.All
            .Where(descriptor => descriptor.Family == FactFamily.Configuration)
            .Select(descriptor => descriptor.Name)
            .ToHashSet();

        var actualNames = typeof(IFact).Assembly.GetTypes()
            .Where(type => typeof(IFact).IsAssignableFrom(type) && !type.IsInterface)
            .Select(type => type.Name)
            .Where(expectedNames.Contains)
            .ToHashSet();

        Assert.Single(expectedNames);
        Assert.Equal(expectedNames, actualNames);
    }

    [Fact]
    [Trait("Requirement", "TAX-12")]
    public void Create_ConfigurationKeyParameter_IsAStructuralLiteral_NotARawString()
    {
        var factory = typeof(ConfigurationBinding).GetMethod(nameof(ConfigurationBinding.Create), BindingFlags.Public | BindingFlags.Static)!;
        var keyParameter = factory.GetParameters().Single(p => p.Name == "configurationKey");

        Assert.Equal(typeof(StructuralLiteral), keyParameter.ParameterType);
        Assert.DoesNotContain(factory.GetParameters(), p => p.ParameterType == typeof(string));
    }

    [Fact]
    [Trait("Requirement", "TAX-12")]
    public void Create_WrongLiteralRole_IsRejectedNamingConfigurationKey()
    {
        var wrongRole = StructuralLiteral.Create(LiteralRole.Route, "/v1/orders", "configurationKey");

        var exception = Assert.Throws<ArgumentException>(() => ConfigurationBinding.Create(ProjectReference("proj"), wrongRole));

        Assert.Equal("configurationKey", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-12")]
    public void NoPublicInstancePropertyOfConfigurationBinding_IsARawStringValueConnectionStringOrToken()
    {
        var offending = typeof(ConfigurationBinding)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string))
            .ToArray();

        Assert.True(
            offending.Length == 0,
            $"ConfigurationBinding exposes raw string member(s): {string.Join(", ", offending.Select(p => p.Name))}");
    }

    [Fact]
    [Trait("Requirement", "TAX-12")]
    public void Create_DefaultBoundFact_IsRejectedNamingBoundFact()
    {
        var exception = Assert.Throws<ArgumentException>(() => ConfigurationBinding.Create(default, Key("ConnectionStrings:Default")));

        Assert.Equal("boundFact", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-12")]
    public void Create_ValidInputs_ProducesConfigurationBindingReferenceCarryingBothComponents()
    {
        var boundFact = ProjectReference("src/Acme.Payments/Acme.Payments.csproj");
        var key = Key("ConnectionStrings:Default");

        var binding = ConfigurationBinding.Create(boundFact, key);

        Assert.Equal("ConfigurationBinding", binding.Reference.FactType);
        Assert.Equal(boundFact, binding.BoundFact);
        Assert.Equal(key, binding.ConfigurationKey);
    }
}
