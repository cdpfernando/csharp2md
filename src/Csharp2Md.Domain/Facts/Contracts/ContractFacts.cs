using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Facts;

public sealed record Contract : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Contract;

    public StructuralLiteral Proof { get; }

    private Contract(FactReference reference, StructuralLiteral proof)
    {
        Reference = reference;
        Proof = proof;
    }

    public static Contract Create(StructuralLiteral proof)
    {
        FactGuards.RequireInitialized(proof, nameof(proof));
        if (proof.Role != LiteralRole.ProtocolName && proof.Role != LiteralRole.SchemaName)
        {
            throw new ArgumentException(
                $"A contract requires a proof literal with role '{nameof(LiteralRole.ProtocolName)}' or '{nameof(LiteralRole.SchemaName)}', but was '{proof.Role}'.",
                nameof(proof));
        }

        var id = FactIdGrammar.Create("contract", ("schema-key", proof.Value));
        var reference = new FactReference(id, nameof(Contract));
        return new Contract(reference, proof);
    }
}

public sealed record ContractBinding : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Contract;

    public FactReference Operation { get; }

    public string PayloadRole { get; }

    public FactReference ClrSymbol { get; }

    public FactReference Contract { get; }

    private ContractBinding(FactReference reference, FactReference operation, string payloadRole, FactReference clrSymbol, FactReference contract)
    {
        Reference = reference;
        Operation = operation;
        PayloadRole = payloadRole;
        ClrSymbol = clrSymbol;
        Contract = contract;
    }

    public static ContractBinding Create(FactReference operation, string payloadRole, FactReference clrSymbol, FactReference contract)
    {
        FactGuards.RequireInitialized(operation, nameof(operation));
        if (!PayloadRoleTable.All.Contains(payloadRole, StringComparer.Ordinal))
        {
            throw new ArgumentException($"'{payloadRole}' is not a registered value of the 'payload_role' axis.", nameof(payloadRole));
        }

        FactGuards.RequireInitialized(clrSymbol, nameof(clrSymbol));
        FactGuards.RequireInitialized(contract, nameof(contract));

        var id = FactIdGrammar.Create(
            "contract-binding",
            ("boundary-operation", operation.Id.Value),
            ("payload-role", payloadRole),
            ("symbol", clrSymbol.Id.Value),
            ("contract", contract.Id.Value));
        var reference = new FactReference(id, nameof(ContractBinding));
        return new ContractBinding(reference, operation, payloadRole, clrSymbol, contract);
    }
}

public sealed record ContractRevision : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Contract;

    public FactReference Contract { get; }

    public string StructuralFingerprint { get; }

    private ContractRevision(FactReference reference, FactReference contract, string structuralFingerprint)
    {
        Reference = reference;
        Contract = contract;
        StructuralFingerprint = structuralFingerprint;
    }

    public static ContractRevision Create(FactReference contract, string structuralFingerprint)
    {
        FactGuards.RequireInitialized(contract, nameof(contract));
        var canonicalFingerprint = FactIdGrammar.RequireCanonicalText(structuralFingerprint, nameof(structuralFingerprint));

        var id = FactIdGrammar.Create("contract-revision", ("scope", contract.Id.Value), ("fingerprint", canonicalFingerprint));
        var reference = new FactReference(id, nameof(ContractRevision));
        return new ContractRevision(reference, contract, canonicalFingerprint);
    }
}
