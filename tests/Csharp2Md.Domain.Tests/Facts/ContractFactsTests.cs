using System.Reflection;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Facts;

public sealed class ContractFactsTests
{
    private static FactReference OperationReference(string key) =>
        new(new FactId("boundary-operation", $"id1:boundary-operation;operation-key={key}"), "BoundaryOperation");

    private static FactReference SymbolReference(string metadataName)
    {
        var signature = CanonicalSymbolSignature.Create(
            "method", "global::Acme.Payment", metadataName, 0, "global::System.Void");
        return new FactReference(new FactId("symbol", signature.Value), "Symbol");
    }

    [Fact]
    [Trait("Requirement", "TAX-10")]
    public void ContractFamily_HasExactlyThreeTypes()
    {
        var expectedNames = FactTypeTable.All
            .Where(descriptor => descriptor.Family == FactFamily.Contract)
            .Select(descriptor => descriptor.Name)
            .ToHashSet();

        var actualNames = typeof(IFact).Assembly.GetTypes()
            .Where(type => typeof(IFact).IsAssignableFrom(type) && !type.IsInterface)
            .Select(type => type.Name)
            .Where(expectedNames.Contains)
            .ToHashSet();

        Assert.Equal(3, expectedNames.Count);
        Assert.Equal(expectedNames, actualNames);
    }

    [Fact]
    [Trait("Requirement", "TAX-16")]
    public void Contract_Create_HasNoClrTypeParameter_SoATypeAloneCannotConstituteAContract()
    {
        var factory = typeof(Contract).GetMethod(nameof(Contract.Create), BindingFlags.Public | BindingFlags.Static)!;

        Assert.DoesNotContain(factory.GetParameters(), p => p.ParameterType == typeof(Type));
        Assert.Equal([typeof(StructuralLiteral)], factory.GetParameters().Select(p => p.ParameterType));
    }

    [Fact]
    [Trait("Requirement", "TAX-16")]
    public void Contract_Create_ProofWithoutProtocolOrSchemaRole_IsRejectedNamingProof()
    {
        var notAProof = StructuralLiteral.Create(LiteralRole.Route, "/v1/orders", "proof");

        var exception = Assert.Throws<ArgumentException>(() => Contract.Create(notAProof));

        Assert.Equal("proof", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-17")]
    public void Contract_Create_TwoOwnersWithSameProvenSchemaKey_ProduceOneContractIdentity()
    {
        var ownerAProof = StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof");
        var ownerBProof = StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof");

        var fromOwnerA = Contract.Create(ownerAProof);
        var fromOwnerB = Contract.Create(ownerBProof);

        Assert.Equal(fromOwnerA.Reference, fromOwnerB.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-17")]
    public void Contract_Create_TwoOwnersWithSimilarlyNamedPayloadsAndNoSharedKey_ProduceDistinctContractIdentities()
    {
        var ownerAProof = StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof");
        var ownerBProof = StructuralLiteral.Create(LiteralRole.SchemaName, "billing.v1.OrderPlaced", "proof");

        var fromOwnerA = Contract.Create(ownerAProof);
        var fromOwnerB = Contract.Create(ownerBProof);

        Assert.NotEqual(fromOwnerA.Reference, fromOwnerB.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-10")]
    public void ContractBinding_Create_DefaultOperation_IsRejectedNamingOperation()
    {
        var exception = Assert.Throws<ArgumentException>(() => ContractBinding.Create(
            default, PayloadRoleTable.All[0], SymbolReference("Charge"), OperationReference("contract-key")));

        Assert.Equal("operation", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-10")]
    public void ContractBinding_Create_OutOfVocabularyPayloadRole_IsRejectedNamingPayloadRole()
    {
        var exception = Assert.Throws<ArgumentException>(() => ContractBinding.Create(
            OperationReference("op"), "bogus-role", SymbolReference("Charge"), OperationReference("contract-key")));

        Assert.Equal("payloadRole", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-10")]
    public void ContractBinding_Create_DefaultClrSymbol_IsRejectedNamingClrSymbol()
    {
        var exception = Assert.Throws<ArgumentException>(() => ContractBinding.Create(
            OperationReference("op"), PayloadRoleTable.All[0], default, OperationReference("contract-key")));

        Assert.Equal("clrSymbol", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-10")]
    public void ContractBinding_Create_DefaultContract_IsRejectedNamingContract()
    {
        var exception = Assert.Throws<ArgumentException>(() => ContractBinding.Create(
            OperationReference("op"), PayloadRoleTable.All[0], SymbolReference("Charge"), default));

        Assert.Equal("contract", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-10")]
    public void ContractBinding_Create_AllComponentsSupplied_ProducesBindingReference()
    {
        var proof = StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof");
        var contract = Contract.Create(proof);

        var binding = ContractBinding.Create(OperationReference("op"), PayloadRoleTable.All[0], SymbolReference("Charge"), contract.Reference);

        Assert.Equal("ContractBinding", binding.Reference.FactType);
    }

    [Fact]
    [Trait("Requirement", "TAX-10")]
    public void ContractRevision_Create_StoresTheCallerSuppliedFingerprintVerbatim()
    {
        var proof = StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof");
        var contract = Contract.Create(proof);

        var revision = ContractRevision.Create(contract.Reference, "shape-3-fields-v1");

        Assert.Equal("shape-3-fields-v1", revision.StructuralFingerprint);
    }

    [Fact]
    [Trait("Requirement", "TAX-10")]
    public void ContractRevision_Create_FingerprintParameterIsAnOpaqueString_NotComputedFromAStructuralLiteral()
    {
        var factory = typeof(ContractRevision).GetMethod(nameof(ContractRevision.Create), BindingFlags.Public | BindingFlags.Static)!;
        var fingerprintParameter = factory.GetParameters().Single(p => p.Name == "structuralFingerprint");

        Assert.Equal(typeof(string), fingerprintParameter.ParameterType);
        Assert.DoesNotContain(factory.GetParameters(), p => p.ParameterType == typeof(StructuralLiteral));
    }

    [Fact]
    [Trait("Requirement", "TAX-10")]
    public void ContractRevision_Create_NonCanonicalFingerprint_IsRejectedNamingFingerprint()
    {
        var proof = StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof");
        var contract = Contract.Create(proof);

        var exception = Assert.Throws<ArgumentException>(() => ContractRevision.Create(contract.Reference, "   "));

        Assert.Equal("structuralFingerprint", exception.ParamName);
    }
}
