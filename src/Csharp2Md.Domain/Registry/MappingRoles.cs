namespace Csharp2Md.Domain.Registry;

internal static class MappingRoleTable
{
    public static readonly ImmutableArray<string> All =
    [
        "contract-implementation",
        "data-object-mapping",
        "data-field-mapping",
        "serialization-binding",
    ];

    public static void RequireRegistered(string mappingRole)
    {
        if (!All.Contains(mappingRole, StringComparer.Ordinal))
        {
            throw new ArgumentException($"'{mappingRole}' is not a registered value of the 'maps-to.mapping_role' axis.", nameof(mappingRole));
        }
    }
}

internal static class PayloadRoleTable
{
    public static readonly ImmutableArray<string> All =
    [
        "request",
        "response",
        "header",
        "query-parameter",
    ];
}

public sealed partial record TaxonomyTables
{
    public ImmutableArray<string> MappingRoles { get; init; } = [];

    public ImmutableArray<string> PayloadRoles { get; init; } = [];
}
