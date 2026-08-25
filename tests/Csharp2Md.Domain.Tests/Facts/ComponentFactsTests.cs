using System.Reflection;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Domain.Tests.Facts;

public sealed class ComponentFactsTests
{
    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");

    private static FactReference SymbolReference(string metadataName)
    {
        var signature = CanonicalSymbolSignature.Create(
            "method", "global::Acme.Payment", metadataName, 0, "global::System.Void");
        return new FactReference(new FactId("symbol", signature.Value), "Symbol");
    }

    [Fact]
    [Trait("Requirement", "TAX-09")]
    public void Component_Create_OwnersInTwoOrdersWithADuplicate_ProduceOneIdentity()
    {
        var first = SymbolReference("First");
        var second = SymbolReference("Second");

        var forward = Component.Create(AcmeSolution, "Payments.Api", [first, second, first]);
        var reverse = Component.Create(AcmeSolution, "Payments.Api", [second, first]);

        Assert.Equal(forward, reverse);
        Assert.Equal(2, forward.Owners.Length);
    }

    [Fact]
    [Trait("Requirement", "TAX-09")]
    public void Component_Create_DefaultSolution_IsRejectedNamingSolution()
    {
        var exception = Assert.Throws<ArgumentException>(() => Component.Create(default, "Payments.Api", []));

        Assert.Equal("solution", exception.ParamName);
    }

    [Theory]
    [Trait("Requirement", "TAX-09")]
    [InlineData("")]
    [InlineData("   ")]
    public void Component_Create_InvalidName_IsRejectedNamingName(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() => Component.Create(AcmeSolution, name, []));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-09")]
    public void Component_Create_NullOwners_IsRejectedNamingOwners()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => Component.Create(AcmeSolution, "Payments.Api", null!));

        Assert.Equal("owners", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-09")]
    public void DeploymentUnit_Create_DefaultSolution_IsRejectedNamingSolution()
    {
        var exception = Assert.Throws<ArgumentException>(() => DeploymentUnit.Create(default, "Payments.Container"));

        Assert.Equal("solution", exception.ParamName);
    }

    [Theory]
    [Trait("Requirement", "TAX-09")]
    [InlineData("")]
    [InlineData("   ")]
    public void DeploymentUnit_Create_InvalidName_IsRejectedNamingName(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() => DeploymentUnit.Create(AcmeSolution, name));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-09")]
    public void DeploymentUnit_Create_ValidInputs_ProducesDeploymentUnitReference()
    {
        var unit = DeploymentUnit.Create(AcmeSolution, "Payments.Container");

        Assert.Equal("DeploymentUnit", unit.Reference.FactType);
    }

    [Fact]
    [Trait("Requirement", "TAX-09")]
    public void ExternalSystem_Create_DefaultSolution_IsRejectedNamingSolution()
    {
        var name = StructuralLiteral.Create(LiteralRole.ClientName, "stripe", "name");

        var exception = Assert.Throws<ArgumentException>(() => ExternalSystem.Create(default, name));

        Assert.Equal("solution", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-09")]
    public void ExternalSystem_Create_DefaultName_IsRejectedNamingName()
    {
        var exception = Assert.Throws<ArgumentException>(() => ExternalSystem.Create(AcmeSolution, default));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-09")]
    public void ExternalSystem_Create_WrongLiteralRole_IsRejectedNamingName()
    {
        var wrongRole = StructuralLiteral.Create(LiteralRole.Route, "/checkout", "name");

        var exception = Assert.Throws<ArgumentException>(() => ExternalSystem.Create(AcmeSolution, wrongRole));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-09")]
    public void ExternalSystem_Create_HasNoStringParameter_SoNoAbsoluteAddressOrCredentialCanBeSupplied()
    {
        var factory = typeof(ExternalSystem).GetMethod(nameof(ExternalSystem.Create), BindingFlags.Public | BindingFlags.Static)!;

        Assert.DoesNotContain(factory.GetParameters(), parameter => parameter.ParameterType == typeof(string));
        Assert.Equal(
            [typeof(SolutionId), typeof(StructuralLiteral)],
            factory.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    [Trait("Requirement", "TAX-09")]
    public void ExternalSystem_Create_ValidInputs_ProducesExternalSystemReference()
    {
        var name = StructuralLiteral.Create(LiteralRole.ClientName, "stripe", "name");

        var system = ExternalSystem.Create(AcmeSolution, name);

        Assert.Equal("ExternalSystem", system.Reference.FactType);
    }
}
