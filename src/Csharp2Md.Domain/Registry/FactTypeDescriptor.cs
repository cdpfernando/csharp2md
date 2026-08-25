namespace Csharp2Md.Domain.Registry;

public enum FactFamily
{
    Structural,
    Architecture,
    Contract,
    Persistence,
    Configuration,
}

public sealed record FactTypeDescriptor(
    FactFamily Family,
    string Name,
    ImmutableArray<string> IdentityComponents);

internal static class FactTypeTable
{
    public static readonly ImmutableArray<FactTypeDescriptor> All =
    [
        new(FactFamily.Structural, "Solution", ["workspace", "path"]),
        new(FactFamily.Structural, "Project", ["solution", "path"]),
        new(FactFamily.Structural, "Document", ["project", "path"]),
        new(FactFamily.Structural, "Symbol", ["project", "signature"]),
        new(FactFamily.Architecture, "Component", ["solution", "name"]),
        new(FactFamily.Architecture, "DeploymentUnit", ["solution", "name"]),
        new(FactFamily.Architecture, "EntryPoint", ["component", "symbol"]),
        new(FactFamily.Architecture, "BoundaryOperation", ["component", "operation-key"]),
        new(FactFamily.Architecture, "ExternalSystem", ["solution", "name"]),
        new(FactFamily.Contract, "Contract", ["schema-key"]),
        new(FactFamily.Contract, "ContractBinding", ["boundary-operation", "payload-role", "symbol", "contract"]),
        new(FactFamily.Contract, "ContractRevision", ["scope", "fingerprint"]),
        new(FactFamily.Persistence, "DataStore", ["solution", "name"]),
        new(FactFamily.Persistence, "DataObject", ["data-store", "name"]),
        new(FactFamily.Persistence, "DataField", ["data-object", "name"]),
        new(FactFamily.Persistence, "DataOperation", ["symbol", "data-object", "operation"]),
        new(FactFamily.Configuration, "ConfigurationBinding", ["project", "key"]),
    ];
}
